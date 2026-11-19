using System.Globalization;
using System.Text.Json;
using LifeUp.Application.Quotes;
using Microsoft.Extensions.Options;

namespace LifeUp.Infrastructure.Quotes;

// Shanghai Gold Exchange Au99.99 spot price in CNY/g from Eastmoney's public quote endpoint.
// 积存金按上金所现货价加减点差定价，这里取现货价作为估值参考，不声明为银行成交价。
public sealed class SgeGoldQuoteProvider : IGoldQuoteProvider
{
    public const string SourceName = "eastmoney_sge";

    private const string QuotePath = "api/qt/stock/get?secid=118.AU9999&fields=f43,f57,f58,f59,f86";

    private readonly HttpClient _httpClient;
    private readonly QuoteProviderOptions _options;

    public SgeGoldQuoteProvider(HttpClient httpClient, IOptions<QuoteProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ExternalGoldQuote> GetLatestAsync(CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        foreach (var baseUrl in QuoteBaseUrls())
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}{QuotePath}");
            request.Headers.Referrer = new Uri("https://quote.eastmoney.com/");

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                return await ReadQuoteAsync(response.Content, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or QuoteUnavailableException)
            {
                lastError = exception;
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = exception;
            }
        }

        throw new QuoteUnavailableException(
            "黄金现货行情服务未能提供有效报价。",
            lastError ?? new HttpRequestException("没有可用的黄金现货行情地址。"));
    }

    private static async Task<ExternalGoldQuote> ReadQuoteAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("f57", out var symbol)
            || symbol.ValueKind != JsonValueKind.String
            || !string.Equals(symbol.GetString(), "AU9999", StringComparison.OrdinalIgnoreCase))
        {
            throw new QuoteUnavailableException("黄金现货行情服务未返回 Au99.99 报价。");
        }

        if (EastmoneyJsonReader.ReadInt(data, "f59") is not int precision || precision is < 0 or > 6
            || EastmoneyJsonReader.ReadScaledDecimal(data, "f43", precision) is not decimal quote || quote <= 0m)
        {
            throw new QuoteUnavailableException("黄金现货行情服务返回的报价无效。");
        }

        var obtainedAt = DateTimeOffset.UtcNow;
        var timestamp = EastmoneyJsonReader.ReadLong(data, "f86");
        // 允许源站与本机存在少量时钟偏差，避免快 1–2 秒误判为无效报价。
        if (timestamp is null or <= 0 || timestamp > obtainedAt.ToUnixTimeSeconds() + 300)
        {
            throw new QuoteUnavailableException("黄金现货行情服务返回的报价时间无效。");
        }

        var quoteTime = DateTimeOffset.FromUnixTimeSeconds(timestamp.Value).ToOffset(TimeSpan.FromHours(8));
        return new ExternalGoldQuote(quote, DateOnly.FromDateTime(quoteTime.Date), SourceName, obtainedAt);
    }

    private IEnumerable<string> QuoteBaseUrls()
    {
        var primary = _httpClient.BaseAddress?.ToString() ?? _options.GoldSgeBaseUrl;
        return EastmoneyJsonReader.NormalizeBaseUrls(primary, _options.GoldSgeFallbackBaseUrl);
    }
}

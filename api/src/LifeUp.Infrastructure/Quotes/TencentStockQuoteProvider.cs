using System.Globalization;
using LifeUp.Application.Quotes;
using LifeUp.Domain.Assets;
using Microsoft.Extensions.Options;

namespace LifeUp.Infrastructure.Quotes;

// 腾讯财经公开行情与搜索接口（qt.gtimg.cn / smartbox.gtimg.cn），覆盖沪深京 A 股及场内 ETF / LOF。
public sealed class TencentStockQuoteProvider : IStockQuoteProvider
{
    public const string SourceName = "tencent";

    private readonly HttpClient _httpClient;
    private readonly QuoteProviderOptions _options;

    public TencentStockQuoteProvider(HttpClient httpClient, IOptions<QuoteProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ExternalStockQuote> GetLatestAsync(
        StockMarket market,
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new QuoteUnavailableException("股票代码为空，无法获取行情。");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        var symbol = StockQuoteParsing.ToMarketSymbol(market, normalizedCode);
        var baseUrl = StockQuoteParsing.NormalizeBaseUrl(_httpClient.BaseAddress?.ToString() ?? _options.StockTencentBaseUrl);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}q={Uri.EscapeDataString(symbol)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new QuoteUnavailableException($"腾讯股票行情服务返回 {(int)response.StatusCode}。");
        }

        var body = await StockQuoteParsing.ReadTextAsync(response.Content, cancellationToken);
        var payload = StockQuoteParsing.ExtractQuotedPayload(body);

        if (string.IsNullOrWhiteSpace(payload) || !payload.Contains('~'))
        {
            throw new QuoteUnavailableException("腾讯股票行情服务未返回有效记录。");
        }

        var fields = payload.Split('~');

        if (fields.Length < 31)
        {
            throw new QuoteUnavailableException("腾讯股票行情服务返回的数据格式无法解析。");
        }

        var name = string.IsNullOrWhiteSpace(fields[1]) ? normalizedCode : fields[1].Trim();
        var quoteCode = string.IsNullOrWhiteSpace(fields[2]) ? normalizedCode : fields[2].Trim().ToUpperInvariant();
        var price = StockQuoteParsing.TryParseDecimal(fields[3]);
        var previousClose = StockQuoteParsing.TryParseDecimal(fields[4]);

        if (price is null || price <= 0m)
        {
            throw new QuoteUnavailableException("腾讯股票行情服务返回的最新价无效。");
        }

        if (previousClose is <= 0m)
        {
            previousClose = null;
        }

        var priceDate = ParsePriceDate(fields[30]);

        return new ExternalStockQuote(
            market,
            quoteCode,
            name,
            price.Value,
            priceDate,
            previousClose,
            null,
            SourceName,
            DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<ExternalStockSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var searchBaseUrl = StockQuoteParsing.NormalizeBaseUrl(_options.StockTencentSearchBaseUrl);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{searchBaseUrl}s3/?v=2&q={Uri.EscapeDataString(query.Trim())}&t=all");

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new QuoteUnavailableException($"腾讯股票搜索服务返回 {(int)response.StatusCode}。");
        }

        var body = await StockQuoteParsing.ReadTextAsync(response.Content, cancellationToken);
        var payload = StockQuoteParsing.ExtractQuotedPayload(body);

        if (string.IsNullOrWhiteSpace(payload) || string.Equals(payload, "N", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var results = new List<ExternalStockSearchResult>();
        var seen = new HashSet<(StockMarket Market, string Code)>();

        foreach (var entry in payload.Split('^', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = entry.Split('~');

            if (fields.Length < 5)
            {
                continue;
            }

            if (!StockQuoteParsing.TryParseMarketPrefix(fields[0], out var market))
            {
                continue;
            }

            var code = fields[1].Trim().ToUpperInvariant();
            var name = StockQuoteParsing.DecodeUnicodeEscapes(fields[2]).Trim();
            var category = fields[4].Trim();

            if (!StockQuoteParsing.IsSixDigitCode(code) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!IsSupportedSecurity(market, code, category))
            {
                continue;
            }

            if (seen.Add((market, code)))
            {
                results.Add(new ExternalStockSearchResult(market, code, name));

                if (results.Count >= 20)
                {
                    break;
                }
            }
        }

        return results;
    }

    private static DateOnly ParsePriceDate(string rawTimestamp)
    {
        var trimmed = rawTimestamp.Trim();

        if (trimmed.Length >= 8
            && DateOnly.TryParseExact(trimmed[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return StockQuoteParsing.BeijingToday();
    }

    private static bool IsSupportedSecurity(StockMarket market, string code, string category)
    {
        if (string.Equals(category, "GP-A", StringComparison.OrdinalIgnoreCase)
            || category.StartsWith("GP-A-", StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, "GP", StringComparison.OrdinalIgnoreCase))
        {
            return StockQuoteParsing.IsAShareCode(market, code);
        }

        if (string.Equals(category, "ETF", StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, "LOF", StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, "FJ", StringComparison.OrdinalIgnoreCase))
        {
            return StockQuoteParsing.IsExchangeTradedFundCode(code);
        }

        return false;
    }
}

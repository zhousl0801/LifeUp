using System.Globalization;
using LifeUp.Application.Quotes;
using LifeUp.Domain.Assets;
using Microsoft.Extensions.Options;

namespace LifeUp.Infrastructure.Quotes;

// 新浪财经公开行情与搜索接口（hq.sinajs.cn / suggest3.sinajs.cn），请求需携带 finance.sina.com.cn Referer。
public sealed class SinaStockQuoteProvider : IStockQuoteProvider
{
    public const string SourceName = "sina";

    private static readonly Uri SinaReferer = new("https://finance.sina.com.cn/");

    private readonly HttpClient _httpClient;
    private readonly QuoteProviderOptions _options;

    public SinaStockQuoteProvider(HttpClient httpClient, IOptions<QuoteProviderOptions> options)
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
        var baseUrl = StockQuoteParsing.NormalizeBaseUrl(_httpClient.BaseAddress?.ToString() ?? _options.StockSinaBaseUrl);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}list={Uri.EscapeDataString(symbol)}");
        request.Headers.Referrer = SinaReferer;

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new QuoteUnavailableException($"新浪股票行情服务返回 {(int)response.StatusCode}。");
        }

        var body = await StockQuoteParsing.ReadTextAsync(response.Content, cancellationToken);
        var payload = StockQuoteParsing.ExtractQuotedPayload(body);

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new QuoteUnavailableException("新浪股票行情服务未返回有效记录。");
        }

        var fields = payload.Split(',');

        if (fields.Length < 31)
        {
            throw new QuoteUnavailableException("新浪股票行情服务返回的数据格式无法解析。");
        }

        var name = string.IsNullOrWhiteSpace(fields[0]) ? normalizedCode : fields[0].Trim();
        var previousClose = StockQuoteParsing.TryParseDecimal(fields[2]);
        var price = StockQuoteParsing.TryParseDecimal(fields[3]);

        if (price is null || price <= 0m)
        {
            throw new QuoteUnavailableException("新浪股票行情服务返回的最新价无效。");
        }

        if (previousClose is <= 0m)
        {
            previousClose = null;
        }

        var priceDate = DateOnly.TryParseExact(
            fields[30].Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate)
            ? parsedDate
            : StockQuoteParsing.BeijingToday();

        return new ExternalStockQuote(
            market,
            normalizedCode,
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

        var searchBaseUrl = StockQuoteParsing.NormalizeBaseUrl(_options.StockSinaSearchBaseUrl);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{searchBaseUrl}suggest/type=11,12,13,14,15,21,22,23,24,25,26,81&key={Uri.EscapeDataString(query.Trim())}");
        request.Headers.Referrer = SinaReferer;

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new QuoteUnavailableException($"新浪股票搜索服务返回 {(int)response.StatusCode}。");
        }

        var body = await StockQuoteParsing.ReadTextAsync(response.Content, cancellationToken);
        var payload = StockQuoteParsing.ExtractQuotedPayload(body);

        if (string.IsNullOrWhiteSpace(payload))
        {
            return [];
        }

        var results = new List<ExternalStockSearchResult>();
        var seen = new HashSet<(StockMarket Market, string Code)>();

        foreach (var entry in payload.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var cols = entry.Split(',');

            if (cols.Length < 5)
            {
                continue;
            }

            var type = cols[1].Trim();
            var code = cols[2].Trim().ToUpperInvariant();
            var fullCode = cols[3].Trim().ToLowerInvariant();
            var name = !string.IsNullOrWhiteSpace(cols[4]) ? cols[4].Trim() : cols[0].Trim();

            if (!StockQuoteParsing.IsSixDigitCode(code) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!TryResolveSecurity(type, code, fullCode, out var market))
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

    private static bool TryResolveSecurity(string type, string code, string fullCode, out StockMarket market)
    {
        if (type == "11")
        {
            if (fullCode.Length >= 2 && StockQuoteParsing.TryParseMarketPrefix(fullCode[..2], out market))
            {
                return StockQuoteParsing.IsAShareCode(market, code);
            }

            market = default;
            return false;
        }

        if (type is "22" or "23" && StockQuoteParsing.IsExchangeTradedFundCode(code))
        {
            if (fullCode.Length >= 2 && StockQuoteParsing.TryParseMarketPrefix(fullCode[..2], out market))
            {
                return true;
            }

            market = code.StartsWith('5') ? StockMarket.Shanghai : StockMarket.Shenzhen;
            return true;
        }

        market = default;
        return false;
    }
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LifeUp.Domain.Assets;

namespace LifeUp.Infrastructure.Quotes;

internal static partial class StockQuoteParsing
{
    private static readonly Encoding Gb18030Encoding = CreateGb18030Encoding();

    public static string ToMarketSymbol(StockMarket market, string code)
    {
        var prefix = market switch
        {
            StockMarket.Shanghai => "sh",
            StockMarket.Shenzhen => "sz",
            StockMarket.Beijing => "bj",
            _ => "sz"
        };

        return $"{prefix}{code}";
    }

    public static async Task<string> ReadTextAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var bytes = await content.ReadAsByteArrayAsync(cancellationToken);
        var charset = content.Headers.ContentType?.CharSet?.Trim().Trim('"');

        if (string.Equals(charset, "utf-8", StringComparison.OrdinalIgnoreCase)
            || string.Equals(charset, "utf8", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetString(bytes);
        }

        return Gb18030Encoding.GetString(bytes);
    }

    public static string? ExtractQuotedPayload(string responseText)
    {
        var firstQuote = responseText.IndexOf('"');

        if (firstQuote < 0)
        {
            return null;
        }

        var lastQuote = responseText.LastIndexOf('"');

        if (lastQuote <= firstQuote)
        {
            return null;
        }

        return responseText.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
    }

    public static decimal? TryParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return decimal.TryParse(raw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    public static DateOnly BeijingToday()
    {
        return DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));
    }

    public static string NormalizeBaseUrl(string baseUrl)
    {
        return baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
    }

    public static string DecodeUnicodeEscapes(string text)
    {
        if (!text.Contains("\\u", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        return UnicodeEscapeRegex().Replace(text, static match =>
        {
            var codePoint = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ((char)codePoint).ToString();
        });
    }

    public static bool IsSixDigitCode(string code)
    {
        return code.Length == 6 && code.All(char.IsDigit);
    }

    public static bool IsAShareCode(StockMarket market, string code)
    {
        return market switch
        {
            StockMarket.Shanghai => code.StartsWith('6'),
            StockMarket.Shenzhen => code.StartsWith('0') || code.StartsWith("30"),
            StockMarket.Beijing => code.StartsWith('4') || code.StartsWith('8') || code.StartsWith("92"),
            _ => false
        };
    }

    public static bool IsExchangeTradedFundCode(string code)
    {
        // 沪市 5xxxxx（ETF 51/56/58、LOF 502、REITs 508）；深市 15xxxx（ETF）、16xxxx（LOF）、18xxxx（封闭式基金）。
        return code.StartsWith('5')
            || code.StartsWith("15") || code.StartsWith("16") || code.StartsWith("18");
    }

    public static bool TryParseMarketPrefix(string prefix, out StockMarket market)
    {
        switch (prefix.Trim().ToLowerInvariant())
        {
            case "sh":
                market = StockMarket.Shanghai;
                return true;
            case "sz":
                market = StockMarket.Shenzhen;
                return true;
            case "bj":
                market = StockMarket.Beijing;
                return true;
            default:
                market = default;
                return false;
        }
    }

    private static Encoding CreateGb18030Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding("GB18030");
    }

    [GeneratedRegex(@"\\u([0-9a-fA-F]{4})", RegexOptions.Compiled)]
    private static partial Regex UnicodeEscapeRegex();
}

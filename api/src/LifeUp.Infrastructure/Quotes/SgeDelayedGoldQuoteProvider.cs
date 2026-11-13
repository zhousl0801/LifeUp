using System.Globalization;
using System.Text.RegularExpressions;
using LifeUp.Application.Quotes;

namespace LifeUp.Infrastructure.Quotes;

public sealed partial class SgeDelayedGoldQuoteProvider(HttpClient httpClient)
{
    public const string SourceName = "sge_delayed";

    public async Task<ExternalGoldQuote> GetLatestAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("sjzx/yshqbg", cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var date = QuoteDatePattern().Match(html);
        var price = QuotePricePattern().Match(html);

        if (!date.Success || !price.Success
            || !DateOnly.TryParseExact(date.Groups["date"].Value, "yyyy年M月d日", CultureInfo.InvariantCulture, DateTimeStyles.None, out var effectiveDate)
            || !decimal.TryParse(price.Groups["price"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quote)
            || quote <= 0m)
        {
            throw new QuoteUnavailableException("上金所延时行情未返回有效的 Au99.99 最新价和行情日期。");
        }

        var obtainedAt = DateTimeOffset.UtcNow;
        var businessDate = DateOnly.FromDateTime(obtainedAt.ToOffset(TimeSpan.FromHours(8)).Date);
        if (effectiveDate > businessDate)
        {
            throw new QuoteUnavailableException("上金所延时行情日期晚于当前业务日期。");
        }

        return new ExternalGoldQuote(quote, effectiveDate, SourceName, obtainedAt);
    }

    [GeneratedRegex(@"<h1\b[^>]*>\s*上海黄金交易所(?<date>\d{4}年\d{1,2}月\d{1,2}日)延时行情\s*</h1>", RegexOptions.IgnoreCase, 500)]
    private static partial Regex QuoteDatePattern();

    // 同时识别表头和合约，避免把最高价或相似合约当成 Au99.99 最新价。
    [GeneratedRegex(@"<th\b[^>]*>\s*合约\s*</th>\s*<th\b[^>]*>\s*最新价\s*</th>(?:(?!</table>)[\s\S])*?<tr\b[^>]*>\s*<td\b[^>]*>\s*Au99\.99\s*</td>\s*<td\b[^>]*>\s*(?:<span\b[^>]*>\s*)?(?<price>\d+(?:\.\d+)?)\s*(?:</span>\s*)?</td>", RegexOptions.IgnoreCase, 500)]
    private static partial Regex QuotePricePattern();
}

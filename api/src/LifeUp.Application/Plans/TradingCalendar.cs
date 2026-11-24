using System.Text.Json;
using LifeUp.Domain.Plans;

namespace LifeUp.Application.Plans;

/// <summary>
/// 三地按同一个本地日历日期判断；修订追加版本并设置未来生效日，保留旧版本。
/// </summary>
public sealed class TradingCalendar
{
    private const int MaximumNextTradingDaySearchDays = 366;
    private static readonly (string Code, string Name)[] Markets = [("CN", "中国"), ("US", "美国"), ("HK", "香港")];
    private readonly Dictionary<(string Market, int Year), IReadOnlyList<IndexedCalendarYear>> _versionsByMarketYear;

    public static TradingCalendar Default { get; } = Load();

    public TradingCalendar(IReadOnlyList<MarketCalendarYear> versions)
    {
        if (versions.Any(version => !Markets.Any(market => market.Code == version.Market)
            || version.ClosedDates.Any(date => date.Year != version.Year)
            || version.HalfDays.Any(date => date.Year != version.Year)
            || version.ClosedDates.Intersect(version.HalfDays).Any())
            || versions.GroupBy(version => (version.Market, version.Year, version.EffectiveFrom)).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("市场日历数据无效或生效日期重复。", nameof(versions));
        }

        _versionsByMarketYear = versions
            .GroupBy(version => (version.Market, version.Year))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<IndexedCalendarYear>)group
                    .OrderByDescending(version => version.EffectiveFrom)
                    .Select(version => new IndexedCalendarYear(
                        version,
                        version.ClosedDates.ToHashSet(),
                        version.HalfDays.ToHashSet()))
                    .ToList());
    }

    public TradingDayDto GetDay(DateOnly date)
    {
        var weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var markets = new List<MarketDayDto>(Markets.Length);

        foreach (var market in Markets)
        {
            var indexed = FindEffectiveCalendar(market.Code, date);
            var calendar = indexed?.Source;
            var open = !weekend && !(indexed?.ClosedDates.Contains(date) ?? false);

            markets.Add(new MarketDayDto(
                market.Code,
                market.Name,
                date.Year,
                open,
                open && (indexed?.HalfDays.Contains(date) ?? false),
                calendar is null,
                calendar?.Version,
                calendar?.SourceUrl,
                calendar?.PublishedOn,
                calendar?.VerifiedOn));
        }

        return new TradingDayDto(date, markets.All(market => market.IsOpen), markets);
    }

    public DateOnly NextTradingDay(DateOnly after)
    {
        return NextTradingDay(after, AutoPlanTradingScope.ThreeMarkets);
    }

    public DateOnly NextTradingDay(DateOnly after, AutoPlanTradingScope scope)
    {
        var date = after.AddDays(1);

        for (var step = 0; step < MaximumNextTradingDaySearchDays; step++)
        {
            if (IsTradingDay(date, scope))
            {
                return date;
            }

            if (date == DateOnly.MaxValue)
            {
                break;
            }

            date = date.AddDays(1);
        }

        throw new InvalidOperationException($"在 {after:yyyy-MM-dd} 之后的 {MaximumNextTradingDaySearchDays} 天内未找到可用交易日。");
    }

    private IndexedCalendarYear? FindEffectiveCalendar(string marketCode, DateOnly date)
    {
        if (!_versionsByMarketYear.TryGetValue((marketCode, date.Year), out var versions))
        {
            return null;
        }

        // 已按 EffectiveFrom 降序排列，首条满足 EffectiveFrom <= date 即为生效版本。
        foreach (var version in versions)
        {
            if (version.Source.EffectiveFrom <= date)
            {
                return version;
            }
        }

        return null;
    }

    private sealed record IndexedCalendarYear(
        MarketCalendarYear Source,
        HashSet<DateOnly> ClosedDates,
        HashSet<DateOnly> HalfDays);

    public bool IsTradingDay(DateOnly date, AutoPlanTradingScope scope)
    {
        var day = GetDay(date);

        return scope == AutoPlanTradingScope.China
            ? day.Markets.Any(market => market.Market == "CN" && market.IsOpen)
            : day.IsTradingDay;
    }

    public IReadOnlyList<TradingDayDto> GetDays(DateOnly from, DateOnly to)
    {
        var days = new List<TradingDayDto>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            days.Add(GetDay(date));
        }

        return days;
    }

    public IReadOnlyList<TradingDayDto> GetMonth(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return GetDays(first, first.AddMonths(1).AddDays(-1));
    }

    private static TradingCalendar Load()
    {
        var assembly = typeof(TradingCalendar).Assembly;
        var versions = new List<MarketCalendarYear>();

        foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.Contains(".Plans.Calendars.") && name.EndsWith(".json")))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            versions.AddRange(JsonSerializer.Deserialize<List<MarketCalendarYear>>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
        }

        return new TradingCalendar(versions);
    }
}

public sealed record MarketCalendarYear(
    string Market,
    string Name,
    int Year,
    string Version,
    string SourceUrl,
    DateOnly? PublishedOn,
    DateOnly VerifiedOn,
    DateOnly EffectiveFrom,
    IReadOnlyList<DateOnly> ClosedDates,
    IReadOnlyList<DateOnly> HalfDays);

public sealed record MarketDayDto(
    string Market,
    string Name,
    int Year,
    bool IsOpen,
    bool IsHalfDay,
    bool UsesWeekdayFallback,
    string? Version,
    string? SourceUrl,
    DateOnly? PublishedOn,
    DateOnly? VerifiedOn);

public sealed record TradingDayDto(DateOnly Date, bool IsTradingDay, IReadOnlyList<MarketDayDto> Markets);

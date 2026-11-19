using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Ledgers;

public sealed class EntryStatsService
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly BusinessDateProvider _dates;

    public EntryStatsService(ILedgerRepository ledgerRepository, BusinessDateProvider dates)
    {
        _ledgerRepository = ledgerRepository;
        _dates = dates;
    }

    public async Task<EntryStatsDto?> GetAsync(
        EntryPeriod period,
        DateOnly? anchor,
        CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetAsync(cancellationToken);

        if (ledger is null)
        {
            return null;
        }

        var today = _dates.Today;
        var anchorDate = anchor ?? today;

        if (anchorDate > today)
        {
            anchorDate = today;
        }

        var (from, to) = EntryPeriodRange.For(period, anchorDate);
        var dataTo = to < today ? to : today;
        var dataFrom = from > today ? today.AddDays(1) : from;
        IReadOnlyList<LedgerEntry> entries = dataFrom <= dataTo
            ? await _ledgerRepository.GetEntriesAsync(ledger.Id, dataFrom, dataTo, cancellationToken)
            : [];
        var income = entries.Where(entry => entry.Kind == LedgerEntryKind.Income).Sum(entry => entry.Amount);
        var expense = entries.Where(entry => entry.Kind == LedgerEntryKind.Expense).Sum(entry => entry.Amount);
        var coverageStart = from < ledger.StartDate ? ledger.StartDate : from;
        var coveredDays = CalculateCoveredDays(period, coverageStart, dataFrom > dataTo ? from.AddDays(-1) : dataTo);
        var note = BuildNote(from, dataFrom, dataTo, ledger.StartDate);

        var categories = await _ledgerRepository.GetCategoriesAsync(ledger.Id, cancellationToken);
        var groupNames = categories.Where(category => category.IsGroup).ToDictionary(category => category.Id, category => category.Name);
        var groupsByCategory = categories.Where(category => !category.IsGroup).ToDictionary(
            category => (category.Kind, category.Name),
            category => category.ParentId is Guid parent && groupNames.TryGetValue(parent, out var name) ? name : "未归类");

        var iconsByCategory = categories.Where(category => !category.IsGroup)
            .ToDictionary(category => (category.Kind, category.Name), category => category.IconKey);
        var points = BuildPoints(period, from, dataTo, entries, iconsByCategory);

        return new EntryStatsDto(
            period,
            anchorDate,
            from,
            to,
            coveredDays,
            Round(income),
            Round(expense),
            CalculateDailyAverage(period, income, coveredDays),
            CalculateDailyAverage(period, expense, coveredDays),
            points,
            BuildCategories(entries, LedgerEntryKind.Income, income),
            BuildCategories(entries, LedgerEntryKind.Expense, expense),
            note,
            BuildCategories(entries, LedgerEntryKind.Income, income, GroupName),
            BuildCategories(entries, LedgerEntryKind.Expense, expense, GroupName));

        string GroupName(LedgerEntry entry) => groupsByCategory.GetValueOrDefault((entry.Kind, entry.Category), "未归类");
    }

    private static IReadOnlyList<EntryStatsPointDto> BuildPoints(
        EntryPeriod period,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<LedgerEntry> entries,
        IReadOnlyDictionary<(LedgerEntryKind Kind, string Name), string?> iconsByCategory)
    {
        if (to < from)
        {
            return [];
        }

        var points = new List<EntryStatsPointDto>();

        if (period == EntryPeriod.Day)
        {
            var pointEntries = entries.Where(entry => entry.BusinessDate == from).ToList();
            points.Add(new EntryStatsPointDto(
                from,
                $"{from.Month}月{from.Day}日",
                Round(pointEntries.Where(entry => entry.Kind == LedgerEntryKind.Income).Sum(entry => entry.Amount)),
                Round(pointEntries.Where(entry => entry.Kind == LedgerEntryKind.Expense).Sum(entry => entry.Amount))));
            return points;
        }

        if (period == EntryPeriod.Month)
        {
            var entriesByDate = entries.ToLookup(entry => entry.BusinessDate);

            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var dayEntries = entriesByDate[date];
                var income = 0m;
                var expense = 0m;
                LedgerEntry? representative = null;

                foreach (var entry in dayEntries)
                {
                    if (entry.Kind == LedgerEntryKind.Income)
                    {
                        income += entry.Amount;
                    }
                    else
                    {
                        expense += entry.Amount;
                    }

                    if (representative is null
                        || entry.Amount > representative.Amount
                        || (entry.Amount == representative.Amount && entry.Id.CompareTo(representative.Id) < 0))
                    {
                        representative = entry;
                    }
                }

                points.Add(new EntryStatsPointDto(
                    date,
                    $"{date.Day}日",
                    Round(income),
                    Round(expense),
                    representative?.Category,
                    representative is null ? null : iconsByCategory.GetValueOrDefault((representative.Kind, representative.Category))));
            }

            return points;
        }

        var entriesByMonth = entries
            .Where(entry => entry.BusinessDate.Year == from.Year)
            .ToLookup(entry => entry.BusinessDate.Month);

        for (var month = 1; month <= to.Month; month++)
        {
            var monthStart = new DateOnly(from.Year, month, 1);
            var income = 0m;
            var expense = 0m;

            foreach (var entry in entriesByMonth[month])
            {
                if (entry.Kind == LedgerEntryKind.Income)
                {
                    income += entry.Amount;
                }
                else
                {
                    expense += entry.Amount;
                }
            }

            points.Add(new EntryStatsPointDto(
                monthStart,
                $"{month}月",
                Round(income),
                Round(expense)));
        }

        return points;
    }

    private static IReadOnlyList<EntryStatsCategoryDto> BuildCategories(
        IReadOnlyList<LedgerEntry> entries,
        LedgerEntryKind kind,
        decimal total,
        Func<LedgerEntry, string>? categoryName = null)
    {
        return entries
            .Where(entry => entry.Kind == kind)
            .GroupBy(entry => categoryName?.Invoke(entry) ?? entry.Category)
            .Select(group => new EntryStatsCategoryDto(
                group.Key,
                Round(group.Sum(entry => entry.Amount)),
                group.Count(),
                total == 0m ? 0m : Round(group.Sum(entry => entry.Amount) / total * 100m)))
            .OrderByDescending(category => category.Amount)
            .ThenBy(category => category.Category)
            .ToList();
    }

    private static int CalculateCoveredDays(EntryPeriod period, DateOnly from, DateOnly to)
    {
        if (from > to)
        {
            return 0;
        }

        if (period == EntryPeriod.Day)
        {
            return 1;
        }

        return to.DayNumber - from.DayNumber + 1;
    }

    private static decimal CalculateDailyAverage(EntryPeriod period, decimal amount, int coveredDays)
    {
        if (period == EntryPeriod.Day)
        {
            return Round(amount);
        }

        return coveredDays <= 0 ? 0m : Round(amount / coveredDays);
    }

    private static string? BuildNote(DateOnly from, DateOnly dataFrom, DateOnly dataTo, DateOnly ledgerStart)
    {
        if (dataFrom > dataTo)
        {
            return "该周期尚未开始。";
        }

        return ledgerStart > from
            ? $"账本自 {ledgerStart:yyyy-MM-dd} 起，周期前段未覆盖。"
            : null;
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 2);
    }
}

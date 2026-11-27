using LifeUp.Application.Ledgers;
using LifeUp.Domain.Assets;
using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Capital;

public sealed record CapitalResolvedInputs(
    CapitalEffectiveInputs Effective,
    int ExpenseWindowMonths,
    decimal StandardMonthlyExpense,
    string StandardExpenseSource,
    decimal LeanMonthlyExpense,
    string LeanExpenseSource,
    IReadOnlyList<FireExpenseCategoryDto> Categories,
    decimal ActualMonthExpense);

public sealed class CapitalDefaultResolver
{
    private const string UserSource = "user";
    private const string HistorySource = "history";
    private const string DefaultSource = "default";
    private const string SalarySource = "salary";

    private static readonly HashSet<string> s_rigidCategories = new(StringComparer.Ordinal)
    {
        "房租", "订阅", "餐饮", "日用", "交通", "居家", "服饰", "通讯", "医疗"
    };

    private readonly ILedgerRepository _ledgerRepository;
    private readonly BusinessDateProvider _dates;

    public CapitalDefaultResolver(
        ILedgerRepository ledgerRepository,
        BusinessDateProvider dates)
    {
        _ledgerRepository = ledgerRepository;
        _dates = dates;
    }

    public async Task<CapitalEffectiveInputs> ResolveAsync(
        Ledger ledger,
        CapitalGoal? goal,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveWithExpensesAsync(ledger, goal, cancellationToken);
        return resolved.Effective;
    }

    public async Task<CapitalResolvedInputs> ResolveWithExpensesAsync(
        Ledger ledger,
        CapitalGoal? goal,
        CancellationToken cancellationToken)
    {
        goal = goal?.Normalize();
        var today = _dates.Today;
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var start = ledger.StartDate;
        var elapsedMonths = (today.Year - start.Year) * 12 + today.Month - start.Month;
        var windowMonths = Math.Clamp(elapsedMonths - (start.Day == 1 ? 0 : 1), 0, 12);
        var windowStart = currentMonth.AddMonths(-windowMonths);
        var data = await _ledgerRepository.GetReplayDataReadOnlyAsync(ledger.Id, cancellationToken);
        var ledgerCategories = await _ledgerRepository.GetCategoriesAsync(ledger.Id, cancellationToken);
        var categoryTotals = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var incomeTotal = 0m;
        var actualMonthExpense = 0m;

        foreach (var entry in data.Entries)
        {
            if (entry.Status != LedgerEntryStatus.Active || entry.BusinessDate < start || entry.BusinessDate > today)
            {
                continue;
            }

            var inWindow = entry.BusinessDate >= windowStart && entry.BusinessDate < currentMonth;
            if (entry.Kind == LedgerEntryKind.Income)
            {
                if (inWindow)
                {
                    incomeTotal += entry.Amount;
                }

                continue;
            }

            categoryTotals.TryAdd(entry.Category, 0m);
            if (inWindow)
            {
                categoryTotals[entry.Category] += entry.Amount;
            }
            else if (entry.BusinessDate >= currentMonth)
            {
                actualMonthExpense += entry.Amount;
            }
        }

        var providentTotal = data.Transactions
            .Where(transaction => !transaction.IsReversed
                && transaction.AssetType == AssetType.ProvidentFund
                && transaction.Kind == AssetTransactionKind.ProvidentDeposit
                && transaction.BusinessDate >= windowStart
                && transaction.BusinessDate < currentMonth)
            .Sum(transaction => transaction.Amount);
        var income = MonthlyAverage(incomeTotal, windowMonths);
        var expense = MonthlyAverage(categoryTotals.Values.Sum(), windowMonths);
        var providentDeposit = MonthlyAverage(providentTotal, windowMonths);
        var categories = BuildCategories(ledgerCategories, categoryTotals, windowMonths, goal);
        var hasHistory = windowMonths > 0;
        var historySource = hasHistory ? HistorySource : DefaultSource;
        var profile = ledger.GetSalaryProfile();
        var followsProfile = profile is not null && (goal is null || goal.UseSalaryProfile);
        var monthlyIncome = followsProfile
            ? new CapitalEffectiveValue(profile!.NetMonthlySalary, SalarySource)
            : UserOr(goal?.MonthlyIncome, income, historySource);
        var monthlyProvident = followsProfile
            ? new CapitalEffectiveValue(profile!.ProvidentMonthly, SalarySource)
            : UserOr(goal?.MonthlyProvidentDeposit, providentDeposit, historySource);
        var annualBonus = followsProfile
            ? new CapitalEffectiveValue(profile!.BonusMonths > 0m ? profile.BonusNetAmount : 0m, SalarySource)
            : UserOr(goal?.AnnualBonus, 0m, DefaultSource);
        var bonusPayoutMonth = followsProfile
            ? new CapitalEffectiveValue(profile!.BonusPayoutMonth, SalarySource)
            : UserOr(goal?.BonusPayoutMonth is int payoutMonth ? payoutMonth : (decimal?)null, 1m, DefaultSource);

        var effective = new CapitalEffectiveInputs(
            monthlyIncome,
            UserOr(goal?.MonthlyExpense, expense, historySource),
            monthlyProvident,
            UserOr(goal?.IncomeGrowthPercent, 0m, DefaultSource),
            UserOr(goal?.ExpenseGrowthPercent, 0m, DefaultSource),
            UserOr(goal?.InvestmentAnnualReturnPercent, 0m, DefaultSource),
            UserOr(goal?.ProvidentAnnualReturnPercent, 1.5m, DefaultSource),
            annualBonus,
            bonusPayoutMonth);

        var standard = UserOr(goal?.FireMonthlyExpenseOverride,
            hasHistory ? categories.Where(category => !category.IsExcluded).Sum(category => category.MonthlyAverage)
                : effective.MonthlyExpense.Value,
            historySource);
        var lean = UserOr(goal?.LeanMonthlyExpenseOverride,
            hasHistory ? categories.Where(category => category.IsLean).Sum(category => category.MonthlyAverage)
                : Round(standard.Value * 0.7m),
            historySource);
        return new CapitalResolvedInputs(
            effective, windowMonths, standard.Value, standard.Source, lean.Value, lean.Source, categories, actualMonthExpense);
    }

    private static IReadOnlyList<FireExpenseCategoryDto> BuildCategories(
        IReadOnlyList<EntryCategory> ledgerCategories,
        IReadOnlyDictionary<string, decimal> totals,
        int windowMonths,
        CapitalGoal? goal)
    {
        var expenseCategories = ledgerCategories.Where(category => category.Kind == LedgerEntryKind.Expense).ToList();
        var groups = expenseCategories.Where(category => category.IsGroup).ToDictionary(category => category.Id);
        var leaves = expenseCategories.Where(category => !category.IsGroup).ToDictionary(category => category.Name, StringComparer.Ordinal);
        var names = totals.Keys.Union(leaves.Values.Where(category => category.IsActive).Select(category => category.Name), StringComparer.Ordinal);
        var excluded = goal?.ExcludedExpenseCategories?.ToHashSet(StringComparer.Ordinal);
        var lean = goal?.LeanExpenseCategories?.ToHashSet(StringComparer.Ordinal);
        var result = new List<FireExpenseCategoryDto>();

        foreach (var name in names)
        {
            var parentId = leaves.GetValueOrDefault(name)?.ParentId;
            var groupName = parentId is Guid id ? groups.GetValueOrDefault(id)?.Name : null;
            var isDefaultLean = groupName is null ? s_rigidCategories.Contains(name) : groupName is "固定" or "必要";
            var isExcluded = excluded?.Contains(name) ?? false;
            result.Add(new FireExpenseCategoryDto(
                name,
                groupName,
                MonthlyAverage(totals.GetValueOrDefault(name), windowMonths),
                isDefaultLean,
                !isExcluded && (lean?.Contains(name) ?? isDefaultLean),
                isExcluded));
        }

        return result.OrderByDescending(category => category.MonthlyAverage)
            .ThenBy(category => category.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static decimal MonthlyAverage(decimal total, int months)
    {
        return months > 0 ? Round(total / months) : 0m;
    }

    private static CapitalEffectiveValue UserOr(decimal? userValue, decimal fallback, string fallbackSource)
    {
        return userValue is decimal value
            ? new CapitalEffectiveValue(value, UserSource)
            : new CapitalEffectiveValue(fallback, fallbackSource);
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}

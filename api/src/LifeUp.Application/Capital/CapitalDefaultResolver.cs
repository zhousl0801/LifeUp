using LifeUp.Application.Ledgers;
using LifeUp.Domain.Assets;
using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Capital;

public sealed class CapitalDefaultResolver
{
    private const string UserSource = "user";
    private const string HistorySource = "history";
    private const string DefaultSource = "default";
    private const string SalarySource = "salary";

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
        var months = GetCompleteMonths(ledger, _dates.Today);
        var hasHistory = months.Count > 0;
        var income = 0m;
        var expense = 0m;
        var providentDeposit = 0m;

        if (hasHistory)
        {
            var transactions = await _ledgerRepository.GetAllAssetTransactionsAsync(ledger.Id, cancellationToken);
            var incomeTotal = 0m;
            var expenseTotal = 0m;
            var providentTotal = 0m;

            foreach (var month in months)
            {
                var monthEnd = month.AddMonths(1).AddDays(-1);
                var totals = await _ledgerRepository.GetEntryTotalsAsync(ledger.Id, month, monthEnd, cancellationToken);
                incomeTotal += totals.Income;
                expenseTotal += totals.Expense;
                providentTotal += transactions
                    .Where(transaction => !transaction.IsReversed
                        && transaction.AssetType == AssetType.ProvidentFund
                        && transaction.Kind == AssetTransactionKind.ProvidentDeposit
                        && transaction.BusinessDate >= month
                        && transaction.BusinessDate <= monthEnd)
                    .Sum(transaction => transaction.Amount);
            }

            income = Round(incomeTotal / months.Count);
            expense = Round(expenseTotal / months.Count);
            providentDeposit = Round(providentTotal / months.Count);
        }

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

        return new CapitalEffectiveInputs(
            monthlyIncome,
            UserOr(goal?.MonthlyExpense, expense, historySource),
            monthlyProvident,
            UserOr(goal?.IncomeGrowthPercent, 0m, DefaultSource),
            UserOr(goal?.ExpenseGrowthPercent, 0m, DefaultSource),
            UserOr(goal?.InvestmentAnnualReturnPercent, 0m, DefaultSource),
            UserOr(goal?.ProvidentAnnualReturnPercent, 1.5m, DefaultSource),
            annualBonus,
            bonusPayoutMonth);
    }

    private static IReadOnlyList<DateOnly> GetCompleteMonths(Ledger ledger, DateOnly today)
    {
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var months = new List<DateOnly>();

        for (var offset = 1; offset <= 6; offset++)
        {
            var month = currentMonth.AddMonths(-offset);

            if (month >= ledger.StartDate)
            {
                months.Add(month);
            }
        }

        return months;
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

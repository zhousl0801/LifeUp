namespace LifeUp.Application.Capital;

public enum CapitalProjectionStatus
{
    Reached,
    OnTrack,
    Unreachable,
    BeyondHorizon,
    Depleted,
    NoTarget
}

public sealed record CapitalProjectionInput(
    DateOnly AsOfDate,
    decimal Cash,
    decimal Investment,
    decimal Provident,
    decimal? TargetAmount,
    decimal MonthlyIncome,
    decimal MonthlyExpense,
    decimal MonthlyProvidentDeposit,
    decimal IncomeGrowthPercent,
    decimal ExpenseGrowthPercent,
    decimal InvestmentAnnualReturnPercent,
    decimal ProvidentAnnualReturnPercent,
    decimal AnnualBonus = 0m,
    int BonusPayoutMonth = 1,
    decimal? FireMonthlyExpense = null,
    decimal? SafeWithdrawalRatePercent = null);

public sealed record CapitalProjectionPoint(
    string Month,
    decimal Capital,
    decimal NetAccumulation,
    decimal Return,
    decimal PassiveAnnualCashflow = 0m,
    decimal ExpectedAnnualExpense = 0m);

public sealed record CapitalYearRow(
    int Year,
    decimal YearEndCapital,
    decimal NetSavings,
    decimal ProvidentDeposit,
    decimal Return,
    decimal Total,
    bool IsGoalYear,
    decimal PassiveMonthlyCashflow = 0m,
    decimal FreedomPercent = 0m);

public sealed record CapitalProjectionResult(
    CapitalProjectionStatus Status,
    decimal? TargetAmount,
    decimal? Remaining,
    decimal ProgressPercent,
    string? EtaMonth,
    int? MonthsRemaining,
    decimal MonthlyNetAccumulation,
    decimal BlendedAnnualReturnPercent,
    IReadOnlyList<CapitalProjectionPoint> Points,
    IReadOnlyList<CapitalYearRow> Years,
    IReadOnlyList<string> Notes)
{
    internal IReadOnlyList<CapitalProjectionPoint> MonthlyPoints { get; init; } = [];
}

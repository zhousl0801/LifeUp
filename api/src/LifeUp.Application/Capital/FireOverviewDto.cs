namespace LifeUp.Application.Capital;

public sealed record FireOverviewDto(
    string GoalMode,
    string ActiveMode,
    decimal SafeWithdrawalRatePercent,
    decimal SavingsRatePercent,
    int ExpenseWindowMonths,
    decimal CurrentPassiveMonthlyCashflow,
    decimal CurrentPassiveAnnualCashflow,
    FireModeDto Standard,
    FireModeDto Lean,
    CoastFireDto Coast,
    IReadOnlyList<FireMilestoneDto> Milestones,
    IReadOnlyList<FireExpenseCategoryDto> Categories,
    FireInsightsDto Insights);

public sealed record FireModeDto(
    decimal MonthlyExpense,
    string ExpenseSource,
    decimal AnnualExpense,
    decimal TargetCapital,
    decimal MonthlyCashflowGap,
    decimal ProgressPercent,
    CapitalProjectionStatus Status,
    string? EtaMonth,
    int? MonthsRemaining);

public sealed record CoastFireDto(
    int TargetYear,
    string TargetYearSource,
    bool Achievable,
    bool Reached,
    decimal? ThresholdCapital,
    decimal ProgressPercent,
    string? EtaMonth,
    int? MonthsRemaining,
    string? Note);

public sealed record FireMilestoneDto(
    string Key,
    string Label,
    decimal ThresholdAmount,
    decimal ProgressPercent,
    bool Reached,
    string? Date,
    string Description);

public sealed record FireExpenseCategoryDto(
    string Name,
    string? GroupName,
    decimal MonthlyAverage,
    bool IsDefaultLean,
    bool IsLean,
    bool IsExcluded);

public sealed record FireInsightsDto(
    string Stage,
    string Headline,
    IReadOnlyList<string> CoveredCategories,
    IReadOnlyList<FireSuggestionDto> Suggestions);

public sealed record FireSuggestionDto(string Badge, string Text);

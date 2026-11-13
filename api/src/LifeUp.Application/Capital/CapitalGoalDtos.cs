using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Capital;

public sealed record CapitalEffectiveValue(decimal Value, string Source);

public sealed record CapitalEffectiveInputs(
    CapitalEffectiveValue MonthlyIncome,
    CapitalEffectiveValue MonthlyExpense,
    CapitalEffectiveValue MonthlyProvidentDeposit,
    CapitalEffectiveValue IncomeGrowthPercent,
    CapitalEffectiveValue ExpenseGrowthPercent,
    CapitalEffectiveValue InvestmentAnnualReturnPercent,
    CapitalEffectiveValue ProvidentAnnualReturnPercent,
    CapitalEffectiveValue AnnualBonus,
    CapitalEffectiveValue BonusPayoutMonth);

public sealed record CapitalGoalSettingsDto(
    decimal? TargetAmount,
    decimal? MonthlyIncome,
    decimal? MonthlyExpense,
    decimal? MonthlyProvidentDeposit,
    decimal? IncomeGrowthPercent,
    decimal? ExpenseGrowthPercent,
    decimal? InvestmentAnnualReturnPercent,
    decimal? ProvidentAnnualReturnPercent,
    decimal? AnnualBonus,
    int? BonusPayoutMonth,
    bool UseSalaryProfile)
{
    public static CapitalGoalSettingsDto From(CapitalGoal? goal)
    {
        return goal is null
            ? new CapitalGoalSettingsDto(null, null, null, null, null, null, null, null, null, null, false)
            : new CapitalGoalSettingsDto(
                goal.TargetAmount,
                goal.MonthlyIncome,
                goal.MonthlyExpense,
                goal.MonthlyProvidentDeposit,
                goal.IncomeGrowthPercent,
                goal.ExpenseGrowthPercent,
                goal.InvestmentAnnualReturnPercent,
                goal.ProvidentAnnualReturnPercent,
                goal.AnnualBonus,
                goal.BonusPayoutMonth,
                goal.UseSalaryProfile);
    }
}

public sealed record CapitalCurrentDto(
    DateOnly AsOfDate,
    decimal Total,
    decimal Cash,
    decimal Investment,
    decimal Provident,
    bool CoverageComplete,
    string? Note);

public sealed record CapitalProjectionPointDto(
    string Month,
    decimal Capital,
    decimal NetAccumulation,
    decimal Return);

public sealed record CapitalYearRowDto(
    int Year,
    decimal YearEndCapital,
    decimal NetSavings,
    decimal ProvidentDeposit,
    decimal Return,
    decimal Total,
    bool IsGoalYear);

public sealed record CapitalMilestoneDto(
    decimal ThresholdAmount,
    decimal ProgressPercent,
    DateOnly Date,
    string Title);

public sealed record CapitalProjectionDto(
    CapitalProjectionStatus Status,
    decimal? TargetAmount,
    decimal? Remaining,
    decimal ProgressPercent,
    string? EtaMonth,
    int? MonthsRemaining,
    decimal MonthlyNetAccumulation,
    decimal BlendedAnnualReturnPercent,
    IReadOnlyList<CapitalProjectionPointDto> Points,
    IReadOnlyList<CapitalYearRowDto> Years,
    IReadOnlyList<CapitalMilestoneDto> Milestones,
    IReadOnlyList<string> Notes);

public sealed record CapitalActualMonthDto(
    string Month,
    decimal? ComparableChange,
    decimal? NetDeposit,
    decimal? MarketValueChange,
    decimal ExpectedNetDeposit,
    bool IsComplete,
    string? Note);

public sealed record CapitalOverviewDto(
    CapitalGoalSettingsDto Settings,
    CapitalEffectiveInputs Effective,
    CapitalCurrentDto Current,
    CapitalProjectionDto Projection,
    CapitalActualMonthDto ActualMonth,
    bool HasSalaryProfile);

public sealed record UpdateCapitalGoalRequest(
    decimal? TargetAmount,
    decimal? MonthlyIncome,
    decimal? MonthlyExpense,
    decimal? MonthlyProvidentDeposit,
    decimal? IncomeGrowthPercent,
    decimal? ExpenseGrowthPercent,
    decimal? InvestmentAnnualReturnPercent,
    decimal? ProvidentAnnualReturnPercent,
    decimal? AnnualBonus = null,
    int? BonusPayoutMonth = null,
    bool UseSalaryProfile = false);

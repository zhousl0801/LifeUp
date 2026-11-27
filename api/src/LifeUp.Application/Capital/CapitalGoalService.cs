using LifeUp.Application.Ledgers;
using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Capital;

public sealed class CapitalGoalService
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly LedgerService _ledgerService;
    private readonly TrendService _trendService;
    private readonly MonthChangeService _monthChangeService;
    private readonly CapitalDefaultResolver _defaultResolver;
    private readonly CapitalProjectionCalculator _calculator;
    private readonly BusinessDateProvider _dates;

    public CapitalGoalService(
        ILedgerRepository ledgerRepository,
        LedgerService ledgerService,
        TrendService trendService,
        MonthChangeService monthChangeService,
        CapitalDefaultResolver defaultResolver,
        CapitalProjectionCalculator calculator,
        BusinessDateProvider dates)
    {
        _ledgerRepository = ledgerRepository;
        _ledgerService = ledgerService;
        _trendService = trendService;
        _monthChangeService = monthChangeService;
        _defaultResolver = defaultResolver;
        _calculator = calculator;
        _dates = dates;
    }

    public async Task<CapitalOverviewDto?> GetAsync(CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetAsync(cancellationToken);

        if (ledger is null)
        {
            return null;
        }

        return await BuildOverviewAsync(ledger, ledger.GetCapitalGoal(), cancellationToken);
    }

    public async Task<CapitalOverviewDto?> SaveAsync(CapitalGoal? goal, CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetForUpdateAsync(cancellationToken);

        if (ledger is null)
        {
            return null;
        }

        if (ledger.SetCapitalGoal(goal, _dates.UtcNow))
        {
            await _ledgerRepository.SaveChangesAsync(cancellationToken);
        }

        return await BuildOverviewAsync(ledger, goal, cancellationToken);
    }

    public async Task<CapitalOverviewDto?> PreviewAsync(CapitalGoal? goal, CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetAsync(cancellationToken);

        if (ledger is null)
        {
            return null;
        }

        return await BuildOverviewAsync(ledger, goal, cancellationToken);
    }

    private async Task<CapitalOverviewDto> BuildOverviewAsync(
        Ledger ledger,
        CapitalGoal? goal,
        CancellationToken cancellationToken)
    {
        goal = goal?.Normalize();
        var overview = await _ledgerService.GetOverviewAsync(cancellationToken)
            ?? throw new InvalidOperationException("账本概览不可用。");
        var resolved = await _defaultResolver.ResolveWithExpensesAsync(ledger, goal, cancellationToken);
        var effective = resolved.Effective;
        var cash = overview.Cash.Amount ?? 0m;
        var investment = (overview.Fund.Amount ?? 0m)
            + (overview.Gold.Amount ?? 0m)
            + (overview.Stock.Amount ?? 0m);
        var provident = overview.ProvidentFund.Amount ?? 0m;
        var current = new CapitalCurrentDto(
            overview.AsOfDate,
            overview.TotalAssets,
            cash,
            investment,
            provident,
            overview.CoverageComplete,
            overview.CoverageComplete ? null : "部分资产未录入或估值不足，按已记录资产推演。");
        var goalMode = goal?.GoalMode ?? "fire";
        var activeMode = goal?.FireMode ?? "standard";
        var isLean = activeMode == "lean";
        var withdrawalRatePercent = goal?.SafeWithdrawalRatePercent ?? CapitalProjectionCalculator.DefaultSafeWithdrawalRatePercent;
        var standardInput = new CapitalProjectionInput(
            _dates.Today,
            cash,
            investment,
            provident,
            goal?.TargetAmount,
            effective.MonthlyIncome.Value,
            effective.MonthlyExpense.Value,
            effective.MonthlyProvidentDeposit.Value,
            effective.IncomeGrowthPercent.Value,
            effective.ExpenseGrowthPercent.Value,
            effective.InvestmentAnnualReturnPercent.Value,
            effective.ProvidentAnnualReturnPercent.Value,
            effective.AnnualBonus.Value,
            (int)effective.BonusPayoutMonth.Value,
            resolved.StandardMonthlyExpense,
            withdrawalRatePercent);
        var leanInput = standardInput with { FireMonthlyExpense = resolved.LeanMonthlyExpense };
        var activeInput = isLean ? leanInput : standardInput;
        var standardProjection = _calculator.CalculateFire(standardInput);
        var leanProjection = _calculator.CalculateFire(leanInput);
        var activeProjection = isLean ? leanProjection : standardProjection;
        var coast = CapitalProjectionCalculator.CalculateCoast(activeInput, activeProjection, goal?.CoastTargetYear);
        var annualPassive = current.Total * withdrawalRatePercent / 100m;
        var monthlyPassive = Round(annualPassive / 12m);
        var standard = BuildFireMode(resolved.StandardMonthlyExpense, resolved.StandardExpenseSource, monthlyPassive, standardProjection);
        var lean = BuildFireMode(resolved.LeanMonthlyExpense, resolved.LeanExpenseSource, monthlyPassive, leanProjection);
        var hasCustomTarget = goalMode == "custom" && goal?.TargetAmount is not null;
        var trend = activeProjection.TargetAmount > 0m || hasCustomTarget
            ? await _trendService.GetAsync(TrendRange.All, cancellationToken)
            : null;
        var trendPoints = trend?.Points ?? [];
        var fireMilestones = FireMilestoneBuilder.Build(
            current, trendPoints, activeMode, isLean ? lean : standard, lean, coast, activeProjection);
        var annualInflow = 12m * (effective.MonthlyIncome.Value + effective.MonthlyProvidentDeposit.Value)
            + effective.AnnualBonus.Value;
        var savingsRatePercent = annualInflow > 0m
            ? CapitalProjectionCalculator.Round1((annualInflow - 12m * effective.MonthlyExpense.Value) / annualInflow * 100m)
            : 0m;
        var insights = FireInsightBuilder.Build(
            _calculator, activeInput, current, activeMode, standard, lean, coast,
            savingsRatePercent, monthlyPassive, resolved.Categories, fireMilestones);
        var fire = new FireOverviewDto(
            goalMode, activeMode, withdrawalRatePercent, savingsRatePercent, resolved.ExpenseWindowMonths,
            monthlyPassive, Round(annualPassive), standard, lean, coast, fireMilestones, resolved.Categories, insights);

        var projection = goalMode == "custom" ? _calculator.Calculate(activeInput) : activeProjection;
        var milestones = hasCustomTarget && goal?.TargetAmount is decimal target
            ? BuildCustomMilestones(target, trendPoints, current.CoverageComplete)
            : [];
        var actual = await BuildActualMonthAsync(effective, resolved.ActualMonthExpense, cancellationToken);
        var projectionDto = new CapitalProjectionDto(
            projection.Status,
            projection.TargetAmount,
            projection.Remaining,
            projection.ProgressPercent,
            projection.EtaMonth,
            projection.MonthsRemaining,
            projection.MonthlyNetAccumulation,
            projection.BlendedAnnualReturnPercent,
            projection.Points
                .Select(point => new CapitalProjectionPointDto(
                    point.Month,
                    point.Capital,
                    point.NetAccumulation,
                    point.Return,
                    point.PassiveAnnualCashflow,
                    point.ExpectedAnnualExpense))
                .ToList(),
            projection.Years
                .Select(year => new CapitalYearRowDto(
                    year.Year,
                    year.YearEndCapital,
                    year.NetSavings,
                    year.ProvidentDeposit,
                    year.Return,
                    year.Total,
                    year.IsGoalYear,
                    year.PassiveMonthlyCashflow,
                    year.FreedomPercent))
                .ToList(),
            milestones,
            projection.Notes);

        return new CapitalOverviewDto(
            CapitalGoalSettingsDto.From(goal),
            effective,
            current,
            projectionDto,
            actual,
            ledger.GetSalaryProfile() is not null,
            fire);
    }

    private static FireModeDto BuildFireMode(
        decimal monthlyExpense,
        string expenseSource,
        decimal monthlyPassive,
        CapitalProjectionResult projection)
    {
        return new FireModeDto(
            monthlyExpense, expenseSource, monthlyExpense * 12m, projection.TargetAmount ?? 0m,
            Math.Max(0m, Round(monthlyExpense - monthlyPassive)), projection.ProgressPercent,
            projection.Status, projection.EtaMonth, projection.MonthsRemaining);
    }

    private static IReadOnlyList<CapitalMilestoneDto> BuildCustomMilestones(
        decimal target,
        IReadOnlyList<AssetTrendPointDto> trend,
        bool coverageComplete)
    {
        return CapitalMilestoneBuilder.Build(target, trend, coverageComplete)
            .Select(milestone => new CapitalMilestoneDto(
                milestone.ThresholdAmount,
                milestone.ProgressPercent,
                milestone.Date,
                milestone.Title))
            .ToList();
    }

    private async Task<CapitalActualMonthDto> BuildActualMonthAsync(
        CapitalEffectiveInputs effective,
        decimal actualExpense,
        CancellationToken cancellationToken)
    {
        var change = await _monthChangeService.GetAsync(ChangeScope.Month, cancellationToken);
        var isComplete = change?.HasComparableData ?? false;
        var bonusMonth = (int)effective.BonusPayoutMonth.Value;
        var includesBonus = effective.AnnualBonus.Value > 0m && _dates.Today.Month == bonusMonth;
        var expectedNetDeposit = Round(
            effective.MonthlyIncome.Value
            - effective.MonthlyExpense.Value
            + effective.MonthlyProvidentDeposit.Value
            + (includesBonus ? effective.AnnualBonus.Value : 0m));
        var note = isComplete ? null : change?.Notes.FirstOrDefault();

        if (includesBonus)
        {
            note = string.IsNullOrEmpty(note)
                ? "本月为年终奖发放月，差额可能来自到账时间。"
                : $"{note} 本月为年终奖发放月，差额可能来自到账时间。";
        }

        return new CapitalActualMonthDto(
            _dates.Today.ToString("yyyy-MM"),
            change?.ComparableChange,
            change?.NetDeposit,
            change?.MarketValueChange,
            expectedNetDeposit,
            isComplete,
            note,
            actualExpense);
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}

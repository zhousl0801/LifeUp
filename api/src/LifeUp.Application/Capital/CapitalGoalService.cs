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
        var overview = await _ledgerService.GetOverviewAsync(cancellationToken)
            ?? throw new InvalidOperationException("账本概览不可用。");
        var effective = await _defaultResolver.ResolveAsync(ledger, goal, cancellationToken);
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
        var projection = _calculator.Calculate(new CapitalProjectionInput(
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
            effective.ProvidentAnnualReturnPercent.Value));
        var milestones = await BuildMilestonesAsync(goal?.TargetAmount, overview.CoverageComplete, cancellationToken);
        var actual = await BuildActualMonthAsync(effective, cancellationToken);
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
                    point.Return))
                .ToList(),
            projection.Years
                .Select(year => new CapitalYearRowDto(
                    year.Year,
                    year.YearEndCapital,
                    year.NetSavings,
                    year.ProvidentDeposit,
                    year.Return,
                    year.Total,
                    year.IsGoalYear))
                .ToList(),
            milestones,
            projection.Notes);

        return new CapitalOverviewDto(
            CapitalGoalSettingsDto.From(goal),
            effective,
            current,
            projectionDto,
            actual);
    }

    private async Task<IReadOnlyList<CapitalMilestoneDto>> BuildMilestonesAsync(
        decimal? targetAmount,
        bool coverageComplete,
        CancellationToken cancellationToken)
    {
        if (targetAmount is not decimal target)
        {
            return [];
        }

        var trend = await _trendService.GetAsync(TrendRange.All, cancellationToken);

        if (trend is null)
        {
            return [];
        }

        return CapitalMilestoneBuilder.Build(target, trend.Points, coverageComplete)
            .Select(milestone => new CapitalMilestoneDto(
                milestone.ThresholdAmount,
                milestone.ProgressPercent,
                milestone.Date,
                milestone.Title))
            .ToList();
    }

    private async Task<CapitalActualMonthDto> BuildActualMonthAsync(
        CapitalEffectiveInputs effective,
        CancellationToken cancellationToken)
    {
        var change = await _monthChangeService.GetAsync(ChangeScope.Month, cancellationToken);
        var isComplete = change?.HasComparableData ?? false;
        var expectedNetDeposit = Round(
            effective.MonthlyIncome.Value
            - effective.MonthlyExpense.Value
            + effective.MonthlyProvidentDeposit.Value);

        return new CapitalActualMonthDto(
            _dates.Today.ToString("yyyy-MM"),
            change?.ComparableChange,
            change?.NetDeposit,
            change?.MarketValueChange,
            expectedNetDeposit,
            isComplete,
            isComplete ? null : change?.Notes.FirstOrDefault());
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}

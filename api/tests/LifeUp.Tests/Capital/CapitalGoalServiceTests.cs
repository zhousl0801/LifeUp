using LifeUp.Application.Capital;
using LifeUp.Application.Ledgers;
using LifeUp.Domain.Ledgers;
using LifeUp.Tests.Ledgers;

namespace LifeUp.Tests.Capital;

public sealed class CapitalGoalServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_WithoutGoal_ReturnsNoTargetAndDefaults()
    {
        var (_, service, _) = await CreateAsync(new DateOnly(2026, 11, 1), 100_000m);

        var overview = await service.GetAsync(CancellationToken.None);

        Assert.NotNull(overview);
        Assert.Null(overview.Settings.TargetAmount);
        Assert.Equal(CapitalProjectionStatus.NoTarget, overview.Projection.Status);
        Assert.Equal(100_000m, overview.Current.Total);
        Assert.False(overview.Current.CoverageComplete);
        Assert.Equal(0m, overview.Effective.MonthlyIncome.Value);
        Assert.Equal("default", overview.Effective.MonthlyIncome.Source);
        Assert.NotEmpty(overview.Projection.Points);
        Assert.True(overview.Projection.Years.Count >= 10);
    }

    [Fact]
    public async Task SaveAsync_PersistsGoalAndEffectiveSourcesBecomeUser()
    {
        var (repository, service, _) = await CreateAsync(new DateOnly(2026, 11, 1), 100_000m);
        var goal = new CapitalGoal(1_000_000m, 12_000m, 7_200m, 2_000m, 3m, 2m, 5m, 1.5m);

        var saved = await service.SaveAsync(goal, CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(1_000_000m, saved.Settings.TargetAmount);
        Assert.Equal(12_000m, saved.Effective.MonthlyIncome.Value);
        Assert.Equal("user", saved.Effective.MonthlyIncome.Source);
        Assert.Equal(CapitalProjectionStatus.OnTrack, saved.Projection.Status);

        var readBack = await service.GetAsync(CancellationToken.None);
        Assert.NotNull(readBack);
        Assert.Equal(1_000_000m, readBack.Settings.TargetAmount);
        Assert.Equal(goal, (await repository.GetAsync(CancellationToken.None))!.GetCapitalGoal());
    }

    [Fact]
    public async Task PreviewAsync_DoesNotPersistDraft()
    {
        var (repository, service, _) = await CreateAsync(new DateOnly(2026, 11, 1), 100_000m);
        var savedGoal = new CapitalGoal(1_000_000m, 12_000m, 7_200m, 2_000m, 3m, 2m, 5m, 1.5m);
        await service.SaveAsync(savedGoal, CancellationToken.None);
        var draft = savedGoal with { TargetAmount = 2_000_000m };

        var preview = await service.PreviewAsync(draft, CancellationToken.None);

        Assert.NotNull(preview);
        Assert.Equal(2_000_000m, preview.Settings.TargetAmount);
        var ledger = await repository.GetAsync(CancellationToken.None);
        Assert.Equal(savedGoal, ledger!.GetCapitalGoal());
    }

    [Fact]
    public async Task GetAsync_IncompleteCoverage_UsesRecordedAssetsAndNote()
    {
        var (_, service, _) = await CreateAsync(new DateOnly(2026, 11, 1), 100_000m);

        var overview = await service.GetAsync(CancellationToken.None);

        Assert.NotNull(overview);
        Assert.False(overview.Current.CoverageComplete);
        Assert.Contains("部分资产", overview.Current.Note);
        Assert.Equal(100_000m, overview.Current.Total);
    }

    [Fact]
    public async Task GetAsync_WithGoal_ReturnsTenPercentMilestones()
    {
        var (_, service, _) = await CreateAsync(new DateOnly(2026, 11, 1), 376_000m);
        await service.SaveAsync(
            new CapitalGoal(1_000_000m, 12_000m, 7_200m, 2_000m, 3m, 2m, 5m, 1.5m),
            CancellationToken.None);

        var overview = await service.GetAsync(CancellationToken.None);

        Assert.NotNull(overview);
        Assert.Equal(37.6m, overview.Projection.ProgressPercent);
        Assert.Equal([10m, 20m, 30m], overview.Projection.Milestones.Select(item => item.ProgressPercent));
        Assert.All(overview.Projection.Milestones, item => Assert.Equal(new DateOnly(2026, 11, 1), item.Date));
    }

    private static async Task<(FakeLedgerRepository Repository, CapitalGoalService Service, Ledger Ledger)> CreateAsync(
        DateOnly startDate,
        decimal cashBalance)
    {
        var repository = new FakeLedgerRepository();
        var dates = new BusinessDateProvider(new FixedTimeProvider(Now));
        var ledgerService = new LedgerService(repository, dates);
        var initialized = await ledgerService.InitializeAsync(
            new InitializeLedgerRequest(startDate, cashBalance),
            CancellationToken.None);

        Assert.Equal(InitializeLedgerStatus.Created, initialized.Status);

        var service = new CapitalGoalService(
            repository,
            ledgerService,
            new TrendService(repository, dates),
            new MonthChangeService(repository, dates),
            new CapitalDefaultResolver(repository, dates),
            new CapitalProjectionCalculator(),
            dates);
        var ledger = await repository.GetAsync(CancellationToken.None);

        return (repository, service, ledger!);
    }
}

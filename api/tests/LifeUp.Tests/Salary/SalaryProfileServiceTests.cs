using LifeUp.Application.Assets;
using LifeUp.Application.Ledgers;
using LifeUp.Application.Plans;
using LifeUp.Application.Salary;
using LifeUp.Domain.Ledgers;
using LifeUp.Domain.Plans;
using LifeUp.Infrastructure.Persistence;
using LifeUp.Tests.Ledgers;
using Microsoft.EntityFrameworkCore;

namespace LifeUp.Tests.Salary;

public sealed class SalaryProfileServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 13);
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveAsync_CreatesThreePlansAndNextDates()
    {
        await using var context = await CreateAsync();

        var result = await context.Service.SaveAsync(NewRequest(payDay: 15), CancellationToken.None);

        Assert.Equal(AssetOperationStatus.Created, result.Status);
        var plans = await context.PlanRepository.GetPlansAsync(context.LedgerId, CancellationToken.None);
        var salary = Assert.Single(plans, plan => plan.Type == AutoPlanType.SalaryIncome);
        var provident = Assert.Single(plans, plan => plan.Type == AutoPlanType.ProvidentDeposit);
        var bonus = Assert.Single(plans, plan => plan.Type == AutoPlanType.BonusIncome);

        Assert.Equal(12_000m, salary.Amount);
        Assert.Equal(AutoPlanCycle.Monthly, salary.Cycle);
        Assert.Equal(new DateOnly(2026, 11, 15), salary.StartDate);

        Assert.Equal(4_800m, provident.Amount);
        Assert.Equal(AutoPlanCycle.Monthly, provident.Cycle);
        Assert.Equal(new DateOnly(2026, 11, 15), provident.StartDate);

        Assert.Equal(60_000m, bonus.Amount);
        Assert.Equal(AutoPlanCycle.Yearly, bonus.Cycle);
        Assert.Equal(1, bonus.MonthOfYear);
        Assert.Equal(new DateOnly(2027, 1, 15), bonus.StartDate);
    }

    [Fact]
    public async Task SaveAsync_PayDayInPast_StartsNextMonth()
    {
        await using var context = await CreateAsync();

        await context.Service.SaveAsync(NewRequest(payDay: 10), CancellationToken.None);

        var plans = await context.PlanRepository.GetPlansAsync(context.LedgerId, CancellationToken.None);
        var salary = Assert.Single(plans, plan => plan.Type == AutoPlanType.SalaryIncome);
        Assert.Equal(new DateOnly(2026, 12, 10), salary.StartDate);
    }

    [Fact]
    public async Task SaveAsync_Update_KeepsPlanIdsAndStartDates()
    {
        await using var context = await CreateAsync();
        await context.Service.SaveAsync(NewRequest(payDay: 15), CancellationToken.None);
        var before = await context.PlanRepository.GetPlansAsync(context.LedgerId, CancellationToken.None);
        var salaryBefore = Assert.Single(before, plan => plan.Type == AutoPlanType.SalaryIncome);
        var bonusBefore = Assert.Single(before, plan => plan.Type == AutoPlanType.BonusIncome);

        await context.Service.SaveAsync(
            NewRequest(payDay: 15) with { NetMonthlySalary = 13_000m, BonusNetAmount = 66_000m },
            CancellationToken.None);

        var after = await context.PlanRepository.GetPlansAsync(context.LedgerId, CancellationToken.None);
        var salaryAfter = Assert.Single(after, plan => plan.Type == AutoPlanType.SalaryIncome);
        var bonusAfter = Assert.Single(after, plan => plan.Type == AutoPlanType.BonusIncome);

        Assert.Equal(salaryBefore.Id, salaryAfter.Id);
        Assert.Equal(salaryBefore.StartDate, salaryAfter.StartDate);
        Assert.Equal(13_000m, salaryAfter.Amount);
        Assert.Equal(bonusBefore.Id, bonusAfter.Id);
        Assert.Equal(66_000m, bonusAfter.Amount);
        Assert.Equal(3, after.Count);
    }

    [Fact]
    public async Task SaveAsync_PayDayChange_AdvancesGenerateFrom()
    {
        await using var context = await CreateAsync();
        await context.Service.SaveAsync(NewRequest(payDay: 15), CancellationToken.None);

        await context.Service.SaveAsync(NewRequest(payDay: 20), CancellationToken.None);

        var plans = await context.PlanRepository.GetPlansAsync(context.LedgerId, CancellationToken.None);
        var salary = Assert.Single(plans, plan => plan.Type == AutoPlanType.SalaryIncome);
        Assert.Equal(20, salary.DayOfMonth);
        Assert.True(salary.GenerateFrom > Today);
    }

    [Fact]
    public async Task SaveAsync_PayDayChange_FromPastGenerateFrom_AdvancesPastToday()
    {
        await using var context = await CreateAsync();
        var past = AutoPlan.Create(
            context.LedgerId,
            AutoPlanType.SalaryIncome,
            "工资到账",
            12_000m,
            AutoPlanCycle.Monthly,
            15,
            null,
            new TimeOnly(9, 0),
            Today.AddDays(-10),
            null,
            Now);
        await context.PlanRepository.AddPlanAsync(past, CancellationToken.None);
        await context.PlanRepository.SaveChangesAsync(CancellationToken.None);

        await context.Service.SaveAsync(NewRequest(payDay: 20), CancellationToken.None);

        var salary = await FindAsync(context, AutoPlanType.SalaryIncome);
        Assert.Equal(20, salary.DayOfMonth);
        Assert.True(salary.GenerateFrom > Today);
    }

    [Fact]
    public async Task SaveAsync_ZeroProvident_PausesAndResumes()
    {
        await using var context = await CreateAsync();
        await context.Service.SaveAsync(NewRequest(payDay: 15), CancellationToken.None);

        await context.Service.SaveAsync(
            NewRequest(payDay: 15) with { ProvidentMonthly = 0m },
            CancellationToken.None);

        var paused = await FindAsync(context, AutoPlanType.ProvidentDeposit);
        Assert.False(paused.IsActive);

        await context.Service.SaveAsync(NewRequest(payDay: 15), CancellationToken.None);

        var resumed = await FindAsync(context, AutoPlanType.ProvidentDeposit);
        Assert.True(resumed.IsActive);
        Assert.Equal(4_800m, resumed.Amount);
    }

    [Fact]
    public async Task SaveAsync_ZeroBonusMonths_PausesAndResumes()
    {
        await using var context = await CreateAsync();
        await context.Service.SaveAsync(NewRequest(payDay: 15), CancellationToken.None);

        await context.Service.SaveAsync(
            NewRequest(payDay: 15) with { BonusMonths = 0m, BonusNetAmount = 0m },
            CancellationToken.None);

        var paused = await FindAsync(context, AutoPlanType.BonusIncome);
        Assert.False(paused.IsActive);

        await context.Service.SaveAsync(NewRequest(payDay: 15), CancellationToken.None);

        var resumed = await FindAsync(context, AutoPlanType.BonusIncome);
        Assert.True(resumed.IsActive);
        Assert.Equal(60_000m, resumed.Amount);
        Assert.Equal(1, resumed.MonthOfYear);
    }

    [Fact]
    public async Task SaveAsync_FirstProfile_SetsUseSalaryProfile()
    {
        await using var context = await CreateAsync();
        await context.SetGoalAsync(new CapitalGoal(1_000_000m, null, null, null, null, null, null, null));

        await context.Service.SaveAsync(NewRequest(payDay: 15), CancellationToken.None);

        var ledger = await context.Repository.GetAsync(CancellationToken.None);
        Assert.NotNull(ledger!.GetCapitalGoal());
        Assert.True(ledger.GetCapitalGoal()!.UseSalaryProfile);
    }

    [Fact]
    public async Task DisableAsync_RemovesProfileAndFillsGoal()
    {
        await using var context = await CreateAsync();
        await context.SetGoalAsync(new CapitalGoal(1_000_000m, null, null, null, null, null, null, null));
        await context.Service.SaveAsync(NewRequest(payDay: 15), CancellationToken.None);

        var result = await context.Service.DisableAsync(
            new SalaryProfileActionRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(AssetOperationStatus.Success, result.Status);
        var ledger = await context.Repository.GetAsync(CancellationToken.None);
        Assert.Null(ledger!.GetSalaryProfile());
        var goal = ledger.GetCapitalGoal()!;
        Assert.False(goal.UseSalaryProfile);
        Assert.Equal(12_000m, goal.MonthlyIncome);
        Assert.Equal(4_800m, goal.MonthlyProvidentDeposit);
        Assert.Equal(60_000m, goal.AnnualBonus);
        Assert.Equal(1, goal.BonusPayoutMonth);

        var plans = await context.PlanRepository.GetPlansAsync(context.LedgerId, CancellationToken.None);
        Assert.Equal(3, plans.Count);
    }

    [Fact]
    public async Task SaveAsync_SameRequestId_DoesNotCreateDuplicatePlans()
    {
        await using var context = await CreateAsync();
        var request = NewRequest(payDay: 15);

        var first = await context.Service.SaveAsync(request, CancellationToken.None);
        var second = await context.Service.SaveAsync(request, CancellationToken.None);

        Assert.Equal(AssetOperationStatus.Created, first.Status);
        Assert.Equal(AssetOperationStatus.Success, second.Status);
        var plans = await context.PlanRepository.GetPlansAsync(context.LedgerId, CancellationToken.None);
        Assert.Equal(3, plans.Count);
    }

    [Fact]
    public async Task SaveAsync_InvalidProfile_ReturnsInvalid()
    {
        await using var context = await CreateAsync();

        var result = await context.Service.SaveAsync(
            NewRequest(payDay: 15) with { NetMonthlySalary = 0m },
            CancellationToken.None);

        Assert.Equal(AssetOperationStatus.Invalid, result.Status);
        var plans = await context.PlanRepository.GetPlansAsync(context.LedgerId, CancellationToken.None);
        Assert.Empty(plans);
    }

    [Fact]
    public async Task GetAsync_WithoutLedger_ReturnsNull()
    {
        await using var db = CreateDb();
        var repository = new LedgerRepository(db);
        var service = new SalaryProfileService(
            repository,
            new PlanRepository(db),
            new BusinessDateProvider(new FixedTimeProvider(Now)));

        var result = await service.GetAsync(CancellationToken.None);

        Assert.Null(result);
    }

    private static SaveSalaryProfileRequest NewRequest(int payDay)
    {
        return new SaveSalaryProfileRequest(
            Guid.NewGuid(),
            20_000m,
            12_000m,
            4_800m,
            4m,
            60_000m,
            1,
            payDay);
    }

    private static async Task<AutoPlan> FindAsync(TestContext context, AutoPlanType type)
    {
        var plans = await context.PlanRepository.GetPlansAsync(context.LedgerId, CancellationToken.None);

        return Assert.Single(plans, plan => plan.Type == type);
    }

    private static async Task<TestContext> CreateAsync()
    {
        var db = CreateDb();
        var repository = new LedgerRepository(db);
        var planRepository = new PlanRepository(db);
        var dates = new BusinessDateProvider(new FixedTimeProvider(Now));
        var ledgerService = new LedgerService(repository, dates);
        var initialized = await ledgerService.InitializeAsync(
            new InitializeLedgerRequest(Today.AddMonths(-1), 100_000m),
            CancellationToken.None);
        Assert.Equal(InitializeLedgerStatus.Created, initialized.Status);
        var ledger = await repository.GetAsync(CancellationToken.None);
        var service = new SalaryProfileService(repository, planRepository, dates);

        return new TestContext(db, ledger!.Id, repository, planRepository, ledger, service);
    }

    private static AppDbContext CreateDb()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private sealed record TestContext(
        AppDbContext Db,
        Guid LedgerId,
        LedgerRepository Repository,
        PlanRepository PlanRepository,
        Ledger Ledger,
        SalaryProfileService Service) : IAsyncDisposable
    {
        public async Task SetGoalAsync(CapitalGoal goal)
        {
            var tracked = await Repository.GetForUpdateAsync(CancellationToken.None);
            tracked!.SetCapitalGoal(goal, Now);
            await Repository.SaveChangesAsync(CancellationToken.None);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

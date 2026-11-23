using LifeUp.Application.Ledgers;
using LifeUp.Domain.Ledgers;
using LifeUp.Tests.Ledgers;

namespace LifeUp.Tests.Ledgers;

public sealed class EntryStatsServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MonthStats_ReturnDailyPointsTotalsAveragesAndCategoryShare()
    {
        var context = await CreateAsync(new DateOnly(2026, 9, 1));
        await CreateEntryAsync(context, LedgerEntryKind.Income, "工资", 1000m, new DateOnly(2026, 10, 1));
        await CreateEntryAsync(context, LedgerEntryKind.Expense, "餐饮", 100m, new DateOnly(2026, 10, 5));
        await CreateEntryAsync(context, LedgerEntryKind.Expense, "购物", 200m, new DateOnly(2026, 10, 5));
        await CreateEntryAsync(context, LedgerEntryKind.Income, "奖金", 500m, new DateOnly(2026, 10, 10));

        var stats = await context.Stats.GetAsync(EntryPeriod.Month, null, CancellationToken.None);

        Assert.NotNull(stats);
        Assert.Equal(new DateOnly(2026, 10, 1), stats.From);
        Assert.Equal(new DateOnly(2026, 10, 31), stats.To);
        Assert.Equal(21, stats.CoveredDays);
        Assert.Equal(1500m, stats.Income);
        Assert.Equal(300m, stats.Expense);
        Assert.Equal(71.43m, stats.IncomeDailyAverage);
        Assert.Equal(14.29m, stats.ExpenseDailyAverage);
        Assert.Equal(21, stats.Points.Count);
        Assert.Equal(1000m, stats.Points[0].Income);
        var shoppingDay = stats.Points.Single(point => point.Date == new DateOnly(2026, 10, 5));
        Assert.Equal(300m, shoppingDay.Expense);

        Assert.Collection(
            stats.IncomeCategories,
            category =>
            {
                Assert.Equal("工资", category.Category);
                Assert.Equal(1000m, category.Amount);
                Assert.Equal(66.67m, category.Share);
            },
            category =>
            {
                Assert.Equal("奖金", category.Category);
                Assert.Equal(500m, category.Amount);
                Assert.Equal(33.33m, category.Share);
            });
        Assert.Collection(
            stats.ExpenseCategories,
            category =>
            {
                Assert.Equal("购物", category.Category);
                Assert.Equal(200m, category.Amount);
                Assert.Equal(66.67m, category.Share);
            },
            category =>
            {
                Assert.Equal("餐饮", category.Category);
                Assert.Equal(100m, category.Amount);
                Assert.Equal(33.33m, category.Share);
            });
        Assert.Null(stats.Note);
    }

    [Fact]
    public async Task MonthStats_WhenLedgerStartsMidPeriod_UseCoveredDaysAndExplainCoverage()
    {
        var context = await CreateAsync(new DateOnly(2026, 10, 10));
        await CreateEntryAsync(context, LedgerEntryKind.Expense, "餐饮", 120m, new DateOnly(2026, 10, 12));

        var stats = await context.Stats.GetAsync(EntryPeriod.Month, null, CancellationToken.None);

        Assert.NotNull(stats);
        Assert.Equal(12, stats.CoveredDays);
        Assert.Equal(10m, stats.ExpenseDailyAverage);
        Assert.Equal("账本自 2026-10-10 起，周期前段未覆盖。", stats.Note);
    }

    [Fact]
    public async Task YearStats_GroupByMonthAndCountOnlyElapsedMonths()
    {
        var context = await CreateAsync(new DateOnly(2026, 9, 1));
        await CreateEntryAsync(context, LedgerEntryKind.Income, "工资", 1000m, new DateOnly(2026, 9, 5));
        await CreateEntryAsync(context, LedgerEntryKind.Income, "工资", 1200m, new DateOnly(2026, 10, 5));

        var stats = await context.Stats.GetAsync(EntryPeriod.Year, new DateOnly(2026, 10, 21), CancellationToken.None);

        Assert.NotNull(stats);
        Assert.Equal(10, stats.Points.Count);
        Assert.Equal(1000m, stats.Points.Single(point => point.Date == new DateOnly(2026, 9, 1)).Income);
        Assert.Equal(1200m, stats.Points.Single(point => point.Date == new DateOnly(2026, 10, 1)).Income);
        Assert.Equal(2200m, stats.Income);
        Assert.Equal(51, stats.CoveredDays);
        Assert.Equal(43.14m, stats.IncomeDailyAverage);
        Assert.Equal("账本自 2026-09-01 起，周期前段未覆盖。", stats.Note);
    }

    [Fact]
    public async Task MonthStats_CompareWithSameProgressInPreviousMonthAndReturnTopEntries()
    {
        var context = await CreateAsync(new DateOnly(2026, 8, 1));
        await CreateEntryAsync(context, LedgerEntryKind.Expense, "餐饮", 200m, new DateOnly(2026, 9, 5));
        await CreateEntryAsync(context, LedgerEntryKind.Expense, "餐饮", 999m, new DateOnly(2026, 9, 25));
        await CreateEntryAsync(context, LedgerEntryKind.Income, "工资", 1000m, new DateOnly(2026, 9, 10));

        await CreateEntryAsync(context, LedgerEntryKind.Expense, "餐饮", 150m, new DateOnly(2026, 10, 3));
        await CreateEntryAsync(context, LedgerEntryKind.Expense, "购物", 350m, new DateOnly(2026, 10, 8));
        await CreateEntryAsync(context, LedgerEntryKind.Expense, "交通", 50m, new DateOnly(2026, 10, 12));
        await CreateEntryAsync(context, LedgerEntryKind.Expense, "娱乐", 80m, new DateOnly(2026, 10, 15));
        await CreateEntryAsync(context, LedgerEntryKind.Expense, "日用", 120m, new DateOnly(2026, 10, 16));
        await CreateEntryAsync(context, LedgerEntryKind.Expense, "医疗", 30m, new DateOnly(2026, 10, 18));
        await CreateEntryAsync(context, LedgerEntryKind.Income, "工资", 1200m, new DateOnly(2026, 10, 10));

        var stats = await context.Stats.GetAsync(EntryPeriod.Month, null, CancellationToken.None);

        Assert.NotNull(stats);
        Assert.NotNull(stats.Comparison);
        Assert.True(stats.Comparison.Comparable);
        Assert.Equal("较上月同期", stats.Comparison.Label);
        Assert.Equal(new DateOnly(2026, 9, 1), stats.Comparison.PreviousFrom);
        Assert.Equal(new DateOnly(2026, 9, 21), stats.Comparison.PreviousTo);
        Assert.Equal(200m, stats.Comparison.PreviousExpense);
        Assert.Equal(1000m, stats.Comparison.PreviousIncome);
        Assert.Equal(580m, stats.Comparison.ExpenseDelta);
        Assert.Equal(290m, stats.Comparison.ExpenseDeltaPercent);
        Assert.Equal(200m, stats.Comparison.IncomeDelta);
        Assert.Equal(20m, stats.Comparison.IncomeDeltaPercent);

        var dining = stats.ExpenseCategories.Single(category => category.Category == "餐饮");
        Assert.Equal(200m, dining.PreviousAmount);
        Assert.False(string.IsNullOrWhiteSpace(dining.ParentGroup));

        Assert.NotNull(stats.TopExpenses);
        Assert.Equal(5, stats.TopExpenses.Count);
        Assert.Equal(new[] { 350m, 150m, 120m, 80m, 50m }, stats.TopExpenses.Select(item => item.Amount).ToArray());

        var sortedPage = await context.Entries.GetPeriodAsync(
            EntryPeriod.Month,
            null,
            LedgerEntryKind.Expense,
            null,
            null,
            CancellationToken.None,
            sort: "amountDesc");

        Assert.NotNull(sortedPage);
        Assert.Equal(350m, sortedPage.Entries[0].Amount);
        Assert.Equal(30m, sortedPage.Entries[^1].Amount);
    }

    [Fact]
    public async Task MonthStats_WhenLedgerStartsAfterPreviousMonthStart_MarkComparisonNotComparable()
    {
        var context = await CreateAsync(new DateOnly(2026, 9, 15));
        await CreateEntryAsync(context, LedgerEntryKind.Expense, "餐饮", 100m, new DateOnly(2026, 10, 5));

        var stats = await context.Stats.GetAsync(EntryPeriod.Month, null, CancellationToken.None);

        Assert.NotNull(stats);
        Assert.NotNull(stats.Comparison);
        Assert.False(stats.Comparison.Comparable);
        Assert.Null(stats.Comparison.ExpenseDelta);
        Assert.Null(stats.ExpenseCategories.Single().PreviousAmount);
    }

    private static Task CreateEntryAsync(
        TestContext context,
        LedgerEntryKind kind,
        string category,
        decimal amount,
        DateOnly date)
    {
        return context.Entries.CreateAsync(
            new CreateLedgerEntryRequest(kind, category, amount, date, null, Guid.NewGuid()),
            CancellationToken.None);
    }

    private static async Task<TestContext> CreateAsync(DateOnly startDate)
    {
        var repository = new FakeLedgerRepository();
        var dates = new BusinessDateProvider(new FixedTimeProvider(Now));
        await new LedgerService(repository, dates).InitializeAsync(
            new InitializeLedgerRequest(startDate, 10000m),
            CancellationToken.None);

        return new TestContext(
            new LedgerEntryService(repository, dates),
            new EntryStatsService(repository, dates));
    }

    private sealed record TestContext(
        LedgerEntryService Entries,
        EntryStatsService Stats);
}

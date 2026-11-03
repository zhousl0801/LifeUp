using AssetKeeper.Application.Funds;
using AssetKeeper.Application.History;
using AssetKeeper.Application.Ledgers;
using AssetKeeper.Infrastructure.Persistence;
using AssetKeeper.Tests.Ledgers;
using AssetKeeper.Tests.Quotes;
using Microsoft.EntityFrameworkCore;

namespace AssetKeeper.Tests.Funds;

/// <summary>
/// 待确认买入的估值口径：份额在 T 日就已确定，确认当天不能凭空多出一笔差价。
/// </summary>
public sealed class FundPendingValuationTests
{
    private static readonly DateOnly Today = new(2026, 11, 3);
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 1, 50, 0, TimeSpan.Zero);

    [Fact]
    public async Task PendingBuy_IsMarkedAtTradeDateNetValue_BeforeConfirmation()
    {
        await using var context = await CreateAsync();

        // 10-29 按净值 2.0 买入 600 元（T 日净值已知），10-30 净值跌到 1.8。
        await context.Funds.BuyAsync(
            new BuyFundRequest(context.PositionId, 600m, new DateOnly(2026, 10, 29), null, Guid.NewGuid(),
                TradeDate: new DateOnly(2026, 10, 29)),
            CancellationToken.None);
        await TestFundQuotes.RefreshAsync(context.Repository, context.Dates, context.PositionId, 1.8m, new DateOnly(2026, 10, 30));

        var beforeConfirm = (await new FundService(context.Repository, context.Dates)
            .GetOverviewAsync(CancellationToken.None))!;

        // 已确认 1000 份 × 1.8 = 1800；待确认 600 元按 2.0 → 1.8 折算成 540，而不是仍记 600。
        Assert.Equal(600m, beforeConfirm.PendingAmount);
        Assert.Equal(2340m, beforeConfirm.TotalValue);
    }

    [Fact]
    public async Task ConfirmingPendingBuy_DoesNotChangeTotalValue()
    {
        await using var context = await CreateAsync();

        var pending = await context.Funds.BuyAsync(
            new BuyFundRequest(context.PositionId, 600m, new DateOnly(2026, 10, 29), null, Guid.NewGuid(),
                TradeDate: new DateOnly(2026, 10, 29)),
            CancellationToken.None);
        await TestFundQuotes.RefreshAsync(context.Repository, context.Dates, context.PositionId, 1.8m, new DateOnly(2026, 10, 30));
        var before = (await new FundService(context.Repository, context.Dates).GetOverviewAsync(CancellationToken.None))!;

        // 11-02 确认：600 ÷ 2.0 = 300 份，成交净值就是 10-29 的 2.0。
        await context.Funds.ConfirmBuyAsync(
            pending.Result!.Activity.Id,
            new ConfirmFundBuyRequest(300m, 2.0m, new DateOnly(2026, 10, 29), 0m, new DateOnly(2026, 11, 2), null, Guid.NewGuid()),
            CancellationToken.None);

        var after = (await new FundService(context.Repository, context.Dates).GetOverviewAsync(CancellationToken.None))!;

        Assert.Equal(2340m, before.TotalValue);
        Assert.Equal(before.TotalValue, after.TotalValue);
        Assert.Equal(0m, after.PendingAmount);
    }

    [Fact]
    public async Task MonthReturn_IgnoresRevaluationCausedByConfirmation()
    {
        await using var context = await CreateAsync();

        var pending = await context.Funds.BuyAsync(
            new BuyFundRequest(context.PositionId, 600m, new DateOnly(2026, 10, 29), null, Guid.NewGuid(),
                TradeDate: new DateOnly(2026, 10, 29)),
            CancellationToken.None);
        await TestFundQuotes.RefreshAsync(context.Repository, context.Dates, context.PositionId, 1.8m, new DateOnly(2026, 10, 30));
        await context.Funds.ConfirmBuyAsync(
            pending.Result!.Activity.Id,
            new ConfirmFundBuyRequest(300m, 2.0m, new DateOnly(2026, 10, 29), 0m, new DateOnly(2026, 11, 2), null, Guid.NewGuid()),
            CancellationToken.None);

        var result = (await new FundReturnService(context.Repository, context.Dates)
            .GetAsync(TrendRange.Month, Today, null, false, CancellationToken.None))!;

        // 11 月没有新净值披露，也没有资金变动，区间盈亏必须是 0；不能再出现「披露 0 天却有金额」。
        Assert.Equal(0, result.DisclosureDays);
        Assert.Equal(2340m, result.OpeningAssetValue);
        Assert.Equal(2340m, result.ClosingAssetValue);
        Assert.Equal(0m, result.IntervalProfit);
        Assert.All(result.Points, point => Assert.Null(point.DailyProfit));
    }

    [Fact]
    public async Task PendingBuy_WithoutTradeDateNetValue_FallsBackToAmount()
    {
        await using var context = await CreateAsync();

        // T 日 11-02 的净值还没披露，待确认只能按承诺金额计值，不能猜一个净值出来。
        await context.Funds.BuyAsync(
            new BuyFundRequest(context.PositionId, 600m, new DateOnly(2026, 11, 2), null, Guid.NewGuid(),
                TradeDate: new DateOnly(2026, 11, 2)),
            CancellationToken.None);
        await TestFundQuotes.RefreshAsync(context.Repository, context.Dates, context.PositionId, 1.8m, new DateOnly(2026, 10, 30));

        var overview = (await new FundService(context.Repository, context.Dates)
            .GetOverviewAsync(CancellationToken.None))!;

        Assert.Equal(600m, overview.PendingAmount);
        Assert.Equal(2400m, overview.TotalValue);
    }

    [Fact]
    public async Task DailyProfit_OnDisclosureDate_IncludesPendingRevaluation()
    {
        await using var context = await CreateAsync();

        await context.Funds.BuyAsync(
            new BuyFundRequest(context.PositionId, 600m, new DateOnly(2026, 10, 29), null, Guid.NewGuid(),
                TradeDate: new DateOnly(2026, 10, 29)),
            CancellationToken.None);
        await TestFundQuotes.RefreshAsync(context.Repository, context.Dates, context.PositionId, 1.8m, new DateOnly(2026, 10, 30));

        var result = (await new FundReturnService(context.Repository, context.Dates)
            .GetAsync(TrendRange.Month, new DateOnly(2026, 10, 30), null, false, CancellationToken.None))!;

        // 10-30 净值 2.0 → 1.8：已确认 1000 份跌 200，待确认 600 元同样跌 60，两边必须同时算上。
        var day = result.Points.Single(point => point.Date == new DateOnly(2026, 10, 30));
        Assert.Equal(-260m, day.DailyProfit);
        Assert.Equal(-260m, day.CumulativeProfit);
    }

    private static async Task<TestContext> CreateAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var repository = new LedgerRepository(db);
        var dates = new BusinessDateProvider(new FixedTimeProvider(Now));
        await new LedgerService(repository, dates).InitializeAsync(
            new InitializeLedgerRequest(new DateOnly(2026, 10, 20), 10000m),
            CancellationToken.None);

        var opening = await TestFundQuotes.CreatePositionAsync(repository, dates,
            new CreateFundPositionRequest(
                "纳指基金",
                "000001",
                "A",
                "平台一",
                1000m,
                2000m,
                2.0m,
                new DateOnly(2026, 10, 20),
                null,
                Guid.NewGuid(),
                HoldingDate: new DateOnly(2026, 10, 20)),
            CancellationToken.None);

        await TestFundQuotes.RefreshAsync(repository, dates, opening.Result!.Position.Id, 2.0m, new DateOnly(2026, 10, 29));

        return new TestContext(db, repository, dates, new FundService(repository, dates),
            opening.Result.Position.Id);
    }

    private sealed record TestContext(
        AppDbContext Db,
        LedgerRepository Repository,
        BusinessDateProvider Dates,
        FundService Funds,
        Guid PositionId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

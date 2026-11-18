using LifeUp.Tests.Quotes;
using LifeUp.Application.Assets;
using LifeUp.Application.Cash;
using LifeUp.Application.Funds;
using LifeUp.Application.Gold;
using LifeUp.Application.Ledgers;
using LifeUp.Application.Provident;
using LifeUp.Application.Stocks;
using LifeUp.Domain.Assets;
using LifeUp.Domain.Ledgers;
using LifeUp.Infrastructure.Persistence;
using LifeUp.Tests.Ledgers;
using Microsoft.EntityFrameworkCore;

namespace LifeUp.Tests.Ledgers;

public sealed class MonthChangeServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_ExplainsTotalChangeWithFourComponents()
    {
        await using var context = await CreateAsync(Today.AddDays(-31));

        await context.Entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Income, "工资", 2000m, new DateOnly(2026, 10, 5), null, Guid.NewGuid()),
            CancellationToken.None);
        await context.Entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Expense, "餐饮", 500m, new DateOnly(2026, 10, 6), null, Guid.NewGuid()),
            CancellationToken.None);
        await context.Provident.DepositAsync(
            new ProvidentDepositRequest(1000m, new DateOnly(2026, 10, 10), null, Guid.NewGuid()),
            CancellationToken.None);
        await TestFundQuotes.RefreshAsync(new LedgerRepository(context.Db), new BusinessDateProvider(new FixedTimeProvider(Now)), context.FundPositionId, 12m, new DateOnly(2026, 10, 15));
        await context.Gold.UpdateQuoteAsync(
            new UpdateGoldQuoteRequest(950m, new DateOnly(2026, 10, 16), Guid.NewGuid()),
            CancellationToken.None);
        var cash = await context.Cash.GetOverviewAsync(CancellationToken.None);
        await context.Cash.ReconcileAsync(
            new ReconcileCashRequest(11600m, "漏记收入", Guid.NewGuid(), cash!.Version),
            CancellationToken.None);

        var change = await context.MonthChange.GetAsync(CancellationToken.None);

        Assert.NotNull(change);
        Assert.True(change.IsComplete);
        Assert.Equal(new DateOnly(2026, 10, 1), change.From);
        Assert.Equal(Today, change.To);
        Assert.Equal(3300m, change.TotalChange);
        Assert.Equal(1500m, change.LivingSurplus);
        Assert.Equal(1000m, change.ProvidentDeposit);
        Assert.Equal(100m, change.ReconciliationDifference);
        Assert.Equal(700m, change.InvestmentProfit);
        Assert.Equal(
            change.TotalChange,
            change.LivingSurplus + change.ProvidentDeposit + change.InvestmentProfit + change.ReconciliationDifference);
        Assert.Empty(change.Notes);
    }

    [Fact]
    public async Task GetAsync_DayScope_ComparesTodayWithYesterday()
    {
        await using var context = await CreateAsync(Today.AddDays(-31));
        await TestFundQuotes.RefreshAsync(new LedgerRepository(context.Db), new BusinessDateProvider(new FixedTimeProvider(Now)), context.FundPositionId, 12m, new DateOnly(2026, 10, 15));
        await context.Gold.UpdateQuoteAsync(
            new UpdateGoldQuoteRequest(950m, new DateOnly(2026, 10, 16), Guid.NewGuid()),
            CancellationToken.None);
        await context.Entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Income, "工资", 500m, Today, null, Guid.NewGuid()),
            CancellationToken.None);

        var dates = new BusinessDateProvider(new FixedTimeProvider(Now));
        var change = await new MonthChangeService(new LedgerRepository(context.Db), dates)
            .GetAsync(ChangeScope.Day, CancellationToken.None);

        Assert.NotNull(change);
        Assert.Equal(Today, change.From);
        Assert.Equal(Today, change.To);
        Assert.True(change.HasComparableData);
        Assert.Equal(500m, change.LivingSurplus);
        Assert.Equal(500m, change.ComparableChange);
        Assert.Equal(0m, change.ReconciliationDifference);
    }

    [Fact]
    public async Task GetAsync_DayScope_SeparatesInvestmentProfitAndCashFlowsWithoutCountingTransfers()
    {
        await using var context = await CreateAsync(Today.AddDays(-31));
        var repository = new LedgerRepository(context.Db);
        var dates = new BusinessDateProvider(new FixedTimeProvider(Now));
        var stocks = new StockService(repository, dates);

        var importedExpense = LedgerEntry.Create(
            (await repository.GetAsync(CancellationToken.None))!.Id,
            LedgerEntryKind.Expense, "历史汇总", 999m, Today, Today,
            null, LedgerEntrySource.MonthlyImport, Guid.NewGuid(), Now);
        context.Db.LedgerEntries.Add(importedExpense);
        await context.Db.SaveChangesAsync();

        await context.Entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Income, "工资", 500m, Today, null, Guid.NewGuid()),
            CancellationToken.None);
        await context.Entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Expense, "餐饮", 120m, Today, null, Guid.NewGuid()),
            CancellationToken.None);
        await context.Provident.DepositAsync(
            new ProvidentDepositRequest(1000m, Today, null, Guid.NewGuid()),
            CancellationToken.None);
        await context.Provident.WithdrawAsync(
            new ProvidentWithdrawalRequest(200m, Today, null, Guid.NewGuid()),
            CancellationToken.None);
        await TestFundQuotes.RefreshAsync(repository, dates, context.FundPositionId, 12m, Today);
        await context.Gold.UpdateQuoteAsync(
            new UpdateGoldQuoteRequest(850m, Today, Guid.NewGuid()),
            CancellationToken.None);
        var firstBuy = await stocks.BuyAsync(
            new BuyStockRequest(StockMarket.Shanghai, "600000", "测试股票", 100m, 10m, 5m, Today, null, Guid.NewGuid()),
            CancellationToken.None);
        Assert.Equal(AssetOperationStatus.Created, firstBuy.Status);
        var recorded = await stocks.SaveHoldingAsync(
            new SaveStockHoldingRequest(StockMarket.Shanghai, "600001", "已持股票",
                100m, 1750m, 20m, Today, Today, null, Guid.NewGuid()),
            CancellationToken.None);
        Assert.Equal(AssetOperationStatus.Success, recorded.Status);
        var cash = await context.Cash.GetOverviewAsync(CancellationToken.None);
        Assert.NotNull(cash);
        await context.Cash.ReconcileAsync(
            new ReconcileCashRequest(cash.Balance + 25m, "余额核对", Guid.NewGuid(), cash.Version),
            CancellationToken.None);

        var change = await context.MonthChange.GetAsync(ChangeScope.Day, CancellationToken.None);

        Assert.NotNull(change);
        Assert.True(change.HasComparableData);
        Assert.Equal(1100m, change.ComparableChange);
        Assert.Equal(2000m, change.AddedRecords);
        Assert.Equal(380m, change.LivingSurplus);
        Assert.Equal(-305m, change.InvestmentProfit);
        var details = Assert.IsType<DayChangeBreakdownDto>(change.DayBreakdown);
        Assert.Equal(500m, details.CashIncome);
        Assert.Equal(120m, details.CashExpense);
        Assert.Equal(1000m, details.ProvidentDeposit);
        Assert.Equal(200m, details.Fund?.Profit);
        var stock = Assert.IsType<DayInvestmentChangeDto>(details.Stock);
        Assert.Equal(-5m, stock.Profit);
        Assert.Equal(0m, stock.PreviousValue);
        Assert.Equal(3000m, stock.CurrentValue);
        Assert.Equal(-1005m, stock.CashTransfer);
        Assert.Equal(2000m, stock.OpeningValue);
        Assert.Equal(-500m, details.Gold?.Profit);
        Assert.Equal(25m, details.CashAdjustment);
        Assert.Equal(0m, details.OtherDifference);
    }

    [Fact]
    public async Task GetAsync_WhenLedgerStartsThisMonth_HasNoComparableBaseline()
    {
        var startDate = new DateOnly(2026, 10, 10);
        await using var context = await CreateAsync(startDate);
        await context.Entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Income, "工资", 100m, new DateOnly(2026, 10, 12), null, Guid.NewGuid()),
            CancellationToken.None);

        var change = await context.MonthChange.GetAsync(CancellationToken.None);

        Assert.NotNull(change);
        Assert.False(change.IsComplete);
        Assert.False(change.HasComparableData);
        Assert.Equal(startDate, change.From);
        Assert.Null(change.TotalChange);
        Assert.Null(change.InvestmentProfit);
        Assert.Equal(100m, change.LivingSurplus);
        Assert.Equal(0m, change.AddedRecords);
        Assert.Contains(change.Notes, note => note.Contains("暂无可比基线"));
    }

    [Fact]
    public async Task GetAsync_SeparatesNewRecordsFromSameScopeChange()
    {
        await using var context = await CreateAsync(Today.AddDays(-31), includeProvident: false, includeGold: false);
        await context.Provident.SetOpeningAsync(
            new ProvidentOpeningRequest(5000m, new DateOnly(2026, 10, 5), null, Guid.NewGuid()),
            CancellationToken.None);
        var goldResult = await context.Gold.SaveHoldingAsync(
            new SaveGoldHoldingRequest(10m, 8000m, 900m, new DateOnly(2026, 10, 5), null, Guid.NewGuid(),
                HoldingDate: new DateOnly(2026, 10, 5)),
            CancellationToken.None);
        Assert.True(goldResult.Status is AssetOperationStatus.Success or AssetOperationStatus.Created,
            string.Join("；", goldResult.Errors));

        var change = await context.MonthChange.GetAsync(CancellationToken.None);

        Assert.NotNull(change);
        Assert.True(change.HasComparableData);
        Assert.Equal(0m, change.ComparableChange);
        Assert.Equal(14000m, change.AddedRecords);
        Assert.Contains(change.Notes, note => note.Contains("含新增记录"));
        Assert.True(change.AssetChanges.Single(item => item.AssetType == "fund").IsNew == false);
        Assert.True(change.AssetChanges.Single(item => item.AssetType == "providentFund").IsNew);
        Assert.True(change.AssetChanges.Single(item => item.AssetType == "gold").IsNew);
    }

    [Fact]
    public async Task GetAsync_WithoutCompleteAssetCoverage_LeavesChangeEmpty()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var repository = new LedgerRepository(db);
        var dates = new BusinessDateProvider(new FixedTimeProvider(Now));
        await new LedgerService(repository, dates).InitializeAsync(
            new InitializeLedgerRequest(Today.AddDays(-10), 1000m),
            CancellationToken.None);

        var change = await new MonthChangeService(repository, dates).GetAsync(CancellationToken.None);

        Assert.NotNull(change);
        Assert.False(change.IsComplete);
        Assert.Null(change.TotalChange);
        Assert.Null(change.InvestmentProfit);
        Assert.Contains(change.Notes, note => note.Contains("暂无可比基线"));
        Assert.Equal(0m, change.LivingSurplus);
        Assert.Equal(0m, change.ReconciliationDifference);
        await db.DisposeAsync();
    }

    private static async Task<TestContext> CreateAsync(
        DateOnly startDate,
        bool includeProvident = true,
        bool includeGold = true)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var repository = new LedgerRepository(db);
        var dates = new BusinessDateProvider(new FixedTimeProvider(Now));
        await new LedgerService(repository, dates).InitializeAsync(
            new InitializeLedgerRequest(startDate, 10000m),
            CancellationToken.None);
        var openingDate = startDate;
        var fund = await TestFundQuotes.CreatePositionAsync(repository, dates,
            new CreateFundPositionRequest(
                "测试基金",
                "000001",
                "A",
                "测试平台",
                100m,
                1000m,
                10m,
                openingDate,
                null,
                Guid.NewGuid(),
                HoldingDate: openingDate),
            CancellationToken.None);

        if (includeGold)
        {
            await new GoldService(repository, dates).SaveHoldingAsync(
                new SaveGoldHoldingRequest(10m, 8000m, 900m, openingDate, null, Guid.NewGuid(), HoldingDate: openingDate),
                CancellationToken.None);
        }

        if (includeProvident)
        {
            await new ProvidentFundService(repository, dates).SetOpeningAsync(
                new ProvidentOpeningRequest(5000m, openingDate, null, Guid.NewGuid()),
                CancellationToken.None);
        }

        return new TestContext(
            db,
            new LedgerEntryService(repository, dates),
            new CashService(repository, dates),
            new FundService(repository, dates),
            new GoldService(repository, dates),
            new ProvidentFundService(repository, dates),
            new MonthChangeService(repository, dates),
            fund.Result!.Position.Id);
    }

    private sealed record TestContext(
        AppDbContext Db,
        LedgerEntryService Entries,
        CashService Cash,
        FundService Funds,
        GoldService Gold,
        ProvidentFundService Provident,
        MonthChangeService MonthChange,
        Guid FundPositionId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

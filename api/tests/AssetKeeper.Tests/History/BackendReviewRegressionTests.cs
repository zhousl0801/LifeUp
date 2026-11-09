using AssetKeeper.Application.Ledgers;
using AssetKeeper.Application.Stocks;
using AssetKeeper.Application.Funds;
using AssetKeeper.Application.Gold;
using AssetKeeper.Application.History;
using AssetKeeper.Application.Quotes;
using AssetKeeper.Domain.Assets;
using AssetKeeper.Tests.Ledgers;


namespace AssetKeeper.Tests.History;

public sealed class BackendReviewRegressionTests
{
    private static readonly CancellationToken Token = CancellationToken.None;
    private static readonly DateOnly Today = new(2026, 11, 6);
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 2, 0, 0, TimeSpan.Zero);
    private static readonly BusinessDateProvider Dates = new(new FixedTimeProvider(Now));

    private static async Task<FakeLedgerRepository> CreateAsync()
    {
        var repository = new FakeLedgerRepository();
        await new LedgerService(repository, Dates).InitializeAsync(new(Today.AddDays(-10), 10000m), Token);
        return repository;
    }

    [Fact]
    public async Task NotHeldAssets_CompleteTodaysTrend()
    {
        var repository = await CreateAsync();
        var ledger = new LedgerService(repository, Dates);
        await ledger.UpdateCoverageAsync(new(true, true, true, true), Token);
        var overview = await ledger.GetOverviewAsync(Token);
        var trend = await new TrendService(repository, Dates).GetAsync(TrendRange.Week, Token);

        Assert.True(overview!.IsComplete);
        Assert.True(trend!.Points.Last().IsComplete);
        Assert.Equal(10000m, trend.Points.Last().TotalAssets);
    }

    [Fact]
    public async Task StockHistoryCorrection_PreservesFee()
    {
        var repository = await CreateAsync();
        var stocks = new StockService(repository, Dates);
        await stocks.BuyAsync(new(StockMarket.Shanghai, "600000", "Test", 100m, 10m, 5m, Today.AddDays(-1), null, Guid.NewGuid()), Token);
        var transaction = repository.AssetTransactions.Single();
        var request = new HistoryChangeRequest(Guid.NewGuid(), repository.Ledger!.Version, "asset", transaction.Id, transaction.Version, "correct", transaction.BusinessDate, transaction.BusinessSequence, transaction.Amount, transaction.Quantity, transaction.Price, transaction.PriceDate, null, null, "changed note", "review", Fee: transaction.Fee);
        var preview = await new HistoryService(repository, Dates).PreviewAsync(request, Token);

        Assert.Equal(AssetKeeper.Application.Assets.AssetOperationStatus.Success, preview.Status);
        var saved = await new HistoryService(repository, Dates).ChangeAsync(new(request, preview.Result!.PreviewToken), Token);
        Assert.Equal(AssetKeeper.Application.Assets.AssetOperationStatus.Success, saved.Status);
        Assert.Equal(5m, repository.AssetTransactions.Single().Fee);
    }

    [Fact]
    public async Task GoldNotHeld_PreservesPendingProceeds()
    {
        var repository = await CreateAsync();
        var gold = new GoldService(repository, Dates);
        var opening = await gold.SaveHoldingAsync(new(1m, 500m, 500m, Today.AddDays(-2), null, Guid.NewGuid(), HoldingDate: Today.AddDays(-2)), Token);
        var sell = await gold.SellAsync(new(1m, 600m, Today.AddDays(-1), null, Guid.NewGuid(), Settlement: GoldSellSettlement.PendingArrival), Token);
        var ledger = new LedgerService(repository, Dates);
        var before = await ledger.GetOverviewAsync(Token);
        var coverage = await ledger.UpdateCoverageAsync(new(false, false, true, false), Token);
        var after = await ledger.GetOverviewAsync(Token);

        Assert.Equal(AssetKeeper.Application.Assets.AssetOperationStatus.Created, sell.Status);
        Assert.Equal(before!.TotalAssets, after!.TotalAssets);
        Assert.Equal(600m, after.Gold.Amount);
    }

    [Fact]
    public async Task FundDateCorrection_PreviewMatchesSavedValuation()
    {
        var repository = await CreateAsync();
        var funds = new FundService(repository, Dates, new Quotes());
        var opening = await funds.CreatePositionAsync(new("Test", "000001", "A", "bank", 100m, 100m, 1m, Today.AddDays(-4), null, Guid.NewGuid(), Today.AddDays(-5)), Token);
        var positionId = repository.FundPositions.Single().Id;
        await funds.RefreshQuoteAsync(positionId, Token, false, new("000001", 2m, Today.AddDays(-2), "review", Now));
        await funds.RefreshQuoteAsync(positionId, Token, false, new("000001", 3m, Today, "review", Now));
        await funds.BuyAsync(new(positionId, 100m, Today.AddDays(-4), null, Guid.NewGuid(), TradeDate: Today.AddDays(-4)), Token);
        var pending = repository.AssetTransactions.Single(x => x.Kind == AssetTransactionKind.FundBuyPending);
        var change = new HistoryChangeRequest(Guid.NewGuid(), repository.Ledger!.Version, "asset", pending.Id, pending.Version, "correct", Today.AddDays(-2), pending.BusinessSequence, pending.Amount, null, null, null, null, null, null, "review");
        var history = new HistoryService(repository, Dates);
        var preview = await history.PreviewAsync(change, Token);
        var saved = await history.ChangeAsync(new(change, preview.Result!.PreviewToken), Token);
        var actual = await history.GetAsync(Today, Token);

        Assert.Equal(150m, preview.Result!.After.Funds.Single().PendingValue);
        Assert.Equal(preview.Result.After.Funds.Single().PendingValue, actual!.Snapshot.Funds.Single().PendingValue);
        Assert.Equal(actual.Snapshot.Funds.Single().PendingValue, saved.Result!.After.Funds.Single().PendingValue);
    }

    [Fact]
    public async Task UnrelatedEntry_DoesNotAdvanceStockProjectionVersion()
    {
        var repository = await CreateAsync();
        var stocks = new StockService(repository, Dates);
        await stocks.BuyAsync(new(StockMarket.Shanghai, "600000", "Test", 100m, 10m, 5m,
            Today.AddDays(-1), null, Guid.NewGuid()), Token);
        var position = repository.StockPositions.Single();
        var version = position.Version;
        var updatedAt = position.UpdatedAt;

        var result = await new LedgerEntryService(repository, Dates).CreateAsync(new(
            AssetKeeper.Domain.Ledgers.LedgerEntryKind.Expense, "餐饮", 10m, Today, null, Guid.NewGuid()), Token);

        Assert.Equal(EntryOperationStatus.Created, result.Status);
        Assert.Equal(version, position.Version);
        Assert.Equal(updatedAt, position.UpdatedAt);
        Assert.Equal(8985m, repository.CashAccount!.Balance);
    }

    private sealed class Quotes : IFundQuoteProvider
    {
        public Task<ExternalFundQuote> GetLatestAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult(new ExternalFundQuote(code, 1m, new(2026, 11, 2), "review", Now));
    }
}

using System.Text;
using LifeUp.Application.Assets;
using LifeUp.Application.Cash;
using LifeUp.Application.Funds;
using LifeUp.Application.Gold;
using LifeUp.Application.History;
using LifeUp.Application.Ledgers;
using LifeUp.Application.Plans;
using LifeUp.Application.Provident;
using LifeUp.Application.Quotes;
using LifeUp.Application.Stocks;
using LifeUp.Domain.Assets;
using LifeUp.Domain.Ledgers;
using LifeUp.Domain.Plans;
using LifeUp.Infrastructure.Backup;
using LifeUp.Infrastructure.Persistence;
using LifeUp.Tests.Ledgers;
using LifeUp.Tests.Quotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LifeUp.Tests.Regression;

public sealed class BackendRefactoringRegressionTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CalculateSeries_MatchesSingleDateCalculate_AcrossMultiDayHistory()
    {
        var start = Today.AddDays(-10);
        await using var context = await CreateContextAsync(start, 20000m);
        var entries = new LedgerEntryService(context.Repository, context.Dates);
        var provident = new ProvidentFundService(context.Repository, context.Dates);
        var gold = new GoldService(context.Repository, context.Dates);
        var stocks = new StockService(context.Repository, context.Dates);
        var funds = new FundService(context.Repository, context.Dates);

        await entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Income, "工资", 3000m, start.AddDays(1), null, Guid.NewGuid()),
            CancellationToken.None);
        await entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Expense, "餐饮", 200m, start.AddDays(2), null, Guid.NewGuid()),
            CancellationToken.None);
        await provident.DepositAsync(
            new ProvidentDepositRequest(1500m, start.AddDays(3), null, Guid.NewGuid()),
            CancellationToken.None);
        await gold.SaveHoldingAsync(
            new SaveGoldHoldingRequest(10m, 5000m, 520m, start.AddDays(2), "初始黄金", Guid.NewGuid(), 0, start.AddDays(2)),
            CancellationToken.None);
        await gold.UpdateQuoteAsync(
            new UpdateGoldQuoteRequest(540m, start.AddDays(6), Guid.NewGuid()),
            CancellationToken.None);
        await stocks.BuyAsync(
            new BuyStockRequest(StockMarket.Shanghai, "600000", "浦发银行", 100m, 10m, 5m, start.AddDays(4), null, Guid.NewGuid()),
            CancellationToken.None);

        var positionResult = await TestFundQuotes.CreatePositionAsync(
            context.Repository,
            context.Dates,
            new CreateFundPositionRequest(
                "沪深300",
                "000300",
                "A",
                "天天基金",
                1000m,
                1000m,
                1.0m,
                start.AddDays(1),
                null,
                Guid.NewGuid(),
                HoldingDate: start.AddDays(1)),
            CancellationToken.None);
        var fundId = positionResult.Result!.Position.Id;
        await funds.BuyAsync(
            new BuyFundRequest(fundId, 500m, start.AddDays(5), null, Guid.NewGuid(), 1.5m),
            CancellationToken.None);
        await TestFundQuotes.RefreshAsync(context.Repository, context.Dates, fundId, 1.1m, start.AddDays(5));
        await TestFundQuotes.RefreshAsync(context.Repository, context.Dates, fundId, 1.2m, start.AddDays(8));

        var data = await context.Repository.GetReplayDataReadOnlyAsync(context.LedgerId, CancellationToken.None);
        var facts = BusinessFact.From(data);
        var calculator = new LedgerReplayCalculator();
        var series = calculator.CalculateSeries(data, facts, start, Today, currentDate: Today);

        for (var date = start; date <= Today; date = date.AddDays(1))
        {
            var expected = calculator.Calculate(data, facts, date, currentDate: Today);
            var actual = series[date];

            Assert.Equal(expected.CashBalance, actual.CashBalance);
            Assert.Equal(expected.ProvidentBalance, actual.ProvidentBalance);
            Assert.Equal(expected.TotalAssets, actual.TotalAssets);
            Assert.Equal(expected.KnownTotalAssets, actual.KnownTotalAssets);
            Assert.Equal(expected.Gold?.Value, actual.Gold?.Value);
            Assert.Equal(expected.Funds.Single().Value, actual.Funds.Single().Value);
            Assert.Equal(expected.Funds.Single().PendingValue, actual.Funds.Single().PendingValue);
            Assert.Equal(expected.Stocks.Single().Value, actual.Stocks.Single().Value);
            Assert.Equal(expected.Issues, actual.Issues);
        }
    }

    [Fact]
    public async Task PlanExecution_MarksAutoIncomeSourceAsAutomatic_AndRecordOccurrenceTouchesLedgerOnce()
    {
        await using var context = await CreateContextAsync(Today, 100m);
        var planRepo = new PlanRepository(context.Db, context.Cache);
        var entries = new LedgerEntryService(context.Repository, context.Dates);
        var provident = new ProvidentFundService(context.Repository, context.Dates);
        var funds = new FundService(context.Repository, context.Dates);
        var plans = new PlanService(context.Repository, planRepo, context.Dates);
        var execution = new PlanExecutionService(context.Repository, planRepo, context.Dates, entries, provident, funds);

        await plans.SaveAsync(
            new SavePlanRequest(
                null,
                AutoPlanType.SalaryIncome,
                "月薪",
                5000m,
                AutoPlanCycle.Monthly,
                Today.Day,
                null,
                new TimeOnly(1, 0),
                Today,
                null,
                Guid.NewGuid(),
                ExpectedVersion: null),
            CancellationToken.None);

        var run = await execution.RunDueAsync(CancellationToken.None);

        Assert.Equal(1, run.Completed);
        var salaryEntry = await context.Db.LedgerEntries.SingleAsync();
        Assert.Equal(LedgerEntrySource.Automatic, salaryEntry.Source);

        var position = (await TestFundQuotes.CreatePositionAsync(
            context.Repository,
            context.Dates,
            new CreateFundPositionRequest(
                "红利ETF联接",
                "000001",
                "A",
                null,
                100m,
                100m,
                1m,
                Today,
                null,
                Guid.NewGuid(),
                HoldingDate: Today),
            CancellationToken.None)).Result!.Position.Id;

        await plans.SaveAsync(
            new SavePlanRequest(
                null,
                AutoPlanType.FundSip,
                "红利定投",
                6000m,
                AutoPlanCycle.Monthly,
                Today.Day,
                null,
                new TimeOnly(1, 0),
                Today,
                position,
                Guid.NewGuid(),
                ExpectedVersion: null),
            CancellationToken.None);

        await execution.RunDueAsync(CancellationToken.None);
        var waitingOccurrence = (await planRepo.GetOccurrencesAsync(context.LedgerId, null, null, 10, CancellationToken.None))
            .Single(item => item.Type == AutoPlanType.FundSip);
        Assert.Equal(PlanOccurrenceStatus.Waiting, waitingOccurrence.Status);

        await entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Income, "其他收入", 2000m, Today, null, Guid.NewGuid()),
            CancellationToken.None);
        var versionBeforeRecord = (await context.Repository.GetAsync(CancellationToken.None))!.Version;

        var recordRequest = new PlanActionRequest(Guid.NewGuid());
        var recordResult = await execution.RecordOccurrenceAsync(waitingOccurrence.Id, recordRequest, CancellationToken.None);

        Assert.Equal(AssetOperationStatus.Success, recordResult.Status);
        Assert.True(recordResult.Result!.Recorded);
        var versionAfterRecord = (await context.Repository.GetAsync(CancellationToken.None))!.Version;
        Assert.Equal(versionBeforeRecord + 1, versionAfterRecord);
    }

    [Fact]
    public async Task SetPriorRealizedProfit_IncrementsLedgerVersionOnceAndReplaysIdempotently()
    {
        await using var context = await CreateContextAsync(Today, 1000m);
        var funds = new FundService(context.Repository, context.Dates);
        var created = await TestFundQuotes.CreatePositionAsync(
            context.Repository,
            context.Dates,
            new CreateFundPositionRequest(
                "纳指100",
                "100001",
                "A",
                null,
                100m,
                100m,
                1m,
                Today,
                null,
                Guid.NewGuid(),
                HoldingDate: Today),
            CancellationToken.None);
        var positionId = created.Result!.Position.Id;
        var versionBefore = (await context.Repository.GetAsync(CancellationToken.None))!.Version;
        var request = new SetPriorRealizedProfitRequest(350.5m, null, Guid.NewGuid());

        var first = await funds.SetPriorRealizedProfitAsync(positionId, request, CancellationToken.None);
        var versionAfterFirst = (await context.Repository.GetAsync(CancellationToken.None))!.Version;
        var replay = await funds.SetPriorRealizedProfitAsync(positionId, request, CancellationToken.None);
        var versionAfterReplay = (await context.Repository.GetAsync(CancellationToken.None))!.Version;

        Assert.Equal(AssetOperationStatus.Success, first.Status);
        Assert.Equal(350.5m, first.Result!.PriorRealizedProfit);
        Assert.Equal(versionBefore + 1, versionAfterFirst);
        Assert.Equal(AssetOperationStatus.Success, replay.Status);
        Assert.Equal(versionAfterFirst, versionAfterReplay);
    }

    [Fact]
    public void StockTransaction_CorrectWithNullFee_PreservesExistingFee()
    {
        var transaction = AssetTransaction.CreateStockBuy(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            1005m,
            10m,
            5m,
            "招商银行",
            Today,
            "原备注",
            Guid.NewGuid(),
            Now);

        transaction.Correct(
            quantity: 100m,
            amount: 1005m,
            price: 10m,
            date: Today,
            priceDate: Today,
            sequence: 1,
            note: "仅改备注",
            fee: null);

        Assert.Equal(5m, transaction.Fee);
        Assert.Equal("仅改备注", transaction.Note);
    }

    [Fact]
    public async Task MonthChange_MonthScope_ExcludesMonthlyImportFromLivingSurplus()
    {
        var start = new DateOnly(2026, 9, 1);
        await using var context = await CreateContextAsync(start, 5000m);
        var entries = new LedgerEntryService(context.Repository, context.Dates);
        var monthChange = new MonthChangeService(context.Repository, context.Dates);

        var imported = LedgerEntry.Create(
            context.LedgerId,
            LedgerEntryKind.Expense,
            "历史月度汇总",
            1200m,
            new DateOnly(2026, 10, 1),
            Today,
            null,
            LedgerEntrySource.MonthlyImport,
            Guid.NewGuid(),
            Now);
        context.Db.LedgerEntries.Add(imported);
        await context.Db.SaveChangesAsync();
        context.Cache.InvalidateAll();

        await entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Income, "工资", 3000m, new DateOnly(2026, 10, 10), null, Guid.NewGuid()),
            CancellationToken.None);
        await entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Expense, "餐饮", 400m, new DateOnly(2026, 10, 12), null, Guid.NewGuid()),
            CancellationToken.None);

        var result = await monthChange.GetAsync(ChangeScope.Month, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2600m, result.LivingSurplus);
    }

    [Fact]
    public async Task BackupCsvExport_EscapesFormulaPrefixes_PreservesNegativeAmounts_AndFormatsBonusPlan()
    {
        await using var context = await CreateContextAsync(Today, 5000m);
        var entries = new LedgerEntryService(context.Repository, context.Dates);
        var planRepo = new PlanRepository(context.Db, context.Cache);
        var plans = new PlanService(context.Repository, planRepo, context.Dates);
        var backup = new BackupService(context.Db, context.Dates, context.Cache);

        await entries.CreateAsync(
            new CreateLedgerEntryRequest(LedgerEntryKind.Expense, "餐饮", 200m, Today, "=SUM(1,2)", Guid.NewGuid()),
            CancellationToken.None);
        var savedPlan = await plans.SaveAsync(
            new SavePlanRequest(
                null,
                AutoPlanType.BonusIncome,
                "年终大奖",
                20000m,
                AutoPlanCycle.Yearly,
                15,
                null,
                new TimeOnly(9, 0),
                Today,
                null,
                Guid.NewGuid(),
                ExpectedVersion: null,
                MonthOfYear: Today.Month),
            CancellationToken.None);
        var planEntity = await context.Db.AutoPlans.SingleAsync(item => item.Id == savedPlan.Result!.Id);
        await planRepo.AddOccurrenceAsync(
            PlanOccurrence.Create(planEntity, $"Y{Today.Year:D4}", Today, new TimeOnly(9, 0), Now),
            CancellationToken.None);
        await planRepo.SaveChangesAsync(CancellationToken.None);

        var csvResult = await backup.ExportDetailsAsync(Today, Today, CancellationToken.None);

        Assert.NotNull(csvResult);
        var text = Encoding.UTF8.GetString(csvResult.Content);
        Assert.Contains("\"-200.00\"", text);
        Assert.Contains("年终奖", text);
        Assert.DoesNotContain("BonusIncome", text);
    }

    [Fact]
    public async Task QuoteRefresh_SelectiveTargets_OnlyRefreshesRequestedAssets()
    {
        await using var context = await CreateContextAsync(Today.AddDays(-2), 10000m);
        var fundProvider = new TestFundQuotes(1.25m, Today, Now);
        var goldProvider = new TestGoldQuotes(610m, Today, Now);
        var funds = new FundService(context.Repository, context.Dates, fundProvider);
        var gold = new GoldService(context.Repository, context.Dates, goldProvider);
        var stocks = new StockService(context.Repository, context.Dates);

        await funds.CreatePositionAsync(
            new CreateFundPositionRequest(
                "标普500",
                "000500",
                "A",
                null,
                100m,
                100m,
                1.0m,
                Today.AddDays(-1),
                null,
                Guid.NewGuid(),
                HoldingDate: Today.AddDays(-1)),
            CancellationToken.None);
        await gold.SaveHoldingAsync(
            new SaveGoldHoldingRequest(5m, 2500m, 500m, Today.AddDays(-1), null, Guid.NewGuid(), 0, Today.AddDays(-1)),
            CancellationToken.None);
        await stocks.SaveHoldingAsync(
            new SaveStockHoldingRequest(StockMarket.Shanghai, "600519", "贵州茅台", 10m, 15000m, 1500m, Today.AddDays(-1), Today.AddDays(-1), null, Guid.NewGuid(), 0, null),
            CancellationToken.None);

        var refreshService = new QuoteRefreshService(
            context.Repository,
            funds,
            gold,
            stocks,
            context.Dates,
            new QuoteRefreshMonitor(),
            Options.Create(new QuoteRefreshOptions()),
            NullLogger<QuoteRefreshService>.Instance,
            fundProvider,
            goldProvider,
            stockQuoteProvider: null);

        var run = await refreshService.RefreshAsync(
            includeFunds: true,
            includeGold: true,
            includeStocks: false,
            QuoteRefreshScope.All,
            "scheduled",
            retryCount: 0,
            retryDelay: TimeSpan.Zero,
            CancellationToken.None);

        Assert.NotNull(run);
        Assert.Equal(1, run.FundSucceeded);
        Assert.Equal(1, run.GoldSucceeded);
        Assert.Equal(0, run.StockAttempted);
    }

    private static async Task<TestContext> CreateContextAsync(DateOnly startDate, decimal openingCash)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var cache = new ReplayDataCache();
        var repository = new LedgerRepository(db, cache);
        var dates = new BusinessDateProvider(new FixedTimeProvider(Now));
        var ledgerService = new LedgerService(repository, dates);
        var initialized = await ledgerService.InitializeAsync(
            new InitializeLedgerRequest(startDate, openingCash, startDate),
            CancellationToken.None);

        return new TestContext(db, cache, repository, dates, initialized.Overview!.Id);
    }

    private sealed record TestContext(
        AppDbContext Db,
        ReplayDataCache Cache,
        LedgerRepository Repository,
        BusinessDateProvider Dates,
        Guid LedgerId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return Db.DisposeAsync();
        }
    }

    private sealed class TestGoldQuotes : IGoldQuoteProvider
    {
        private readonly decimal _quote;
        private readonly DateOnly _date;
        private readonly DateTimeOffset _now;

        public TestGoldQuotes(decimal quote, DateOnly date, DateTimeOffset now)
        {
            _quote = quote;
            _date = date;
            _now = now;
        }

        public Task<ExternalGoldQuote> GetLatestAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ExternalGoldQuote(_quote, _date, "test", _now));
        }
    }
}

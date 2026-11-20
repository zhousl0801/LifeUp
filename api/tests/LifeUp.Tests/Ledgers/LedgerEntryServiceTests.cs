using LifeUp.Application.Ledgers;
using LifeUp.Domain.Ledgers;

namespace LifeUp.Tests.Ledgers;

public sealed class LedgerEntryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly BusinessToday = new(2026, 10, 21);

    [Fact]
    public async Task ImportHistoricalExpensesAsync_RecordsHistoricalSpendingWithoutChangingCash()
    {
        var (service, repository) = await CreateServiceAsync();
        var month = new DateOnly(2021, 5, 1);
        var request = new HistoricalExpenseImportRequest(Guid.NewGuid(), [new(month, "水果", 2150m)]);

        var result = await service.ImportHistoricalExpensesAsync(request, CancellationToken.None);
        var repeated = await service.ImportHistoricalExpensesAsync(
            request with { RequestId = Guid.NewGuid() }, CancellationToken.None);
        var period = await service.GetPeriodAsync(EntryPeriod.Month, month, null, null, null, CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Success, result.Status);
        Assert.Equal(1, result.Added);
        Assert.Equal(0, repeated.Added);
        Assert.Equal(1, repeated.Skipped);
        var entry = Assert.Single(repository.Entries);
        Assert.Equal(0m, entry.CashEffect);
        Assert.Equal(2150m, period!.Expense);
        Assert.Equal(1000m, repository.CashAccount!.Balance);

        var updated = await service.UpdateAsync(entry.Id, new(
            LedgerEntryKind.Expense, "水果", 2200m, month, entry.Note, Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(EntryOperationStatus.Success, updated.Status);
        Assert.Equal(1000m, updated.Result!.CashBalance);

        var reversed = await service.ReverseAsync(entry.Id, new(null, Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(EntryOperationStatus.Success, reversed.Status);
        Assert.Equal(1000m, reversed.Result!.CashBalance);
    }

    [Fact]
    public async Task ImportHistoricalExpensesAsync_RejectsExcludedMonthWithoutSavingAnyRows()
    {
        var (service, repository) = await CreateServiceAsync();
        var result = await service.ImportHistoricalExpensesAsync(new(Guid.NewGuid(),
            [new(new DateOnly(2021, 5, 1), "水果", 10m), new(new DateOnly(2026, 11, 1), "水果", 20m)]),
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Invalid, result.Status);
        Assert.Empty(repository.Entries);
    }

    [Fact]
    public async Task CreateAsync_WithExpense_ReducesCash()
    {
        var (service, repository) = await CreateServiceAsync();
        var result = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 35.60m, "午餐"),
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Created, result.Status);
        Assert.NotNull(result.Result);
        Assert.Equal(964.40m, result.Result.CashBalance);
        Assert.Equal(LedgerEntryKind.Expense, result.Result.Entry.Kind);
        Assert.Equal("午餐", result.Result.Entry.Note);
        Assert.Single(repository.Entries);
    }

    [Fact]
    public async Task CreateAsync_WithIncome_IncreasesCash()
    {
        var (service, _) = await CreateServiceAsync();
        var result = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Income, "工资", 1500m),
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Created, result.Status);
        Assert.NotNull(result.Result);
        Assert.Equal(2500m, result.Result.CashBalance);
    }

    [Fact]
    public async Task CreateAsync_WithExpenseAboveCash_ReturnsInvalid()
    {
        var (service, _) = await CreateServiceAsync(100m);
        var result = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 100.01m),
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Invalid, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("现金余额不足"));
    }

    [Fact]
    public async Task CreateAsync_WithFutureDate_ReturnsInvalid()
    {
        var (service, _) = await CreateServiceAsync();
        var result = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 10m, businessDate: BusinessToday.AddDays(1)),
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Invalid, result.Status);
        Assert.Contains("业务日期不能晚于今天。", result.Errors);
    }

    [Fact]
    public async Task CreateAsync_WithDateBeforeLedgerStart_IsAllowed()
    {
        var (service, _) = await CreateServiceAsync();
        var result = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 10m, businessDate: BusinessToday.AddDays(-1)),
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Created, result.Status);
        Assert.NotNull(result.Result);
        Assert.Equal(990m, result.Result.CashBalance);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidAmount_ReturnsInvalid()
    {
        var (service, _) = await CreateServiceAsync();

        var zero = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 0m),
            CancellationToken.None);
        var overPrecise = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 1.001m),
            CancellationToken.None);

        Assert.Contains("金额必须大于 0。", zero.Errors);
        Assert.Contains("金额最多保留两位小数。", overPrecise.Errors);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateRequestId_ReturnsExistingEntry()
    {
        var (service, repository) = await CreateServiceAsync();
        var requestId = Guid.NewGuid();
        var first = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 100m, requestId: requestId),
            CancellationToken.None);
        var second = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 100m, requestId: requestId),
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Success, second.Status);
        Assert.NotNull(first.Result);
        Assert.NotNull(second.Result);
        Assert.Equal(first.Result.Entry.Id, second.Result.Entry.Id);
        Assert.Equal(900m, second.Result.CashBalance);
        Assert.Single(repository.Entries);
    }

    [Fact]
    public async Task UpdateAsync_WithNewAmount_AdjustsCashByDifference()
    {
        var (service, _) = await CreateServiceAsync();
        var created = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 100m),
            CancellationToken.None);

        var result = await service.UpdateAsync(
            created.Result!.Entry.Id,
            new UpdateLedgerEntryRequest(LedgerEntryKind.Expense, "餐饮", 150m, BusinessToday, "晚餐", Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Success, result.Status);
        Assert.NotNull(result.Result);
        Assert.Equal(850m, result.Result.CashBalance);
        Assert.Equal(150m, result.Result.Entry.Amount);
        Assert.Equal("晚餐", result.Result.Entry.Note);
    }

    [Fact]
    public async Task UpdateAsync_ChangingExpenseToIncome_AdjustsCashBySum()
    {
        var (service, _) = await CreateServiceAsync();
        var created = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 100m),
            CancellationToken.None);

        var result = await service.UpdateAsync(
            created.Result!.Entry.Id,
            new UpdateLedgerEntryRequest(LedgerEntryKind.Income, "奖金", 100m, BusinessToday, null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Success, result.Status);
        Assert.NotNull(result.Result);
        Assert.Equal(1100m, result.Result.CashBalance);
        Assert.Equal(LedgerEntryKind.Income, result.Result.Entry.Kind);
    }

    [Fact]
    public async Task UpdateAsync_WithAmountAboveCash_ReturnsInvalid()
    {
        var (service, _) = await CreateServiceAsync(100m);
        var created = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 60m),
            CancellationToken.None);

        var result = await service.UpdateAsync(
            created.Result!.Entry.Id,
            new UpdateLedgerEntryRequest(LedgerEntryKind.Expense, "餐饮", 150m, BusinessToday, null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Invalid, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("现金余额不足"));
    }

    [Fact]
    public async Task ReverseAsync_WithExpense_RestoresCashAndExcludesEntryFromPeriod()
    {
        var (service, repository) = await CreateServiceAsync();
        var created = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 100m),
            CancellationToken.None);

        var result = await service.ReverseAsync(
            created.Result!.Entry.Id,
            new ReverseLedgerEntryRequest("重复记录", Guid.NewGuid()),
            CancellationToken.None);
        var period = await service.GetPeriodAsync(
            EntryPeriod.Month,
            BusinessToday,
            null,
            null,
            null,
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Success, result.Status);
        Assert.NotNull(result.Result);
        Assert.Equal(1000m, result.Result.CashBalance);
        Assert.Equal(LedgerEntryStatus.Reversed, result.Result.Entry.Status);
        Assert.Equal("重复记录", result.Result.Entry.ReversalReason);
        Assert.Single(repository.Entries);
        Assert.NotNull(period);
        Assert.Empty(period.Entries);
        Assert.Equal(0m, period.Expense);
    }

    [Fact]
    public async Task ReverseAsync_WithIncomeAlreadySpent_ReturnsInvalid()
    {
        var (service, _) = await CreateServiceAsync(100m);
        var income = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Income, "奖金", 100m),
            CancellationToken.None);
        await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "购物", 200m),
            CancellationToken.None);

        var result = await service.ReverseAsync(
            income.Result!.Entry.Id,
            new ReverseLedgerEntryRequest(null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Invalid, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("现金余额不足"));
    }

    [Fact]
    public async Task ReverseAsync_WhenAlreadyReversed_ReturnsConflict()
    {
        var (service, _) = await CreateServiceAsync();
        var created = await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 100m),
            CancellationToken.None);

        await service.ReverseAsync(
            created.Result!.Entry.Id,
            new ReverseLedgerEntryRequest(null, Guid.NewGuid()),
            CancellationToken.None);
        var result = await service.ReverseAsync(
            created.Result.Entry.Id,
            new ReverseLedgerEntryRequest(null, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(EntryOperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task GetPeriodAsync_ReturnsSummaryAndAppliesFilters()
    {
        var (service, _) = await CreateServiceAsync(1000m, BusinessToday.AddDays(-5));

        await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Income, "工资", 1000m, businessDate: BusinessToday),
            CancellationToken.None);
        await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "餐饮", 100m, "午餐", BusinessToday),
            CancellationToken.None);
        await service.CreateAsync(
            CreateRequest(LedgerEntryKind.Expense, "购物", 200m, businessDate: BusinessToday.AddDays(-1)),
            CancellationToken.None);

        var period = await service.GetPeriodAsync(
            EntryPeriod.Month,
            BusinessToday,
            null,
            null,
            null,
            CancellationToken.None);
        var expenseOnly = await service.GetPeriodAsync(
            EntryPeriod.Month,
            BusinessToday,
            LedgerEntryKind.Expense,
            null,
            null,
            CancellationToken.None);
        var foodOnly = await service.GetPeriodAsync(
            EntryPeriod.Month,
            BusinessToday,
            null,
            "餐饮",
            null,
            CancellationToken.None);
        var searchOnly = await service.GetPeriodAsync(
            EntryPeriod.Month,
            BusinessToday,
            null,
            null,
            "午餐",
            CancellationToken.None);

        Assert.NotNull(period);
        Assert.Equal(1000m, period.Income);
        Assert.Equal(300m, period.Expense);
        Assert.Equal(700m, period.Surplus);
        Assert.Equal(3, period.Entries.Count);

        Assert.NotNull(expenseOnly);
        Assert.Equal(300m, expenseOnly.Expense);
        Assert.Equal(2, expenseOnly.Entries.Count);

        Assert.NotNull(foodOnly);
        Assert.Single(foodOnly.Entries);
        Assert.Equal("餐饮", foodOnly.Entries[0].Category);

        Assert.NotNull(searchOnly);
        Assert.Single(searchOnly.Entries);
        Assert.Equal("午餐", searchOnly.Entries[0].Note);
    }

    [Fact]
    public async Task GetPeriodAsync_WithoutLedger_ReturnsNull()
    {
        var repository = new FakeLedgerRepository();
        var service = new LedgerEntryService(
            repository,
            new BusinessDateProvider(new FixedTimeProvider(Now)));

        var period = await service.GetPeriodAsync(
            EntryPeriod.Month,
            BusinessToday,
            null,
            null,
            null,
            CancellationToken.None);

        Assert.Null(period);
    }

    private static async Task<(LedgerEntryService Service, FakeLedgerRepository Repository)> CreateServiceAsync(
        decimal openingBalance = 1000m,
        DateOnly? startDate = null)
    {
        var repository = new FakeLedgerRepository();
        var businessDateProvider = new BusinessDateProvider(new FixedTimeProvider(Now));
        var ledgerService = new LedgerService(repository, businessDateProvider);

        await ledgerService.InitializeAsync(
            new InitializeLedgerRequest(startDate ?? BusinessToday, openingBalance),
            CancellationToken.None);

        return (new LedgerEntryService(repository, businessDateProvider), repository);
    }

    private static CreateLedgerEntryRequest CreateRequest(
        LedgerEntryKind kind,
        string category,
        decimal amount,
        string? note = null,
        DateOnly? businessDate = null,
        Guid? requestId = null)
    {
        return new CreateLedgerEntryRequest(
            kind,
            category,
            amount,
            businessDate ?? BusinessToday,
            note,
            requestId ?? Guid.NewGuid());
    }
}

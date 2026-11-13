using LifeUp.Application.Capital;
using LifeUp.Application.Ledgers;
using LifeUp.Domain.Assets;
using LifeUp.Domain.Cash;
using LifeUp.Domain.Ledgers;
using LifeUp.Tests.Ledgers;

namespace LifeUp.Tests.Capital;

public sealed class CapitalDefaultResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 12);

    [Fact]
    public async Task ResolveAsync_UsesCompleteMonthsForIncomeExpenseAndProvident()
    {
        var repository = new FakeLedgerRepository();
        var ledger = Ledger.Create(new DateOnly(2026, 8, 1), Today, Now);
        await SeedLedgerAsync(repository, ledger);

        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Income, 9_000m, new DateOnly(2026, 8, 5));
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Income, 10_000m, new DateOnly(2026, 9, 5));
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Income, 11_000m, new DateOnly(2026, 10, 5));
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Expense, 5_000m, new DateOnly(2026, 8, 6));
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Expense, 5_000m, new DateOnly(2026, 9, 6));
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Expense, 5_000m, new DateOnly(2026, 10, 6));
        await AddDepositAsync(repository, ledger.Id, 1_000m, new DateOnly(2026, 8, 10));
        await AddDepositAsync(repository, ledger.Id, 2_000m, new DateOnly(2026, 9, 10));
        await AddDepositAsync(repository, ledger.Id, 3_000m, new DateOnly(2026, 10, 10));

        var resolver = CreateResolver(repository);
        var result = await resolver.ResolveAsync(ledger, null, CancellationToken.None);

        Assert.Equal(10_000m, result.MonthlyIncome.Value);
        Assert.Equal("history", result.MonthlyIncome.Source);
        Assert.Equal(5_000m, result.MonthlyExpense.Value);
        Assert.Equal("history", result.MonthlyExpense.Source);
        Assert.Equal(2_000m, result.MonthlyProvidentDeposit.Value);
        Assert.Equal("history", result.MonthlyProvidentDeposit.Source);
        Assert.Equal(0m, result.IncomeGrowthPercent.Value);
        Assert.Equal("default", result.IncomeGrowthPercent.Source);
        Assert.Equal(1.5m, result.ProvidentAnnualReturnPercent.Value);
        Assert.Equal("default", result.ProvidentAnnualReturnPercent.Source);
    }

    [Fact]
    public async Task ResolveAsync_WithoutCompleteMonths_UsesDefaults()
    {
        var repository = new FakeLedgerRepository();
        var ledger = Ledger.Create(new DateOnly(2026, 11, 1), Today, Now);
        await SeedLedgerAsync(repository, ledger);

        var result = await CreateResolver(repository).ResolveAsync(ledger, null, CancellationToken.None);

        Assert.Equal(0m, result.MonthlyIncome.Value);
        Assert.Equal("default", result.MonthlyIncome.Source);
        Assert.Equal(0m, result.MonthlyExpense.Value);
        Assert.Equal("default", result.MonthlyExpense.Source);
        Assert.Equal(0m, result.MonthlyProvidentDeposit.Value);
        Assert.Equal("default", result.MonthlyProvidentDeposit.Source);
    }

    [Fact]
    public async Task ResolveAsync_UserValuesTakePrecedence()
    {
        var repository = new FakeLedgerRepository();
        var ledger = Ledger.Create(new DateOnly(2026, 8, 1), Today, Now);
        await SeedLedgerAsync(repository, ledger);
        var goal = new CapitalGoal(null, 12_000m, 7_200m, 2_000m, 3m, 2m, 5m, 1.5m);

        var result = await CreateResolver(repository).ResolveAsync(ledger, goal, CancellationToken.None);

        Assert.Equal(12_000m, result.MonthlyIncome.Value);
        Assert.Equal("user", result.MonthlyIncome.Source);
        Assert.Equal(7_200m, result.MonthlyExpense.Value);
        Assert.Equal("user", result.MonthlyExpense.Source);
        Assert.Equal(2_000m, result.MonthlyProvidentDeposit.Value);
        Assert.Equal("user", result.MonthlyProvidentDeposit.Source);
        Assert.Equal(3m, result.IncomeGrowthPercent.Value);
        Assert.Equal("user", result.IncomeGrowthPercent.Source);
        Assert.Equal(5m, result.InvestmentAnnualReturnPercent.Value);
        Assert.Equal("user", result.InvestmentAnnualReturnPercent.Source);
    }

    [Fact]
    public async Task ResolveAsync_ExcludesPartialStartMonth()
    {
        var repository = new FakeLedgerRepository();
        var ledger = Ledger.Create(new DateOnly(2026, 8, 21), Today, Now);
        await SeedLedgerAsync(repository, ledger);
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Income, 100_000m, new DateOnly(2026, 8, 25));
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Income, 9_000m, new DateOnly(2026, 9, 5));
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Income, 10_000m, new DateOnly(2026, 10, 25));

        var result = await CreateResolver(repository).ResolveAsync(ledger, null, CancellationToken.None);

        Assert.Equal(9_500m, result.MonthlyIncome.Value);
        Assert.Equal("history", result.MonthlyIncome.Source);
    }

    private static CapitalDefaultResolver CreateResolver(FakeLedgerRepository repository)
    {
        return new CapitalDefaultResolver(repository, new BusinessDateProvider(new FixedTimeProvider(Now)));
    }

    private static Task SeedLedgerAsync(FakeLedgerRepository repository, Ledger ledger)
    {
        return repository.AddAsync(
            ledger,
            CashAccount.Create(ledger.Id, 0m, ledger.StartDate, Now),
            CancellationToken.None);
    }

    private static Task AddEntryAsync(
        FakeLedgerRepository repository,
        Guid ledgerId,
        LedgerEntryKind kind,
        decimal amount,
        DateOnly date)
    {
        var category = kind == LedgerEntryKind.Income ? "工资" : "餐饮";
        var entry = LedgerEntry.Create(
            ledgerId,
            kind,
            category,
            amount,
            date,
            Today,
            null,
            LedgerEntrySource.Manual,
            Guid.NewGuid(),
            Now);

        return repository.AddEntryAsync(entry, CancellationToken.None);
    }

    private static Task AddDepositAsync(
        FakeLedgerRepository repository,
        Guid ledgerId,
        decimal amount,
        DateOnly date)
    {
        var transaction = AssetTransaction.CreateProvidentDeposit(
            ledgerId,
            amount,
            date,
            "缴存",
            Guid.NewGuid(),
            Now);

        return repository.AddAssetTransactionAsync(transaction, CancellationToken.None);
    }
}

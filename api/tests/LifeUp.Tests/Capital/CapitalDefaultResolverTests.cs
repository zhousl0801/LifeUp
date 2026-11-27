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

    [Fact]
    public async Task ResolveAsync_FollowsSalaryProfile()
    {
        var repository = new FakeLedgerRepository();
        var ledger = Ledger.Create(new DateOnly(2026, 8, 1), Today, Now);
        await SeedLedgerAsync(repository, ledger);
        ledger.SetSalaryProfile(new SalaryProfile(20_000m, 12_000m, 4_800m, 4m, 60_000m, 1, 15), Now);
        var goal = new CapitalGoal(
            1_000_000m, 99_000m, 5_000m, 9_999m, 0m, 0m, 5m, 1.5m,
            AnnualBonus: 1m, BonusPayoutMonth: 12, UseSalaryProfile: true);

        var result = await CreateResolver(repository).ResolveAsync(ledger, goal, CancellationToken.None);

        Assert.Equal(12_000m, result.MonthlyIncome.Value);
        Assert.Equal("salary", result.MonthlyIncome.Source);
        Assert.Equal(4_800m, result.MonthlyProvidentDeposit.Value);
        Assert.Equal("salary", result.MonthlyProvidentDeposit.Source);
        Assert.Equal(60_000m, result.AnnualBonus.Value);
        Assert.Equal("salary", result.AnnualBonus.Source);
        Assert.Equal(1m, result.BonusPayoutMonth.Value);
        Assert.Equal(5_000m, result.MonthlyExpense.Value);
        Assert.Equal("user", result.MonthlyExpense.Source);
    }

    [Fact]
    public async Task ResolveAsync_ProfileWithZeroBonusMonths_IgnoresBonusNet()
    {
        var repository = new FakeLedgerRepository();
        var ledger = Ledger.Create(new DateOnly(2026, 8, 1), Today, Now);
        await SeedLedgerAsync(repository, ledger);
        ledger.SetSalaryProfile(new SalaryProfile(20_000m, 12_000m, 4_800m, 0m, 60_000m, 1, 15), Now);

        var result = await CreateResolver(repository).ResolveAsync(ledger, null, CancellationToken.None);

        Assert.Equal(0m, result.AnnualBonus.Value);
        Assert.Equal("salary", result.AnnualBonus.Source);
    }

    [Fact]
    public async Task ResolveAsync_ManualGoalOverridesProfile()
    {
        var repository = new FakeLedgerRepository();
        var ledger = Ledger.Create(new DateOnly(2026, 8, 1), Today, Now);
        await SeedLedgerAsync(repository, ledger);
        ledger.SetSalaryProfile(new SalaryProfile(20_000m, 12_000m, 4_800m, 4m, 60_000m, 1, 15), Now);
        var goal = new CapitalGoal(
            1_000_000m, 99_000m, 5_000m, 9_999m, 0m, 0m, 5m, 1.5m,
            AnnualBonus: 1m, BonusPayoutMonth: 12);

        var result = await CreateResolver(repository).ResolveAsync(ledger, goal, CancellationToken.None);

        Assert.Equal(99_000m, result.MonthlyIncome.Value);
        Assert.Equal("user", result.MonthlyIncome.Source);
        Assert.Equal(9_999m, result.MonthlyProvidentDeposit.Value);
        Assert.Equal(1m, result.AnnualBonus.Value);
        Assert.Equal(12m, result.BonusPayoutMonth.Value);
    }

    [Fact]
    public async Task ResolveAsync_ProfileMissing_FallsBackToHistory()
    {
        var repository = new FakeLedgerRepository();
        var ledger = Ledger.Create(new DateOnly(2026, 8, 1), Today, Now);
        await SeedLedgerAsync(repository, ledger);
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Income, 10_000m, new DateOnly(2026, 8, 5));
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Income, 10_000m, new DateOnly(2026, 9, 5));
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Income, 10_000m, new DateOnly(2026, 10, 5));
        var goal = new CapitalGoal(
            1_000_000m, null, 5_000m, null, 0m, 0m, 5m, 1.5m,
            UseSalaryProfile: true);

        var result = await CreateResolver(repository).ResolveAsync(ledger, goal, CancellationToken.None);

        Assert.Equal(10_000m, result.MonthlyIncome.Value);
        Assert.Equal("history", result.MonthlyIncome.Source);
        Assert.Equal(0m, result.AnnualBonus.Value);
        Assert.Equal("default", result.AnnualBonus.Source);
        Assert.Equal(1m, result.BonusPayoutMonth.Value);
    }

    [Fact]
    public async Task ResolveAsync_WithoutGoal_UsesProfile()
    {
        var repository = new FakeLedgerRepository();
        var ledger = Ledger.Create(new DateOnly(2026, 8, 1), Today, Now);
        await SeedLedgerAsync(repository, ledger);
        ledger.SetSalaryProfile(new SalaryProfile(20_000m, 12_000m, 4_800m, 4m, 60_000m, 1, 15), Now);

        var result = await CreateResolver(repository).ResolveAsync(ledger, null, CancellationToken.None);

        Assert.Equal(12_000m, result.MonthlyIncome.Value);
        Assert.Equal("salary", result.MonthlyIncome.Source);
    }

    [Fact]
    public async Task ResolveWithExpensesAsync_UsesTwelveFullMonthsAndClassifiesExpenseCategories()
    {
        var repository = new FakeLedgerRepository();
        var ledger = Ledger.Create(new DateOnly(2025, 8, 21), Today, Now);
        await SeedLedgerAsync(repository, ledger);
        var fixedGroup = await AddCategoryAsync("固定", isGroup: true);
        var necessaryGroup = await AddCategoryAsync("必要", isGroup: true);
        var leisureGroup = await AddCategoryAsync("弹性", isGroup: true);
        await AddCategoryAsync("停车", fixedGroup.Id);
        await AddCategoryAsync("宠物", necessaryGroup.Id);
        await AddCategoryAsync("日用", leisureGroup.Id);
        var oldCategory = await AddCategoryAsync("旧分类", leisureGroup.Id);
        oldCategory.SetActive(false, Now);
        await AddCategoryAsync("未使用", fixedGroup.Id);
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Expense, 7_000m, new DateOnly(2025, 10, 31), "旧分类");
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Expense, 1_200m, new DateOnly(2025, 11, 1), "停车");
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Expense, 600m, new DateOnly(2026, 2, 28), "宠物");
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Expense, 2_400m, new DateOnly(2026, 10, 31), "日用");
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Expense, 300m, new DateOnly(2026, 10, 30), "餐饮");
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Expense, 500m, new DateOnly(2026, 11, 1), "日用");
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Expense, 1_000m, new DateOnly(2026, 11, 1), "日用", reversed: true);
        var goal = new CapitalGoal(null, null, null, null, null, null, null, null,
            ExcludedExpenseCategories: [" 停车 "]);

        var resolved = await CreateResolver(repository).ResolveWithExpensesAsync(ledger, goal, CancellationToken.None);

        Assert.Equal(12, resolved.ExpenseWindowMonths);
        Assert.Equal(375m, resolved.Effective.MonthlyExpense.Value);
        Assert.Equal(275m, resolved.StandardMonthlyExpense);
        Assert.Equal("history", resolved.StandardExpenseSource);
        Assert.Equal(75m, resolved.LeanMonthlyExpense);
        Assert.Equal("history", resolved.LeanExpenseSource);
        Assert.Equal(500m, resolved.ActualMonthExpense);
        Assert.Equal(["日用", "停车", "宠物", "餐饮", "旧分类", "未使用"],
            resolved.Categories.Select(category => category.Name));
        Assert.Equal(new FireExpenseCategoryDto("日用", "弹性", 200m, false, false, false), resolved.Categories[0]);
        Assert.Equal(new FireExpenseCategoryDto("停车", "固定", 100m, true, false, true), resolved.Categories[1]);
        Assert.Equal(new FireExpenseCategoryDto("宠物", "必要", 50m, true, true, false), resolved.Categories[2]);
        Assert.Equal(new FireExpenseCategoryDto("餐饮", null, 25m, true, true, false), resolved.Categories[3]);

        var explicitLean = await CreateResolver(repository).ResolveWithExpensesAsync(
            ledger, goal with { LeanExpenseCategories = ["停车", "日用"] }, CancellationToken.None);
        Assert.Equal(200m, explicitLean.LeanMonthlyExpense);
        var overrides = await CreateResolver(repository).ResolveWithExpensesAsync(ledger, goal with
        {
            MonthlyExpense = 5_000m,
            FireMonthlyExpenseOverride = 4_000m,
            LeanMonthlyExpenseOverride = 0m
        }, CancellationToken.None);
        Assert.Equal(5_000m, overrides.Effective.MonthlyExpense.Value);
        Assert.Equal(4_000m, overrides.StandardMonthlyExpense);
        Assert.Equal("user", overrides.StandardExpenseSource);
        Assert.Equal(0m, overrides.LeanMonthlyExpense);
        Assert.Equal("user", overrides.LeanExpenseSource);

        async Task<EntryCategory> AddCategoryAsync(string name, Guid? parent = null, bool isGroup = false)
        {
            var category = EntryCategory.Create(ledger.Id, LedgerEntryKind.Expense, name, 0, null, Now, parent, isGroup);
            await repository.AddCategoryAsync(category, CancellationToken.None);
            return category;
        }
    }

    [Fact]
    public async Task ResolveWithExpensesAsync_UsesExpenseFallbacksWithoutACompleteMonth()
    {
        var repository = new FakeLedgerRepository();
        var ledger = Ledger.Create(new DateOnly(2026, 10, 2), Today, Now);
        await SeedLedgerAsync(repository, ledger);
        await AddEntryAsync(repository, ledger.Id, LedgerEntryKind.Expense, 500m, new DateOnly(2026, 10, 5));
        var goal = new CapitalGoal(null, null, 3_303.33m, null, null, null, null, null);

        var resolved = await CreateResolver(repository).ResolveWithExpensesAsync(ledger, goal, CancellationToken.None);

        Assert.Equal(0, resolved.ExpenseWindowMonths);
        Assert.Equal(3_303.33m, resolved.StandardMonthlyExpense);
        Assert.Equal("default", resolved.StandardExpenseSource);
        Assert.Equal(2_312.33m, resolved.LeanMonthlyExpense);
        Assert.Equal("default", resolved.LeanExpenseSource);
        Assert.Equal(0m, Assert.Single(resolved.Categories).MonthlyAverage);
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
        DateOnly date,
        string? category = null,
        bool reversed = false)
    {
        category ??= kind == LedgerEntryKind.Income ? "工资" : "餐饮";
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

        if (reversed)
        {
            entry.Reverse("测试冲销", Now);
        }

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

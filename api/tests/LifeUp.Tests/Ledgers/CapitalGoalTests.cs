using System.Text.Json;
using LifeUp.Domain.Ledgers;

namespace LifeUp.Tests.Ledgers;

public sealed class CapitalGoalTests
{
    [Fact]
    public void SetCapitalGoal_RoundTripsAndClears()
    {
        var now = new DateTimeOffset(2026, 11, 12, 2, 0, 0, TimeSpan.Zero);
        var ledger = Ledger.Create(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 12), now);
        var goal = new CapitalGoal(
            1_000_000m,
            12_000m,
            7_200m,
            2_000m,
            3m,
            2m,
            5m,
            1.5m);

        Assert.True(ledger.SetCapitalGoal(goal, now));
        Assert.Equal(goal, ledger.GetCapitalGoal());
        Assert.True(ledger.HasValidCapitalGoal());

        Assert.True(ledger.SetCapitalGoal(null, now));
        Assert.Null(ledger.GetCapitalGoal());
        Assert.True(ledger.HasValidCapitalGoal());
    }

    [Fact]
    public void SetCapitalGoal_WithInvalidGoal_Throws()
    {
        var now = new DateTimeOffset(2026, 11, 12, 2, 0, 0, TimeSpan.Zero);
        var ledger = Ledger.Create(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 12), now);
        var goal = new CapitalGoal(0m, null, null, null, null, null, null, null);

        Assert.Throws<ArgumentException>(() => ledger.SetCapitalGoal(goal, now));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(1.234, true)]
    [InlineData(1_000_000_000.01, true)]
    [InlineData(1_000_000, false)]
    public void TryValidate_TargetAmountBounds(decimal target, bool invalid)
    {
        var goal = new CapitalGoal(target, null, null, null, null, null, null, null);

        var result = CapitalGoal.TryValidate(goal, out var errors);

        Assert.Equal(!invalid, result);
        Assert.Equal(invalid, errors.Count > 0);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(1.234, true)]
    [InlineData(1_000_000_000.01, true)]
    [InlineData(0, false)]
    [InlineData(12_000, false)]
    public void TryValidate_MonthlyAmountsBounds(decimal amount, bool invalid)
    {
        var goal = new CapitalGoal(1_000_000m, amount, null, null, null, null, null, null);

        var result = CapitalGoal.TryValidate(goal, out var errors);

        Assert.Equal(!invalid, result);
        Assert.Equal(invalid, errors.Count > 0);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(100.01)]
    [InlineData(4.567)]
    public void TryValidate_PercentBounds(decimal percent)
    {
        var goal = new CapitalGoal(1_000_000m, null, null, null, percent, null, null, null);

        Assert.False(CapitalGoal.TryValidate(goal, out var errors));
        Assert.NotEmpty(errors);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(1.234, true)]
    [InlineData(0, false)]
    [InlineData(60_000, false)]
    public void TryValidate_AnnualBonusBounds(decimal amount, bool invalid)
    {
        var goal = new CapitalGoal(
            1_000_000m, null, null, null, null, null, null, null,
            AnnualBonus: amount);

        var result = CapitalGoal.TryValidate(goal, out var errors);

        Assert.Equal(!invalid, result);
        Assert.Equal(invalid, errors.Count > 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void TryValidate_RejectsInvalidBonusPayoutMonth(int month)
    {
        var goal = new CapitalGoal(
            1_000_000m, null, null, null, null, null, null, null,
            BonusPayoutMonth: month);

        Assert.False(CapitalGoal.TryValidate(goal, out var errors));
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Deserialize_LegacyGoalJson_UsesDefaults()
    {
        const string json = "{\"targetAmount\":1000000,\"monthlyIncome\":12000,\"monthlyExpense\":7200,\"monthlyProvidentDeposit\":2000,\"incomeGrowthPercent\":3,\"expenseGrowthPercent\":2,\"investmentAnnualReturnPercent\":5,\"providentAnnualReturnPercent\":1.5}";

        var goal = JsonSerializer.Deserialize<CapitalGoal>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(goal);
        Assert.Null(goal.AnnualBonus);
        Assert.Null(goal.BonusPayoutMonth);
        Assert.False(goal.UseSalaryProfile);
        Assert.Equal("fire", goal.GoalMode);
        Assert.Equal("standard", goal.FireMode);
        Assert.Null(goal.SafeWithdrawalRatePercent);
        Assert.Null(goal.ExcludedExpenseCategories);
        Assert.Null(goal.LeanExpenseCategories);
        Assert.Null(goal.FireMonthlyExpenseOverride);
        Assert.Null(goal.LeanMonthlyExpenseOverride);
        Assert.Null(goal.CoastTargetYear);
        Assert.True(CapitalGoal.TryValidate(goal, out _));
    }

    [Fact]
    public void SetCapitalGoal_NormalizesListsAndDefaultModes()
    {
        var now = new DateTimeOffset(2026, 11, 12, 2, 0, 0, TimeSpan.Zero);
        var ledger = Ledger.Create(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 12), now);
        var goal = new CapitalGoal(null, null, null, null, null, null, null, null,
            GoalMode: " ", FireMode: "", ExcludedExpenseCategories: [" 租金 ", "租金", " ", "A", "a"],
            LeanExpenseCategories: []);

        Assert.True(ledger.SetCapitalGoal(goal, now));
        Assert.False(ledger.SetCapitalGoal(goal, now));

        var persisted = ledger.GetCapitalGoal();
        Assert.NotNull(persisted);
        Assert.Equal("fire", persisted.GoalMode);
        Assert.Equal("standard", persisted.FireMode);
        Assert.Equal(["租金", "A", "a"], persisted.ExcludedExpenseCategories);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(persisted.LeanExpenseCategories));
    }

    [Theory]
    [InlineData(0.99)]
    [InlineData(15.01)]
    [InlineData(4.001)]
    public void TryValidate_RejectsInvalidWithdrawalRate(decimal rate)
    {
        var goal = EmptyGoal() with { SafeWithdrawalRatePercent = rate };

        Assert.False(CapitalGoal.TryValidate(goal, out _));
    }

    [Fact]
    public void TryValidate_EnforcesFireModesExpensesYearAndCategories()
    {
        var goal = EmptyGoal();

        Assert.True(CapitalGoal.TryValidate(goal with
        {
            GoalMode = "custom",
            FireMode = "lean",
            SafeWithdrawalRatePercent = 1m,
            FireMonthlyExpenseOverride = 0m,
            LeanMonthlyExpenseOverride = LedgerEntry.MaximumAmount,
            CoastTargetYear = 2200,
            LeanExpenseCategories = Enumerable.Repeat("餐饮", 101).ToArray()
        }, out _));
        Assert.False(CapitalGoal.TryValidate(goal with { GoalMode = "FIRE" }, out _));
        Assert.False(CapitalGoal.TryValidate(goal with { FireMode = "custom" }, out _));
        Assert.False(CapitalGoal.TryValidate(goal with { FireMonthlyExpenseOverride = -1m }, out _));
        Assert.False(CapitalGoal.TryValidate(goal with { FireMonthlyExpenseOverride = 1.001m }, out _));
        Assert.False(CapitalGoal.TryValidate(goal with { LeanMonthlyExpenseOverride = LedgerEntry.MaximumAmount + 1m }, out _));
        Assert.False(CapitalGoal.TryValidate(goal with { CoastTargetYear = 2024 }, out _));
        Assert.False(CapitalGoal.TryValidate(goal with { CoastTargetYear = 2201 }, out _));
        Assert.False(CapitalGoal.TryValidate(goal with { ExcludedExpenseCategories = [new string('类', 21)] }, out _));
        Assert.False(CapitalGoal.TryValidate(goal with
        {
            LeanExpenseCategories = Enumerable.Range(0, 101).Select(index => $"分类{index}").ToArray()
        }, out _));
    }

    private static CapitalGoal EmptyGoal() => new(null, null, null, null, null, null, null, null);

    [Fact]
    public void TryValidate_AllowsNullAssumptions()
    {
        var goal = new CapitalGoal(null, null, null, null, null, null, null, null);

        Assert.True(CapitalGoal.TryValidate(goal, out var errors));
        Assert.Empty(errors);
    }
}

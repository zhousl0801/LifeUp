using LifeUp.Application.Capital;

namespace LifeUp.Tests.Capital;

public sealed class FireInsightBuilderTests
{
    private static readonly DateOnly AsOf = new(2026, 11, 12);

    [Theory]
    [InlineData(0, 0, "待设定", "完整月")]
    [InlineData(1_000, 10_000, "起步期", "10.0 个月")]
    [InlineData(1_000, 30_000, "筑基期", "¥3.29")]
    [InlineData(1_000, 75_000, "加速期", "半退休")]
    [InlineData(1_000, 150_000, "半自由期", "50%")]
    [InlineData(1_000, 240_000, "冲刺期", "1 年 8 个月")]
    [InlineData(1_000, 300_000, "已自由", "全部目标开销")]
    public void Build_DescribesEveryStage(decimal expense, decimal assets, string stage, string headlineFragment)
    {
        var input = CreateInput(cash: assets, expense: expense);

        var insights = Build(input);

        Assert.Equal(stage, insights.Stage);
        Assert.Contains(headlineFragment, insights.Headline);
        if (expense == 0m)
        {
            Assert.Empty(insights.CoveredCategories);
            Assert.Empty(insights.Suggestions);
        }
    }

    [Fact]
    public void Build_MatchesLeanFirstThenOtherAffordableCategories()
    {
        var input = CreateInput(cash: 90_000m, expense: 1_000m);
        var categories = new FireExpenseCategoryDto[]
        {
            new("较贵刚性", "固定", 400m, true, true, false),
            new("房租", "固定", 150m, true, true, false),
            new("日用", "必要", 110m, true, true, false),
            new("娱乐", null, 250m, false, false, false),
            new("阅读", null, 40m, false, false, false),
            new("礼物", null, 10m, false, false, true),
            new("交通", null, 0m, true, true, false)
        };

        var insights = Build(input, categories: categories);

        Assert.Equal(["房租", "日用", "阅读"], insights.CoveredCategories);
        Assert.Contains("¥300", insights.Headline);
        Assert.Contains("房租 + 日用 + 阅读", insights.Headline);
        Assert.Contains("1 年 8 个月", insights.Headline);
    }

    [Fact]
    public void Build_ProvidesThreeHighestPrioritySuggestions()
    {
        var input = CreateInput(investment: 100_000m, expense: 3_000m, income: 7_500m, investmentReturn: 6m);

        var insights = Build(input, activeMode: "standard", leanExpense: 2_000m, coastYear: 2051, savingsRate: 60m);

        Assert.Equal(["支出杠杆", "阶段捷径", "Coast 捷径"], insights.Suggestions.Select(item => item.Badge));
        Assert.Contains("¥500", insights.Suggestions[0].Text);
        Assert.Contains("¥15 万", insights.Suggestions[0].Text);
        Assert.Contains("提前", insights.Suggestions[0].Text);
        Assert.Contains("¥2,000/月", insights.Suggestions[1].Text);
        Assert.Contains("可提前", insights.Suggestions[1].Text);
        Assert.Contains("2051 年", insights.Suggestions[2].Text);
    }

    [Theory]
    [InlineData(100_000, 0, 0, "组合年化按 4%")]
    [InlineData(0, 100_000, 0, "投资年化按 4%")]
    [InlineData(0, 100_000, 3, "投资年化提高 1 个百分点")]
    public void Build_QuantifiesReturnTrialAndRecognizesHighSavings(
        decimal cash, decimal investment, decimal investmentReturn, string returnAssumption)
    {
        var input = CreateInput(cash, investment, expense: 1_000m, investmentReturn: investmentReturn);

        var insights = Build(input, savingsRate: 50m);

        Assert.Equal(["支出杠杆", "收益试算", "高储蓄率"], insights.Suggestions.Select(item => item.Badge));
        Assert.Contains("¥200", insights.Suggestions[0].Text);
        Assert.Contains(returnAssumption, insights.Suggestions[1].Text);
        Assert.Contains("提前", insights.Suggestions[1].Text);
        Assert.Contains("50%", insights.Suggestions[2].Text);
    }

    [Fact]
    public void Build_WithReachedGoal_OmitsExpenseAndReturnTrials()
    {
        var input = CreateInput(cash: 350_000m, expense: 1_000m);

        var insights = Build(input, savingsRate: 65m);

        Assert.Equal("高储蓄率", Assert.Single(insights.Suggestions).Badge);
    }

    private static CapitalProjectionInput CreateInput(
        decimal cash = 0m,
        decimal investment = 0m,
        decimal expense = 1_000m,
        decimal income = 4_000m,
        decimal investmentReturn = 0m)
    {
        return new CapitalProjectionInput(
            AsOf, cash, investment, 0m, null, income, expense, 0m, 0m, 0m, investmentReturn, 0m,
            FireMonthlyExpense: expense, SafeWithdrawalRatePercent: 4m);
    }

    private static FireInsightsDto Build(
        CapitalProjectionInput input,
        string activeMode = "lean",
        decimal? leanExpense = null,
        int coastYear = 2026,
        decimal savingsRate = 0m,
        IReadOnlyList<FireExpenseCategoryDto>? categories = null)
    {
        var calculator = new CapitalProjectionCalculator();
        var leanInput = input with { FireMonthlyExpense = leanExpense ?? input.FireMonthlyExpense };
        var standardProjection = calculator.CalculateFire(input);
        var leanProjection = calculator.CalculateFire(leanInput);
        var standard = ToMode(input, standardProjection);
        var lean = ToMode(leanInput, leanProjection);
        var active = activeMode == "lean" ? lean : standard;
        var activeInput = activeMode == "lean" ? leanInput : input;
        var activeProjection = activeMode == "lean" ? leanProjection : standardProjection;
        var coast = CapitalProjectionCalculator.CalculateCoast(activeInput, activeProjection, coastYear);
        var current = new CapitalCurrentDto(AsOf, input.Cash + input.Investment, input.Cash, input.Investment, 0m, false, null);
        var milestones = FireMilestoneBuilder.Build(current, [], activeMode, active, lean, coast, activeProjection);

        return FireInsightBuilder.Build(calculator, activeInput, current, activeMode, standard, lean, coast,
            savingsRate, Math.Round(current.Total * 0.04m / 12m, 2), categories ?? [], milestones);
    }

    private static FireModeDto ToMode(CapitalProjectionInput input, CapitalProjectionResult projection)
    {
        var expense = input.FireMonthlyExpense.GetValueOrDefault();
        return new FireModeDto(expense, "user", expense * 12m, projection.TargetAmount ?? 0m, 0m,
            projection.ProgressPercent, projection.Status, projection.EtaMonth, projection.MonthsRemaining);
    }
}

using LifeUp.Application.Plans;
using LifeUp.Domain.Plans;

namespace LifeUp.Tests.Plans;

public sealed class PlanScheduleTests
{
    [Fact]
    public void MonthlyPlan_UsesLastDayWhenMonthIsShorter()
    {
        var plan = Plan(AutoPlanCycle.Monthly, dayOfMonth: 31, startDate: new DateOnly(2026, 1, 1));

        var periods = PlanSchedule.GetDuePeriods(plan, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));

        var period = Assert.Single(periods);
        Assert.Equal(new DateOnly(2026, 2, 28), period.DueDate);
        Assert.Equal("2026-02", period.PeriodKey);
    }

    [Fact]
    public void WeeklyPlan_UsesConfiguredWeekday()
    {
        var plan = Plan(AutoPlanCycle.Weekly, dayOfWeek: 1, startDate: new DateOnly(2026, 10, 1));

        var periods = PlanSchedule.GetDuePeriods(plan, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        Assert.Equal(4, periods.Count);
        Assert.All(periods, period => Assert.Equal(DayOfWeek.Monday, period.DueDate.DayOfWeek));
        Assert.Equal("W2026-10-05", periods[0].PeriodKey);
    }

    [Fact]
    public void MonthlyPlan_SkipsPeriodsBeforeGenerateFrom()
    {
        var plan = Plan(AutoPlanCycle.Monthly, dayOfMonth: 5, startDate: new DateOnly(2026, 10, 15));

        var periods = PlanSchedule.GetDuePeriods(plan, new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 30));

        var period = Assert.Single(periods);
        Assert.Equal(new DateOnly(2026, 11, 5), period.DueDate);
    }

    [Theory]
    [InlineData("2026-10-17", false)]
    [InlineData("2026-10-19", false)] // Hong Kong only
    [InlineData("2026-11-26", false)] // United States only
    [InlineData("2026-09-25", false)] // China only
    [InlineData("2026-12-24", true)] // half day in US and HK
    [InlineData("2027-01-04", true)] // unmaintained year
    public void TradingDayPlan_UsesThreeMarkets(string dateText, bool expected)
    {
        var date = DateOnly.Parse(dateText);
        var plan = Plan((AutoPlanCycle)2, startDate: new DateOnly(2026, 1, 1));

        Assert.Equal(expected, PlanSchedule.IsDueOn(plan, date));
        Assert.Equal(expected, PlanSchedule.GetDuePeriods(plan, date, date).Count == 1);
    }

    [Theory]
    [InlineData("2026-11-26", true)] // United States only
    [InlineData("2026-10-19", true)] // Hong Kong only
    [InlineData("2026-09-25", false)] // China only
    public void TradingDayPlan_ChinaScope_OnlyUsesChinaCalendar(string dateText, bool expected)
    {
        var date = DateOnly.Parse(dateText);
        var plan = Plan((AutoPlanCycle)2, startDate: new DateOnly(2026, 1, 1), tradingScope: AutoPlanTradingScope.China);

        Assert.Equal(expected, PlanSchedule.IsDueOn(plan, date));
        Assert.Equal(expected, PlanSchedule.GetDuePeriods(plan, date, date).Count == 1);
    }

    [Fact]
    public void GetDuePeriods_Yearly_ReturnsOnePeriodPerYear()
    {
        var plan = YearlyPlan(month: 1, day: 15, from: new DateOnly(2026, 11, 13));

        var periods = PlanSchedule.GetDuePeriods(plan, plan.GenerateFrom, new DateOnly(2028, 12, 31));

        Assert.Equal(["Y2027", "Y2028"], periods.Select(period => period.PeriodKey));
        Assert.Equal(
            [new DateOnly(2027, 1, 15), new DateOnly(2028, 1, 15)],
            periods.Select(period => period.DueDate));
    }

    [Fact]
    public void GetDuePeriods_Yearly_ClampsToMonthEnd()
    {
        var plan = YearlyPlan(month: 2, day: 31, from: new DateOnly(2026, 11, 13));

        var periods = PlanSchedule.GetDuePeriods(plan, plan.GenerateFrom, new DateOnly(2027, 12, 31));

        Assert.Equal(new DateOnly(2027, 2, 28), Assert.Single(periods).DueDate);
    }

    [Fact]
    public void PeriodKeyFor_Yearly_UsesYear()
    {
        var plan = YearlyPlan(month: 1, day: 15, from: new DateOnly(2026, 11, 13));

        Assert.Equal("Y2027", PlanSchedule.PeriodKeyFor(plan, new DateOnly(2027, 1, 15)));
    }

    [Fact]
    public void GetNextDue_Yearly_SkipsPastPayout()
    {
        var plan = YearlyPlan(month: 1, day: 15, from: new DateOnly(2026, 11, 13));

        var next = PlanSchedule.GetNextDue(plan, new DateOnly(2027, 1, 16));

        Assert.NotNull(next);
        Assert.Equal(new DateOnly(2028, 1, 15), next.DueDate);
    }

    [Fact]
    public void IsDueOn_Yearly_ChecksMonthAndDay()
    {
        var plan = YearlyPlan(month: 1, day: 15, from: new DateOnly(2026, 11, 13));

        Assert.True(PlanSchedule.IsDueOn(plan, new DateOnly(2027, 1, 15)));
        Assert.False(PlanSchedule.IsDueOn(plan, new DateOnly(2027, 1, 16)));
        Assert.False(PlanSchedule.IsDueOn(plan, new DateOnly(2027, 2, 15)));
    }

    private static AutoPlan YearlyPlan(int month, int day, DateOnly from)
    {
        return AutoPlan.Create(
            Guid.NewGuid(),
            AutoPlanType.BonusIncome,
            "年终奖",
            60_000m,
            AutoPlanCycle.Yearly,
            day,
            null,
            new TimeOnly(9, 0),
            from,
            null,
            new DateTimeOffset(2026, 11, 13, 2, 0, 0, TimeSpan.Zero),
            monthOfYear: month);
    }

    private static AutoPlan Plan(
        AutoPlanCycle cycle,
        int? dayOfMonth = null,
        int? dayOfWeek = null,
        DateOnly? startDate = null,
        AutoPlanTradingScope tradingScope = AutoPlanTradingScope.ThreeMarkets)
    {
        return AutoPlan.Create(
            Guid.NewGuid(),
            AutoPlanType.FundSip,
            "测试计划",
            100m,
            cycle,
            dayOfMonth,
            dayOfWeek,
            new TimeOnly(9, 0),
            startDate ?? new DateOnly(2026, 1, 1),
            Guid.NewGuid(),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            tradingScope: tradingScope);
    }
}

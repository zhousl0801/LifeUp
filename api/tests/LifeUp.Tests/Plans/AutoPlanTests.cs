using LifeUp.Domain.Plans;

namespace LifeUp.Tests.Plans;

public sealed class AutoPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_YearlyBonus_RequiresMonthOfYear()
    {
        var error = Assert.Throws<ArgumentException>(() => AutoPlan.Create(
            Guid.NewGuid(),
            AutoPlanType.BonusIncome,
            "年终奖",
            60_000m,
            AutoPlanCycle.Yearly,
            15,
            null,
            new TimeOnly(9, 0),
            new DateOnly(2027, 1, 15),
            null,
            Now));

        Assert.Contains("发放月", error.Message);
    }

    [Fact]
    public void Create_MonthlySalary_RejectsMonthOfYear()
    {
        var error = Assert.Throws<ArgumentException>(() => AutoPlan.Create(
            Guid.NewGuid(),
            AutoPlanType.SalaryIncome,
            "工资",
            12_000m,
            AutoPlanCycle.Monthly,
            15,
            null,
            new TimeOnly(9, 0),
            new DateOnly(2026, 11, 15),
            null,
            Now,
            monthOfYear: 1));

        Assert.Contains("每月", error.Message);
    }

    [Fact]
    public void Create_YearlyBonus_AllowsMonthAndDay()
    {
        var plan = AutoPlan.Create(
            Guid.NewGuid(),
            AutoPlanType.BonusIncome,
            "年终奖",
            60_000m,
            AutoPlanCycle.Yearly,
            15,
            null,
            new TimeOnly(9, 0),
            new DateOnly(2027, 1, 15),
            null,
            Now,
            monthOfYear: 1);

        Assert.Equal(AutoPlanType.BonusIncome, plan.Type);
        Assert.Equal(AutoPlanCycle.Yearly, plan.Cycle);
        Assert.Equal(1, plan.MonthOfYear);
        Assert.Equal(15, plan.DayOfMonth);
    }

    [Fact]
    public void Create_BonusIncome_Monthly_Rejected()
    {
        var error = Assert.Throws<ArgumentException>(() => AutoPlan.Create(
            Guid.NewGuid(),
            AutoPlanType.BonusIncome,
            "年终奖",
            60_000m,
            AutoPlanCycle.Monthly,
            15,
            null,
            new TimeOnly(9, 0),
            new DateOnly(2026, 11, 15),
            null,
            Now));

        Assert.Contains("每年", error.Message);
    }

    [Fact]
    public void Create_FundSip_Yearly_Rejected()
    {
        var error = Assert.Throws<ArgumentException>(() => AutoPlan.Create(
            Guid.NewGuid(),
            AutoPlanType.FundSip,
            "定投",
            100m,
            AutoPlanCycle.Yearly,
            15,
            null,
            new TimeOnly(9, 0),
            new DateOnly(2027, 1, 15),
            Guid.NewGuid(),
            Now,
            monthOfYear: 1));

        Assert.Contains("每年", error.Message);
    }

    [Fact]
    public void Update_YearlyMonthChange_ResetsGenerateFrom()
    {
        var plan = AutoPlan.Create(
            Guid.NewGuid(),
            AutoPlanType.BonusIncome,
            "年终奖",
            60_000m,
            AutoPlanCycle.Yearly,
            15,
            null,
            new TimeOnly(9, 0),
            new DateOnly(2026, 1, 15),
            null,
            Now,
            monthOfYear: 1);

        plan.Update(
            AutoPlanType.BonusIncome,
            "年终奖",
            60_000m,
            AutoPlanCycle.Yearly,
            15,
            null,
            new TimeOnly(9, 0),
            new DateOnly(2026, 1, 15),
            null,
            Now.AddMinutes(1),
            new DateOnly(2026, 11, 14),
            monthOfYear: 2);

        Assert.Equal(2, plan.MonthOfYear);
        Assert.Equal(new DateOnly(2026, 11, 14), plan.GenerateFrom);
    }
}

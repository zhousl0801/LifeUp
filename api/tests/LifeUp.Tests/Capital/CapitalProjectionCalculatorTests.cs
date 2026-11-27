using LifeUp.Application.Capital;

namespace LifeUp.Tests.Capital;

public sealed class CapitalProjectionCalculatorTests
{
    private static readonly DateOnly AsOf = new(2026, 11, 12);

    [Fact]
    public void ZeroReturn_ReachesTargetAfterTenMonths()
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, 0m, 0m, 100_000m,
            10_000m, 0m, 0m, 0m, 0m, 0m, 1.5m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Equal(CapitalProjectionStatus.OnTrack, result.Status);
        Assert.Equal(10, result.MonthsRemaining);
        Assert.Equal("2027-09", result.EtaMonth);
        Assert.Equal(0m, result.ProgressPercent);
        Assert.Equal(10_000m, result.Points[0].Capital);
    }

    [Fact]
    public void PureCompounding_ReachesAfterTwelveMonths()
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, 100_000m, 0m, 111_900m,
            0m, 0m, 0m, 0m, 0m, 12m, 0m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Equal(CapitalProjectionStatus.OnTrack, result.Status);
        Assert.Equal(12, result.MonthsRemaining);
        Assert.Equal("2027-11", result.EtaMonth);
    }

    [Fact]
    public void AlreadyReached_ReturnsReachedWithoutEta()
    {
        var input = new CapitalProjectionInput(
            AsOf, 376_000m, 0m, 0m, 300_000m,
            0m, 0m, 0m, 0m, 0m, 0m, 0m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Equal(CapitalProjectionStatus.Reached, result.Status);
        Assert.Equal(100m, result.ProgressPercent);
        Assert.Null(result.EtaMonth);
        Assert.Empty(result.Points);
        Assert.Empty(result.Years);
    }

    [Fact]
    public void NegativeContribution_DepletesCapital()
    {
        var input = new CapitalProjectionInput(
            AsOf, 100_000m, 0m, 0m, 200_000m,
            0m, 5_000m, 0m, 0m, 0m, 0m, 0m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Equal(CapitalProjectionStatus.Depleted, result.Status);
        Assert.Null(result.EtaMonth);
        Assert.Equal("2028-07", result.Points[^1].Month);
        Assert.Equal(0m, result.Points[^1].Capital);
        Assert.Contains(result.Notes, note => note.Contains("2028-07"));
        var year = Assert.Single(result.Years, item => item.Year == 2028);
        Assert.Equal(-35_000m, year.NetSavings);
        Assert.Equal(0m, year.YearEndCapital);
    }

    [Fact]
    public void ProvidentCoversDeficitAfterNonProvidentCapital()
    {
        var input = new CapitalProjectionInput(
            AsOf, 100_000m, 0m, 10_000m, 1_000_000_000m,
            0m, 5_000m, 0m, 0m, 0m, 0m, 0m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Equal(CapitalProjectionStatus.Depleted, result.Status);
        Assert.Equal("2028-09", result.Points[^1].Month);
        Assert.Equal(0m, result.Points[^1].Capital);
        Assert.Contains(result.Notes, note => note.Contains("2028-09"));
    }

    [Fact]
    public void ExtremeGrowth_DoesNotOverflow()
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, 0m, 0m, 1_000_000_000m,
            1m, 0m, 0m, 100m, 0m, 0m, 0m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Equal(CapitalProjectionStatus.OnTrack, result.Status);
        Assert.True(result.Points.Count > 0);
    }

    [Fact]
    public void GrowthAppliesInJanuary()
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, 0m, 0m, 1_000_000m,
            10_000m, 5_000m, 0m, 10m, 0m, 0m, 0m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Equal(5_000m, result.Points.Single(point => point.Month == "2026-12").NetAccumulation);
        Assert.Equal(6_000m, result.Points.Single(point => point.Month == "2027-01").NetAccumulation);
    }

    [Fact]
    public void ProvidentBucketGrowsAtItsOwnRate()
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, 0m, 10_000m, 1_000_000m,
            0m, 0m, 1_000m, 0m, 0m, 0m, 1.5m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.True(result.Points.Single(point => point.Month == "2027-11").Capital > 22_000m);
        Assert.True(result.Years.Sum(year => year.Return) > 0m);
    }

    [Fact]
    public void NoTarget_ReturnsTenYearProjection()
    {
        var input = new CapitalProjectionInput(
            AsOf, 100_000m, 0m, 0m, null,
            1_000m, 0m, 0m, 0m, 0m, 0m, 0m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Equal(CapitalProjectionStatus.NoTarget, result.Status);
        Assert.Null(result.EtaMonth);
        Assert.NotEmpty(result.Points);
        Assert.True(result.Years.Count >= 10);
    }

    [Fact]
    public void BeyondHorizon_DoesNotInventDate()
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, 0m, 0m, 1_000_000_000m,
            1m, 0m, 0m, 0m, 0m, 0m, 0m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.True(result.Status is CapitalProjectionStatus.BeyondHorizon or CapitalProjectionStatus.Unreachable);
        Assert.Null(result.EtaMonth);
    }

    [Fact]
    public void BlendedReturn_UsesCurrentAllocationAndIgnoresCash()
    {
        var input = new CapitalProjectionInput(
            AsOf, 26_000m, 300_000m, 50_000m, 1_000_000m,
            12_000m, 7_200m, 2_000m, 0m, 0m, 5m, 1.5m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Equal(4.19m, decimal.Round(result.BlendedAnnualReturnPercent, 2));
        Assert.Equal(6_800m, result.MonthlyNetAccumulation);
    }

    [Fact]
    public void Calculate_BonusInPayoutMonth_RaisesCapitalAndKeepsMonthlyNet()
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, 0m, 0m, 1_000_000m,
            10_000m, 5_000m, 0m, 0m, 0m, 0m, 0m,
            AnnualBonus: 60_000m, BonusPayoutMonth: 1);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Equal(5_000m, result.MonthlyNetAccumulation);
        Assert.Equal(5_000m, result.Points[0].NetAccumulation);
        Assert.Equal(65_000m, result.Points[1].NetAccumulation);
        var year = Assert.Single(result.Years, item => item.Year == 2027);
        Assert.Equal(120_000m, year.NetSavings + year.ProvidentDeposit);
        Assert.Contains(result.Notes, note => note.Contains("年终奖"));
    }

    [Fact]
    public void Calculate_Bonus_ShiftsEtaEarlier()
    {
        var withoutBonus = new CapitalProjectionInput(
            AsOf, 0m, 0m, 0m, 100_000m,
            10_000m, 0m, 0m, 0m, 0m, 0m, 0m);
        var withBonus = withoutBonus with { AnnualBonus = 60_000m, BonusPayoutMonth = 1 };

        var baseline = new CapitalProjectionCalculator().Calculate(withoutBonus);
        var improved = new CapitalProjectionCalculator().Calculate(withBonus);

        Assert.NotNull(baseline.MonthsRemaining);
        Assert.NotNull(improved.MonthsRemaining);
        Assert.True(improved.MonthsRemaining < baseline.MonthsRemaining);
    }

    [Fact]
    public void Calculate_Bonus_GrowsWithIncomeGrowth()
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, 0m, 0m, 1_000_000m,
            10_000m, 0m, 0m, 10m, 0m, 0m, 0m,
            AnnualBonus: 10_000m, BonusPayoutMonth: 1);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Equal(10_000m, result.Points[0].NetAccumulation);
        Assert.Equal(22_000m, result.Points[1].NetAccumulation);
    }

    [Fact]
    public void Calculate_WithoutBonus_AddsNoBonusNote()
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, 0m, 0m, 100_000m,
            10_000m, 0m, 0m, 0m, 0m, 0m, 0m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.DoesNotContain(result.Notes, note => note.Contains("年终奖"));
        Assert.Equal(10_000m, result.Points[0].NetAccumulation);
    }

    [Fact]
    public void NoInvestmentAssets_AddsRateHint()
    {
        var input = new CapitalProjectionInput(
            AsOf, 100_000m, 0m, 0m, 1_000_000m,
            1_000m, 0m, 0m, 0m, 0m, 5m, 1.5m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Contains(result.Notes, note => note.Contains("投资年化"));
    }

    [Fact]
    public void CalculateFire_WhenExpensesGrowInJanuary_WaitsForCashflowCrossover()
    {
        var input = new CapitalProjectionInput(
            AsOf, 200_000m, 0m, 0m, 250_000m,
            60_000m, 0m, 0m, 0m, 10m, 0m, 0m, FireMonthlyExpense: 1_000m);

        var projection = new CapitalProjectionCalculator().CalculateFire(input);

        Assert.Equal(CapitalProjectionStatus.OnTrack, projection.Status);
        Assert.Equal(300_000m, projection.TargetAmount);
        Assert.Equal(100_000m, projection.Remaining);
        Assert.Equal(66.7m, projection.ProgressPercent);
        Assert.Equal(3, projection.MonthsRemaining);
        Assert.Equal("2027-02", projection.EtaMonth);
        Assert.Equal(12_000m, projection.Points[0].ExpectedAnnualExpense);
        Assert.Equal(13_200m, projection.Points[1].ExpectedAnnualExpense);
        Assert.Equal(12_800m, projection.Points[1].PassiveAnnualCashflow);
        Assert.Equal(15_200m, projection.Points[^1].PassiveAnnualCashflow);
        Assert.Equal(1_266.67m, projection.Years[^1].PassiveMonthlyCashflow);
        Assert.Equal(86.7m, projection.Years[0].FreedomPercent);
        Assert.Equal(100m, projection.Years[^1].FreedomPercent);
        Assert.True(projection.Years[^1].IsGoalYear);
    }

    [Fact]
    public void CalculateFire_PreservesCrossoverBeyondTenYearsWhenSampling()
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, 0m, 0m, null,
            1_200m, 0m, 0m, 0m, 0m, 0m, 0m, FireMonthlyExpense: 1_000m, SafeWithdrawalRatePercent: 5m);

        var projection = new CapitalProjectionCalculator().CalculateFire(input);

        Assert.Equal(240_000m, projection.TargetAmount);
        Assert.Equal(200, projection.MonthsRemaining);
        Assert.InRange(projection.Points.Count, 13, 120);
        Assert.Equal("2026-12", projection.Points[0].Month);
        Assert.Equal(projection.EtaMonth, projection.Points[^1].Month);
        Assert.Equal(12_000m, projection.Points[^1].PassiveAnnualCashflow);
        Assert.Equal(12_000m, projection.Points[^1].ExpectedAnnualExpense);
        Assert.True(projection.Years[^1].IsGoalYear);
    }

    [Fact]
    public void CalculateFire_ZeroExpenseReturnsTenYearsEvenIfAccumulationDepletes()
    {
        var input = new CapitalProjectionInput(
            AsOf, 100m, 0m, 0m, 300m,
            0m, 100m, 0m, 0m, 0m, 0m, 0m, FireMonthlyExpense: 0m);

        var projection = new CapitalProjectionCalculator().CalculateFire(input);

        Assert.Equal(CapitalProjectionStatus.NoTarget, projection.Status);
        Assert.Equal(0m, projection.TargetAmount);
        Assert.Equal(0m, projection.ProgressPercent);
        Assert.Equal(120, projection.Points.Count);
        Assert.Equal("2036-11", projection.Points[^1].Month);
        Assert.All(projection.Years, year => Assert.Equal(0m, year.FreedomPercent));
    }

    [Fact]
    public void CalculateFire_AlreadyReachedUsesWithdrawalRateAndNoEta()
    {
        var input = new CapitalProjectionInput(
            AsOf, 240_000m, 0m, 0m, null,
            0m, 0m, 0m, 0m, 0m, 0m, 0m, FireMonthlyExpense: 1_000m, SafeWithdrawalRatePercent: 5m);

        var projection = new CapitalProjectionCalculator().CalculateFire(input);

        Assert.Equal(CapitalProjectionStatus.Reached, projection.Status);
        Assert.Equal(100m, projection.ProgressPercent);
        Assert.Equal(0, projection.MonthsRemaining);
        Assert.Null(projection.EtaMonth);
    }

    [Fact]
    public void Calculate_CustomKeepsFixedTargetAndAddsCashflowMetrics()
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, 0m, 0m, 100_000m,
            10_000m, 0m, 0m, 0m, 0m, 0m, 0m, FireMonthlyExpense: 1_000m, SafeWithdrawalRatePercent: 5m);

        var projection = new CapitalProjectionCalculator().Calculate(input);

        Assert.Equal(100_000m, projection.TargetAmount);
        Assert.Equal(10, projection.MonthsRemaining);
        Assert.Equal(5_000m, projection.Points[^1].PassiveAnnualCashflow);
        Assert.Equal(12_000m, projection.Points[^1].ExpectedAnnualExpense);
        Assert.Equal(416.67m, projection.Years[^1].PassiveMonthlyCashflow);
        Assert.Equal(41.7m, projection.Years[^1].FreedomPercent);
    }

    [Theory]
    [InlineData(10_000, false)]
    [InlineData(160_000, true)]
    public void CalculateCoast_ReturnsThresholdAndFirstEligibleMonth(decimal investment, bool alreadyReached)
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, investment, 0m, null,
            3_000m, 0m, 0m, 0m, 2m, 6m, 0m, FireMonthlyExpense: 1_000m);
        var projection = new CapitalProjectionCalculator().CalculateFire(input);

        var coast = CapitalProjectionCalculator.CalculateCoast(input, projection, null);

        var expected = 300_000d * Math.Pow(1.02d / 1.06d, 25);
        Assert.Equal(2051, coast.TargetYear);
        Assert.Equal("default", coast.TargetYearSource);
        Assert.True(coast.Achievable);
        Assert.Equal(Math.Round((decimal)expected, 2), coast.ThresholdCapital);
        Assert.Equal(alreadyReached, coast.Reached);
        if (alreadyReached)
        {
            Assert.Null(coast.EtaMonth);
            Assert.Equal(0, coast.MonthsRemaining);
            Assert.Equal(100m, coast.ProgressPercent);
        }
        else
        {
            var firstEligible = projection.Points
                .Select((point, index) => (point, months: index + 1))
                .First(item => (double)item.point.Capital >= expected * Math.Pow(1.06d, item.months / 12d));
            Assert.Equal(firstEligible.point.Month, coast.EtaMonth);
            Assert.Equal(firstEligible.months, coast.MonthsRemaining);
            Assert.True(coast.MonthsRemaining < projection.MonthsRemaining);
        }
    }

    [Theory]
    [InlineData(2026, 1_000, 6, "目标年份需晚于当前年份")]
    [InlineData(2050, 0, 6, "请先设定或记录有效生活支出")]
    [InlineData(2050, 1_000, 2, "当前组合年化不高于支出年增长，暂无法仅靠复利滑行")]
    public void CalculateCoast_ReturnsReasonWhenNotAchievable(int year, decimal expense, decimal annualReturn, string note)
    {
        var input = new CapitalProjectionInput(
            AsOf, 0m, 10_000m, 0m, null,
            0m, 0m, 0m, 0m, 2m, annualReturn, 0m, FireMonthlyExpense: expense);
        var projection = new CapitalProjectionCalculator().CalculateFire(input);

        var coast = CapitalProjectionCalculator.CalculateCoast(input, projection, year);

        Assert.False(coast.Achievable);
        Assert.Equal("user", coast.TargetYearSource);
        Assert.Equal(note, coast.Note);
        Assert.Null(coast.ThresholdCapital);
        Assert.Null(coast.EtaMonth);
        Assert.Equal(0m, coast.ProgressPercent);
    }
}

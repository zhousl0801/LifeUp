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
}

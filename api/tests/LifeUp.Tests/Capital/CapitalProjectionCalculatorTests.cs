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
        Assert.Contains(result.Notes, note => note.Contains("耗尽"));
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
    public void NoInvestmentAssets_AddsRateHint()
    {
        var input = new CapitalProjectionInput(
            AsOf, 100_000m, 0m, 0m, 1_000_000m,
            1_000m, 0m, 0m, 0m, 0m, 5m, 1.5m);

        var result = new CapitalProjectionCalculator().Calculate(input);

        Assert.Contains(result.Notes, note => note.Contains("投资年化"));
    }
}

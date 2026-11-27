using LifeUp.Application.Capital;
using LifeUp.Application.Ledgers;

namespace LifeUp.Tests.Capital;

public sealed class FireMilestoneBuilderTests
{
    private static readonly DateOnly AsOf = new(2026, 11, 12);

    [Fact]
    public void Build_UsesEarliestKnownAssetHistoryAndFullMonthlyForecast()
    {
        var current = new CapitalCurrentDto(AsOf, 50_000m, 50_000m, 0m, 0m, false, null);
        var trend = new[]
        {
            TrendPoint(new DateOnly(2026, 6, 1), 49_000m),
            TrendPoint(new DateOnly(2026, 2, 1), 11_999m),
            TrendPoint(new DateOnly(2026, 3, 1), 12_000m)
        };
        var standard = new FireModeDto(1_000m, "history", 12_000m, 300_000m,
            833.33m, 16.7m, CapitalProjectionStatus.BeyondHorizon, null, null);
        var lean = standard with { TargetCapital = 110_000m, EtaMonth = "2036-11" };
        var coast = new CoastFireDto(2051, "default", true, false, 100_000m, 50m, "2037-01", 122, null);
        var input = new CapitalProjectionInput(
            AsOf, 50_000m, 0m, 0m, null,
            500m, 0m, 0m, 0m, 2m, 0m, 0m, FireMonthlyExpense: 1_000m);
        var projection = new CapitalProjectionCalculator().CalculateFire(input);

        var milestones = FireMilestoneBuilder.Build(current, trend, "standard", standard, lean, coast, projection);

        Assert.Equal(["emergency", "coast", "barista", "lean"], milestones.Select(milestone => milestone.Key));
        Assert.Equal(new FireMilestoneDto("emergency", "应急垫", 12_000m, 4m, true, "2026-03-01", "覆盖 1 年生活支出"), milestones[0]);
        Assert.Equal("2037-01-01", milestones[1].Date);
        Assert.Equal(33.3m, milestones[1].ProgressPercent);
        Assert.Equal(150_000m, milestones[2].ThresholdAmount);
        Assert.Equal("2043-07-01", milestones[2].Date);
        Assert.DoesNotContain(projection.Points, point => point.Month == "2043-07");
        Assert.Equal("2036-11-01", milestones[3].Date);
    }

    [Fact]
    public void Build_OmitsInapplicableMilestonesAndCanReturnUnknownHistoricalDate()
    {
        var current = new CapitalCurrentDto(AsOf, 50_000m, 50_000m, 0m, 0m, true, null);
        var lean = new FireModeDto(1_000m, "history", 12_000m, 300_000m,
            833.33m, 16.7m, CapitalProjectionStatus.NoTarget, null, null);
        var coast = new CoastFireDto(2051, "default", true, false, 300_000m, 15m, null, null, null);
        var projection = new CapitalProjectionResult(
            CapitalProjectionStatus.NoTarget, null, null, 0m, null, null, 0m, 0m, [], [], []);

        var milestones = FireMilestoneBuilder.Build(current, [], "lean", lean, lean, coast, projection);

        Assert.Equal(["emergency", "barista"], milestones.Select(milestone => milestone.Key));
        Assert.True(milestones[0].Reached);
        Assert.Null(milestones[0].Date);
        Assert.False(milestones[1].Reached);
        Assert.Null(milestones[1].Date);
        Assert.Empty(FireMilestoneBuilder.Build(current, [], "lean", lean with { TargetCapital = 0m }, lean, coast, projection));
    }

    private static AssetTrendPointDto TrendPoint(DateOnly date, decimal knownAssets)
    {
        return new AssetTrendPointDto(date, null, knownAssets, null, null, null, 0m, 0m, null, 0m, false);
    }
}

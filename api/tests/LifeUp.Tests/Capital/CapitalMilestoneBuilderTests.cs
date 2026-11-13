using LifeUp.Application.Capital;
using LifeUp.Application.Ledgers;

namespace LifeUp.Tests.Capital;

public sealed class CapitalMilestoneBuilderTests
{
    [Fact]
    public void Build_UsesFirstCrossingDateAndTenPercentThresholds()
    {
        var points = new List<AssetTrendPointDto>
        {
            Point("2026-10-01", total: 5_000m, known: 5_000m),
            Point("2026-10-15", total: 12_000m, known: 12_000m),
            Point("2026-11-01", total: 25_000m, known: 25_000m),
            Point("2026-11-10", total: 31_000m, known: 31_000m)
        };

        var milestones = CapitalMilestoneBuilder.Build(100_000m, points, coverageComplete: true);

        Assert.Equal([10m, 20m, 30m], milestones.Select(item => item.ProgressPercent));
        Assert.Equal(new DateOnly(2026, 10, 15), milestones[0].Date);
        Assert.Equal(new DateOnly(2026, 11, 1), milestones[1].Date);
        Assert.Equal(new DateOnly(2026, 11, 10), milestones[2].Date);
        Assert.Equal("资本突破 1 万", milestones[0].Title);
    }

    [Fact]
    public void Build_IncompleteCoverageUsesKnownTotalAndSkipsMissingThresholds()
    {
        var points = new List<AssetTrendPointDto>
        {
            Point("2026-10-01", total: null, known: 5_000m),
            Point("2026-10-15", total: null, known: 12_000m),
            Point("2026-11-01", total: null, known: 15_000m)
        };

        var milestones = CapitalMilestoneBuilder.Build(100_000m, points, coverageComplete: false);

        var milestone = Assert.Single(milestones);
        Assert.Equal(10m, milestone.ProgressPercent);
        Assert.Equal(new DateOnly(2026, 10, 15), milestone.Date);
    }

    [Fact]
    public void FormatThreshold_FormatsWanAndYuan()
    {
        Assert.Equal("10 万", CapitalMilestoneBuilder.FormatThreshold(100_000m));
        Assert.Equal("1 万", CapitalMilestoneBuilder.FormatThreshold(10_000m));
        Assert.Equal("5,000 元", CapitalMilestoneBuilder.FormatThreshold(5_000m));
    }

    private static AssetTrendPointDto Point(string date, decimal? total, decimal known)
    {
        return new AssetTrendPointDto(
            DateOnly.Parse(date),
            total,
            known,
            null,
            null,
            null,
            0m,
            0m,
            null,
            0m,
            total is not null,
            true,
            true,
            false,
            false,
            null,
            false);
    }
}

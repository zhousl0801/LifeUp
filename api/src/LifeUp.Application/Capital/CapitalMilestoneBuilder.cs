using System.Globalization;
using LifeUp.Application.Ledgers;

namespace LifeUp.Application.Capital;

public sealed record CapitalMilestone(
    decimal ThresholdAmount,
    decimal ProgressPercent,
    DateOnly Date,
    string Title);

public static class CapitalMilestoneBuilder
{
    public static IReadOnlyList<CapitalMilestone> Build(
        decimal targetAmount,
        IReadOnlyList<AssetTrendPointDto> points,
        bool coverageComplete)
    {
        if (targetAmount <= 0m || points.Count == 0)
        {
            return [];
        }

        var ordered = points.OrderBy(point => point.Date).ToList();
        var milestones = new List<CapitalMilestone>();

        for (var step = 1; step <= 9; step++)
        {
            var threshold = Round(targetAmount * step / 10m);
            var hit = ordered.FirstOrDefault(point => ReadValue(point, coverageComplete) is decimal value && value >= threshold);

            if (hit is null)
            {
                continue;
            }

            milestones.Add(new CapitalMilestone(
                threshold,
                decimal.Round(threshold / targetAmount * 100m, 1, MidpointRounding.AwayFromZero),
                hit.Date,
                $"资本突破 {FormatThreshold(threshold)}"));
        }

        return milestones;
    }

    public static string FormatThreshold(decimal amount)
    {
        if (amount >= 10_000m)
        {
            return $"{decimal.Round(amount / 10_000m, 1, MidpointRounding.AwayFromZero).ToString("0.#", CultureInfo.InvariantCulture)} 万";
        }

        return $"{amount.ToString("N0", CultureInfo.InvariantCulture)} 元";
    }

    private static decimal? ReadValue(AssetTrendPointDto point, bool coverageComplete)
    {
        return coverageComplete ? point.TotalAssets : point.KnownTotalAssets;
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}

using System.Globalization;
using LifeUp.Application.Ledgers;
using static LifeUp.Application.Capital.CapitalProjectionCalculator;

namespace LifeUp.Application.Capital;

public static class FireMilestoneBuilder
{
    public static IReadOnlyList<FireMilestoneDto> Build(
        CapitalCurrentDto current,
        IReadOnlyList<AssetTrendPointDto> trend,
        string activeMode,
        FireModeDto active,
        FireModeDto lean,
        CoastFireDto coast,
        CapitalProjectionResult projection)
    {
        if (active.TargetCapital <= 0m)
        {
            return [];
        }

        var history = trend.OrderBy(point => point.Date).ToList();
        var milestones = new List<FireMilestoneDto>
        {
            BuildMilestone("emergency", "应急垫", active.AnnualExpense, "覆盖 1 年生活支出",
                current.Total >= active.AnnualExpense, FindProjectedMonth(active.AnnualExpense))
        };

        if (coast.Achievable && coast.ThresholdCapital is > 0m and decimal coastThreshold
            && coastThreshold < active.TargetCapital)
        {
            milestones.Add(BuildMilestone("coast", "Coast", coastThreshold,
                $"停止存钱也可在 {coast.TargetYear} 年自然自由", coast.Reached, coast.EtaMonth));
        }

        var baristaThreshold = Round(active.TargetCapital * 0.5m);
        milestones.Add(BuildMilestone("barista", "半退休", baristaThreshold, "被动现金流覆盖一半开销",
            current.Total >= baristaThreshold, FindProjectedMonth(baristaThreshold)));

        if (activeMode == "standard" && lean.TargetCapital > 0m && lean.TargetCapital < active.TargetCapital)
        {
            milestones.Add(BuildMilestone("lean", "Lean", lean.TargetCapital, "被动现金流覆盖全部刚性支出",
                current.Total >= lean.TargetCapital, lean.EtaMonth));
        }

        return milestones;

        string? FindProjectedMonth(decimal threshold)
        {
            return projection.MonthlyPoints.FirstOrDefault(point => point.Capital >= threshold)?.Month;
        }

        FireMilestoneDto BuildMilestone(
            string key, string label, decimal threshold, string description, bool reached, string? projectedMonth)
        {
            string? date = null;
            if (reached)
            {
                var hit = history.FirstOrDefault(point =>
                    CapitalMilestoneBuilder.ReadValue(point, current.CoverageComplete) >= threshold);
                date = hit?.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            else if (projectedMonth is not null)
            {
                date = $"{projectedMonth}-01";
            }

            return new FireMilestoneDto(
                key, label, threshold, Round1(Math.Clamp(threshold / active.TargetCapital * 100m, 0m, 100m)),
                reached, date, description);
        }
    }
}

using System.Globalization;
using static LifeUp.Application.Capital.CapitalProjectionCalculator;

namespace LifeUp.Application.Capital;

public static class FireInsightBuilder
{
    public static FireInsightsDto Build(
        CapitalProjectionCalculator calculator,
        CapitalProjectionInput input,
        CapitalCurrentDto current,
        string activeMode,
        FireModeDto standard,
        FireModeDto lean,
        CoastFireDto coast,
        decimal savingsRatePercent,
        decimal passiveMonthlyCashflow,
        IReadOnlyList<FireExpenseCategoryDto> categories,
        IReadOnlyList<FireMilestoneDto> milestones)
    {
        var active = activeMode == "lean" ? lean : standard;
        if (active.AnnualExpense <= 0m)
        {
            return new FireInsightsDto("待设定", "补充月支出或记账满 1 个完整月后，即可为你测算专属 FIRE 路线。", [], []);
        }

        var progress = active.ProgressPercent;
        var covered = progress is >= 25m and < 80m
            ? MatchCategories(categories, passiveMonthlyCashflow)
            : [];
        var coverage = covered.Count > 0 ? $"，已覆盖「{string.Join(" + ", covered)}」开销" : string.Empty;
        var barista = milestones.FirstOrDefault(item => item.Key == "barista");
        var (stage, headline) = progress switch
        {
            < 10m => ("起步期", $"当前资产可支撑 {Round1(current.Total / active.MonthlyExpense).ToString("F1", CultureInfo.InvariantCulture)} 个月的应急生活支出。"),
            < 25m => ("筑基期", $"每天的被动收入约 {Money(Round(current.Total * (input.SafeWithdrawalRatePercent ?? DefaultSafeWithdrawalRatePercent) / 100m / 365m))}，积累的本金正开始产生复利。"),
            < 50m => ("加速期", $"当前每月被动现金流约 {Money(passiveMonthlyCashflow)}{coverage}。{DescribeBarista(input.AsOfDate, barista)}"),
            < 80m => ("半自由期", $"你已跨过半退休线，被动现金流可覆盖 {progress.ToString("0.#", CultureInfo.InvariantCulture)}% 的目标开销{coverage}。"),
            < 100m => ("冲刺期", $"{DescribeRemaining(active.MonthsRemaining)}留意资产回撤，保持积累节奏。"),
            _ => ("已自由", "恭喜达成 FIRE！你的被动现金流已能覆盖全部目标开销。")
        };
        var suggestions = BuildSuggestions(calculator, input, activeMode, active, standard, lean, coast, savingsRatePercent)
            .Take(3).ToList();
        return new FireInsightsDto(stage, headline, covered, suggestions);
    }

    private static IReadOnlyList<string> MatchCategories(
        IReadOnlyList<FireExpenseCategoryDto> categories,
        decimal passiveMonthlyCashflow)
    {
        var matched = new List<string>();
        var remaining = passiveMonthlyCashflow;
        var candidates = categories.Where(category => !category.IsExcluded && category.MonthlyAverage > 0m)
            .OrderByDescending(category => category.IsLean)
            .ThenByDescending(category => category.MonthlyAverage)
            .ThenBy(category => category.Name, StringComparer.Ordinal);
        foreach (var category in candidates)
        {
            if (category.MonthlyAverage > remaining)
            {
                continue;
            }

            matched.Add(category.Name);
            remaining -= category.MonthlyAverage;
            if (matched.Count == 3)
            {
                break;
            }
        }

        return matched;
    }

    private static IEnumerable<FireSuggestionDto> BuildSuggestions(
        CapitalProjectionCalculator calculator,
        CapitalProjectionInput input,
        string activeMode,
        FireModeDto active,
        FireModeDto standard,
        FireModeDto lean,
        CoastFireDto coast,
        decimal savingsRatePercent)
    {
        var reached = active.Status == CapitalProjectionStatus.Reached;
        var delta = active.MonthlyExpense < 2_000m ? 200m : 500m;
        if (!reached && active.MonthlyExpense > delta)
        {
            var reduced = calculator.CalculateFire(input with
            {
                MonthlyExpense = Math.Max(0m, input.MonthlyExpense - delta),
                FireMonthlyExpense = active.MonthlyExpense - delta
            });
            var targetReduction = active.TargetCapital - (reduced.TargetAmount ?? 0m);
            var saved = active.MonthsRemaining - reduced.MonthsRemaining;
            if (saved is >= 1 || targetReduction > 0m)
            {
                var timing = saved is >= 1 ? $"，自由时间提前 {Duration(saved.Value)}" : string.Empty;
                yield return new FireSuggestionDto("支出杠杆",
                    $"月支出减少 {Money(delta)}，FIRE 目标本金减少 ¥{CapitalMilestoneBuilder.FormatThreshold(targetReduction)}{timing}。");
            }
        }

        var leanSavings = standard.MonthsRemaining - lean.MonthsRemaining;
        if (activeMode == "standard" && lean.Status == CapitalProjectionStatus.OnTrack
            && (standard.MonthsRemaining is null || leanSavings is >= 1))
        {
            var timing = leanSavings is >= 1 ? $"，可提前 {Duration(leanSavings.Value)}" : string.Empty;
            yield return new FireSuggestionDto("阶段捷径",
                $"可先以 Lean FIRE（{Money(lean.MonthlyExpense)}/月）为阶段目标，预计 {FormatMonth(lean.EtaMonth)}达成{timing}。");
        }

        if (coast.Achievable && !coast.Reached && coast.EtaMonth is not null
            && (active.MonthsRemaining is null || active.MonthsRemaining - coast.MonthsRemaining is >= 6))
        {
            yield return new FireSuggestionDto("Coast 捷径",
                $"预计 {FormatMonth(coast.EtaMonth)}达成 Coast FIRE，届时停止额外储蓄也能在 {coast.TargetYear} 年自然自由。");
        }

        if (!reached)
        {
            var hypotheticalFourPercent = input.Investment <= 0m || input.InvestmentAnnualReturnPercent == 0m;
            var trial = input.Investment <= 0m
                ? calculator.CalculateFireWithPortfolioReturn(input, 4m)
                : calculator.CalculateFire(input with
                {
                    InvestmentAnnualReturnPercent = hypotheticalFourPercent ? 4m : input.InvestmentAnnualReturnPercent + 1m
                });
            var saved = active.MonthsRemaining - trial.MonthsRemaining;
            if (saved is >= 1)
            {
                var assumption = hypotheticalFourPercent ? "投资年化按 4% 试算" : "投资年化提高 1 个百分点试算";
                if (input.Investment <= 0m)
                {
                    assumption = "组合年化按 4% 试算";
                }

                yield return new FireSuggestionDto("收益试算", $"{assumption}，自由时间可提前 {Duration(saved.Value)}。");
            }
        }

        if (savingsRatePercent >= 50m)
        {
            yield return new FireSuggestionDto("高储蓄率",
                $"你的综合储蓄率已达 {savingsRatePercent.ToString("0.#", CultureInfo.InvariantCulture)}%，持续积累正在缩小现金流缺口。");
        }
    }

    private static string DescribeBarista(DateOnly today, FireMilestoneDto? milestone)
    {
        if (milestone is { Reached: false, Date: string date }
            && DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            var remaining = (month.Year - today.Year) * 12 + month.Month - today.Month;
            return $"预计再过 {Duration(remaining)}达到半退休。";
        }

        return string.Empty;
    }

    private static string DescribeRemaining(int? months)
    {
        return months is > 0 ? $"距离财务自由预计还有 {Duration(months.Value)}。" : "你已接近财务自由。";
    }

    private static string Duration(int months)
    {
        if (months < 12)
        {
            return $"{months} 个月";
        }

        var remaining = months % 12;
        return remaining == 0 ? $"{months / 12} 年" : $"{months / 12} 年 {remaining} 个月";
    }

    private static string FormatMonth(string? value)
    {
        return DateOnly.TryParseExact($"{value}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? $"{date.Year} 年 {date.Month} 月"
            : string.Empty;
    }

    private static string Money(decimal value)
    {
        return $"¥{value.ToString("#,0.##", CultureInfo.InvariantCulture)}";
    }
}

namespace LifeUp.Application.Capital;

public sealed class CapitalProjectionCalculator
{
    private const int MaximumMonths = 1200;
    private const int DisplayYears = 10;
    private const int MaximumPoints = 120;
    private const int MonthlyPointsKept = 12;

    public CapitalProjectionResult Calculate(CapitalProjectionInput input)
    {
        var notes = new List<string>
        {
            "按当前资产结构推演"
        };
        var cash = NonNegative(input.Cash);
        var investment = NonNegative(input.Investment);
        var provident = NonNegative(input.Provident);
        var nonProvident = cash + investment;
        var total = nonProvident + provident;
        var monthlyNet = Round(input.MonthlyIncome - input.MonthlyExpense + input.MonthlyProvidentDeposit);
        var blendedReturn = total > 0m
            ? Round((investment * input.InvestmentAnnualReturnPercent + provident * input.ProvidentAnnualReturnPercent) / total)
            : 0m;

        if (investment == 0m && input.InvestmentAnnualReturnPercent != 0m)
        {
            notes.Add("当前没有投资资产，投资年化暂不生效。");
        }

        if (input.TargetAmount is decimal startTarget && total >= startTarget)
        {
            notes.Add("目标已达成。");

            return new CapitalProjectionResult(
                CapitalProjectionStatus.Reached,
                startTarget,
                0m,
                100m,
                null,
                0,
                monthlyNet,
                blendedReturn,
                [],
                [],
                notes);
        }

        var investmentRate = input.InvestmentAnnualReturnPercent / 100m;
        var providentRate = input.ProvidentAnnualReturnPercent / 100m;
        var nonProvidentRate = nonProvident > 0m
            ? MonthlyRate(investmentRate * investment / nonProvident)
            : 0m;
        var providentMonthlyRate = MonthlyRate(providentRate);
        var incomeGrowth = input.IncomeGrowthPercent / 100m;
        var expenseGrowth = input.ExpenseGrowthPercent / 100m;
        var firstMonth = new DateOnly(input.AsOfDate.Year, input.AsOfDate.Month, 1).AddMonths(1);
        var nonProvidentBalance = nonProvident;
        var providentBalance = provident;
        var points = new List<CapitalProjectionPoint>();
        var years = new Dictionary<int, YearAccumulator>();
        var monthIndex = 0;
        var reached = false;
        var depleted = false;
        string? etaMonth = null;
        int? monthsRemaining = null;
        var iterationLimit = input.TargetAmount is null ? DisplayYears * 12 : MaximumMonths;

        while (monthIndex < iterationLimit)
        {
            var month = firstMonth.AddMonths(monthIndex);
            var yearOffset = month.Year - input.AsOfDate.Year;
            var income = Round(input.MonthlyIncome * GrowthFactor(incomeGrowth, yearOffset));
            var expense = Round(input.MonthlyExpense * GrowthFactor(expenseGrowth, yearOffset));
            var providentDeposit = Round(input.MonthlyProvidentDeposit * GrowthFactor(incomeGrowth, yearOffset));
            var netSavings = income - expense;
            var netAccumulation = netSavings + providentDeposit;
            var nonProvidentReturn = nonProvidentBalance * nonProvidentRate;
            var providentReturn = providentBalance * providentMonthlyRate;
            var monthReturn = nonProvidentReturn + providentReturn;

            nonProvidentBalance += nonProvidentReturn + netSavings;
            providentBalance += providentReturn + providentDeposit;

            if (nonProvidentBalance < 0m)
            {
                var transfer = Math.Min(providentBalance, -nonProvidentBalance);
                providentBalance -= transfer;
                nonProvidentBalance += transfer;
            }

            if (nonProvidentBalance < 0m)
            {
                nonProvidentBalance = 0m;
                providentBalance = 0m;
                depleted = true;
            }

            monthIndex++;
            var capital = Round(nonProvidentBalance + providentBalance);
            points.Add(new CapitalProjectionPoint(
                month.ToString("yyyy-MM"),
                capital,
                Round(netAccumulation),
                Round(monthReturn)));

            if (!years.TryGetValue(month.Year, out var year))
            {
                year = new YearAccumulator();
                years[month.Year] = year;
            }

            year.NetSavings += netSavings;
            year.ProvidentDeposit += providentDeposit;
            year.Return += monthReturn;
            year.YearEndCapital = capital;

            if (input.TargetAmount is decimal target && capital >= target)
            {
                reached = true;
                etaMonth = month.ToString("yyyy-MM");
                monthsRemaining = monthIndex;
                year.IsGoalYear = true;
                break;
            }

            if (depleted)
            {
                break;
            }
        }

        var status = ResolveStatus(input.TargetAmount, reached, depleted, points);
        AddStatusNotes(notes, status, points);
        var displayMonthLimit = input.TargetAmount is null
            ? DisplayYears * 12
            : monthsRemaining is int displayMonths && displayMonths <= DisplayYears * 12
                ? displayMonths
                : DisplayYears * 12;
        var displayYearLimit = input.AsOfDate.Year + DisplayYears;

        if (input.TargetAmount is not null
            && monthsRemaining is int goalMonths
            && goalMonths <= DisplayYears * 12)
        {
            displayYearLimit = firstMonth.AddMonths(goalMonths - 1).Year;
        }

        var displayPoints = SelectPoints(points, displayMonthLimit);
        var displayYears = BuildYears(years, displayYearLimit);
        var progress = input.TargetAmount is decimal progressTarget && progressTarget > 0m
            ? decimal.Round(Math.Min(100m, total / progressTarget * 100m), 1, MidpointRounding.AwayFromZero)
            : 0m;
        var remaining = input.TargetAmount is decimal remainingTarget
            ? Round(Math.Max(0m, remainingTarget - total))
            : (decimal?)null;

        return new CapitalProjectionResult(
            status,
            input.TargetAmount,
            remaining,
            progress,
            etaMonth,
            monthsRemaining,
            monthlyNet,
            blendedReturn,
            displayPoints,
            displayYears,
            notes);
    }

    private static CapitalProjectionStatus ResolveStatus(
        decimal? targetAmount,
        bool reached,
        bool depleted,
        IReadOnlyList<CapitalProjectionPoint> points)
    {
        if (depleted)
        {
            return CapitalProjectionStatus.Depleted;
        }

        if (targetAmount is null)
        {
            return CapitalProjectionStatus.NoTarget;
        }

        if (reached)
        {
            return CapitalProjectionStatus.OnTrack;
        }

        var growing = points.Count >= 2 && points[^1].Capital > points[^2].Capital;

        return growing
            ? CapitalProjectionStatus.BeyondHorizon
            : CapitalProjectionStatus.Unreachable;
    }

    private static void AddStatusNotes(
        List<string> notes,
        CapitalProjectionStatus status,
        IReadOnlyList<CapitalProjectionPoint> points)
    {
        switch (status)
        {
            case CapitalProjectionStatus.NoTarget:
                notes.Add("尚未设定目标，按 10 年推演。");
                break;
            case CapitalProjectionStatus.Depleted:
                var month = points.Count > 0 ? points[^1].Month : "未知月份";
                notes.Add($"按当前假设，资本将在 {month} 耗尽。");
                break;
            case CapitalProjectionStatus.BeyondHorizon:
                notes.Add("按当前假设，100 年内无法达成目标。");
                break;
            case CapitalProjectionStatus.Unreachable:
                notes.Add("按当前假设无法达成目标。");
                break;
        }
    }

    private static IReadOnlyList<CapitalProjectionPoint> SelectPoints(
        IReadOnlyList<CapitalProjectionPoint> points,
        int displayMonthLimit)
    {
        var count = Math.Min(points.Count, displayMonthLimit);

        if (count <= 0)
        {
            return [];
        }

        var trimmed = points.Take(count).ToList();

        if (trimmed.Count <= MaximumPoints)
        {
            return trimmed;
        }

        var selected = trimmed.Take(MonthlyPointsKept).ToList();
        var remaining = trimmed.Skip(MonthlyPointsKept).ToList();
        var slots = MaximumPoints - MonthlyPointsKept;
        var stride = Math.Max(1, (int)Math.Ceiling(remaining.Count / (double)slots));

        for (var index = 0; index < remaining.Count; index += stride)
        {
            selected.Add(remaining[index]);
        }

        if (selected[^1] != remaining[^1])
        {
            if (selected.Count >= MaximumPoints)
            {
                selected[^1] = remaining[^1];
            }
            else
            {
                selected.Add(remaining[^1]);
            }
        }

        return selected;
    }

    private static IReadOnlyList<CapitalYearRow> BuildYears(
        IReadOnlyDictionary<int, YearAccumulator> years,
        int displayYearLimit)
    {
        return years
            .Where(pair => pair.Key <= displayYearLimit)
            .OrderBy(pair => pair.Key)
            .Select(pair => new CapitalYearRow(
                pair.Key,
                Round(pair.Value.YearEndCapital),
                Round(pair.Value.NetSavings),
                Round(pair.Value.ProvidentDeposit),
                Round(pair.Value.Return),
                Round(pair.Value.NetSavings + pair.Value.ProvidentDeposit + pair.Value.Return),
                pair.Value.IsGoalYear))
            .ToList();
    }

    private static decimal MonthlyRate(decimal annualRate)
    {
        if (annualRate == 0m)
        {
            return 0m;
        }

        var monthly = Math.Pow((double)(1m + annualRate), 1.0 / 12.0) - 1.0;

        return decimal.Round((decimal)monthly, 10, MidpointRounding.AwayFromZero);
    }

    private static decimal GrowthFactor(decimal annualGrowth, int yearOffset)
    {
        if (yearOffset <= 0 || annualGrowth == 0m)
        {
            return 1m;
        }

        return (decimal)Math.Pow((double)(1m + annualGrowth), yearOffset);
    }

    private static decimal NonNegative(decimal value)
    {
        return value < 0m ? 0m : value;
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private sealed class YearAccumulator
    {
        public decimal YearEndCapital { get; set; }

        public decimal NetSavings { get; set; }

        public decimal ProvidentDeposit { get; set; }

        public decimal Return { get; set; }

        public bool IsGoalYear { get; set; }
    }
}

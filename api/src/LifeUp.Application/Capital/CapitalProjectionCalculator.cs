using System.Globalization;
using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Capital;

public sealed class CapitalProjectionCalculator
{
    public const decimal DefaultSafeWithdrawalRatePercent = 4m;

    private const int MaximumMonths = 1200;
    private const int DisplayYears = 10;
    private const int MaximumPoints = 120;
    private const int MonthlyPointsKept = 12;
    private const decimal ProjectionBalanceLimit = decimal.MaxValue / 1024m;

    public CapitalProjectionResult Calculate(CapitalProjectionInput input)
    {
        return Calculate(input, dynamicFire: false);
    }

    public CapitalProjectionResult CalculateFire(CapitalProjectionInput input)
    {
        return Calculate(input, dynamicFire: true);
    }

    internal CapitalProjectionResult CalculateFireWithPortfolioReturn(CapitalProjectionInput input, decimal annualReturnPercent)
    {
        return Calculate(input, dynamicFire: true, annualReturnPercent);
    }

    private static CapitalProjectionResult Calculate(
        CapitalProjectionInput input,
        bool dynamicFire,
        decimal? portfolioAnnualReturnPercent = null)
    {
        var notes = new List<string>
        {
            "按当前资产结构推演"
        };

        if (input.AnnualBonus > 0m)
        {
            notes.Add("年终奖按发放月计入。");
        }
        var cash = NonNegative(input.Cash);
        var investment = NonNegative(input.Investment);
        var provident = NonNegative(input.Provident);
        var nonProvident = cash + investment;
        var total = nonProvident + provident;
        var monthlyNet = Round(input.MonthlyIncome - input.MonthlyExpense + input.MonthlyProvidentDeposit);
        var blendedReturn = total > 0m
            ? Round((investment * input.InvestmentAnnualReturnPercent + provident * input.ProvidentAnnualReturnPercent) / total)
            : 0m;
        var expenseGrowth = input.ExpenseGrowthPercent / 100m;
        var withdrawalRate = (input.SafeWithdrawalRatePercent
            ?? (dynamicFire ? DefaultSafeWithdrawalRatePercent : 0m)) / 100m;
        var annualExpense = NonNegative(input.FireMonthlyExpense ?? 0m) * 12m;
        var targetAmount = input.TargetAmount;
        if (dynamicFire)
        {
            targetAmount = annualExpense > 0m ? Round(annualExpense / withdrawalRate) : null;
        }

        if (investment == 0m && input.InvestmentAnnualReturnPercent != 0m && portfolioAnnualReturnPercent is null)
        {
            notes.Add("当前没有投资资产，投资年化暂不生效。");
        }

        var reachedAtStart = dynamicFire
            ? annualExpense > 0m && total * withdrawalRate >= annualExpense
            : targetAmount is decimal startTarget && total >= startTarget;
        if (reachedAtStart)
        {
            notes.Add("目标已达成。");

            return new CapitalProjectionResult(
                CapitalProjectionStatus.Reached,
                targetAmount,
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
        if (portfolioAnnualReturnPercent is decimal portfolioReturn)
        {
            nonProvidentRate = providentMonthlyRate = MonthlyRate(portfolioReturn / 100m);
            blendedReturn = portfolioReturn;
        }

        var incomeGrowth = input.IncomeGrowthPercent / 100m;
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
        var iterationLimit = targetAmount is null ? DisplayYears * 12 : MaximumMonths;

        while (monthIndex < iterationLimit)
        {
            var month = firstMonth.AddMonths(monthIndex);
            var yearOffset = month.Year - input.AsOfDate.Year;
            var income = Round(Grow(input.MonthlyIncome, incomeGrowth, yearOffset));
            var expense = Round(Grow(input.MonthlyExpense, expenseGrowth, yearOffset));
            var providentDeposit = Round(Grow(input.MonthlyProvidentDeposit, incomeGrowth, yearOffset));
            var netSavings = income - expense;
            var bonus = month.Month == input.BonusPayoutMonth
                ? Round(Grow(input.AnnualBonus, incomeGrowth, yearOffset))
                : 0m;
            var netAccumulation = netSavings + providentDeposit + bonus;
            var nonProvidentReturn = nonProvidentBalance * nonProvidentRate;
            var providentReturn = providentBalance * providentMonthlyRate;
            var monthReturn = nonProvidentReturn + providentReturn;

            nonProvidentBalance += nonProvidentReturn;
            providentBalance += providentReturn;
            var capitalBeforeContributions = nonProvidentBalance + providentBalance;
            nonProvidentBalance = Math.Min(ProjectionBalanceLimit, nonProvidentBalance + netSavings + bonus);
            providentBalance = Math.Min(ProjectionBalanceLimit, providentBalance + providentDeposit);

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

            var capitalAfterContributions = nonProvidentBalance + providentBalance;
            var actualNet = capitalAfterContributions - capitalBeforeContributions;

            if (capitalAfterContributions == 0m && netAccumulation < 0m)
            {
                depleted = true;
            }

            monthIndex++;
            var capital = Round(capitalAfterContributions);
            var expectedAnnualExpense = Round(GrowAnnualExpense(annualExpense, expenseGrowth, yearOffset));
            var passiveAnnualCashflow = Round(capital * withdrawalRate);
            var monthKey = month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            points.Add(new CapitalProjectionPoint(
                monthKey,
                capital,
                Round(actualNet),
                Round(monthReturn),
                passiveAnnualCashflow,
                expectedAnnualExpense));

            if (!years.TryGetValue(month.Year, out var year))
            {
                year = new YearAccumulator();
                years[month.Year] = year;
            }

            year.NetSavings += actualNet - providentDeposit;
            year.ProvidentDeposit += providentDeposit;
            year.Return += monthReturn;
            year.YearEndCapital = capital;
            year.PassiveMonthlyCashflow = Round(passiveAnnualCashflow / 12m);
            year.FreedomPercent = ProgressPercent(passiveAnnualCashflow, expectedAnnualExpense);

            var reachedThisMonth = dynamicFire
                ? targetAmount is not null && (passiveAnnualCashflow >= expectedAnnualExpense
                    || capital >= Round(expectedAnnualExpense / withdrawalRate))
                : targetAmount is decimal target && capital >= target;
            if (reachedThisMonth)
            {
                reached = true;
                etaMonth = monthKey;
                monthsRemaining = monthIndex;
                year.IsGoalYear = true;
                break;
            }

            if (depleted && (!dynamicFire || targetAmount is not null))
            {
                break;
            }
        }

        var status = dynamicFire && targetAmount is null
            ? CapitalProjectionStatus.NoTarget
            : ResolveStatus(targetAmount, reached, depleted && !(dynamicFire && reached), points);
        AddStatusNotes(notes, status, points);
        var displayMonthLimit = dynamicFire && reached ? points.Count : DisplayYears * 12;
        var displayYearLimit = input.AsOfDate.Year + DisplayYears;

        if (monthsRemaining is int goalMonths && (dynamicFire || goalMonths <= DisplayYears * 12))
        {
            displayYearLimit = firstMonth.AddMonths(goalMonths - 1).Year;
        }

        var displayPoints = SelectPoints(points, displayMonthLimit);
        var displayYears = BuildYears(years, displayYearLimit);
        var progress = ProgressPercent(total, targetAmount ?? 0m);
        var remaining = targetAmount is decimal remainingTarget
            ? Round(Math.Max(0m, remainingTarget - total))
            : dynamicFire ? 0m : (decimal?)null;

        return new CapitalProjectionResult(
            status,
            targetAmount ?? (dynamicFire ? 0m : (decimal?)null),
            remaining,
            progress,
            etaMonth,
            monthsRemaining,
            monthlyNet,
            blendedReturn,
            displayPoints,
            displayYears,
            notes)
        {
            MonthlyPoints = points
        };
    }

    public static CoastFireDto CalculateCoast(
        CapitalProjectionInput input,
        CapitalProjectionResult projection,
        int? coastTargetYear)
    {
        var targetYear = coastTargetYear ?? input.AsOfDate.Year + 25;
        var targetYearSource = coastTargetYear.HasValue ? "user" : "default";
        var unachievable = new CoastFireDto(targetYear, targetYearSource, false, false, null, 0m, null, null, null);
        var years = targetYear - input.AsOfDate.Year;
        var annualExpense = (input.FireMonthlyExpense ?? 0m) * 12m;
        var returnRate = projection.BlendedAnnualReturnPercent / 100m;
        var expenseGrowth = input.ExpenseGrowthPercent / 100m;

        if (years < 1)
        {
            return unachievable with { Note = "目标年份需晚于当前年份" };
        }

        if (annualExpense <= 0m)
        {
            return unachievable with { Note = "请先设定或记录有效生活支出" };
        }

        if (returnRate <= 0m || returnRate <= expenseGrowth)
        {
            return unachievable with { Note = "当前组合年化不高于支出年增长，暂无法仅靠复利滑行" };
        }

        var withdrawalRate = (input.SafeWithdrawalRatePercent ?? DefaultSafeWithdrawalRatePercent) / 100m;
        var coastThreshold = (double)(annualExpense / withdrawalRate)
            * Math.Pow((double)((1m + expenseGrowth) / (1m + returnRate)), years);
        var threshold = Round((decimal)coastThreshold);
        var capital = NonNegative(input.Cash) + NonNegative(input.Investment) + NonNegative(input.Provident);
        var reached = capital >= threshold;
        var result = new CoastFireDto(
            targetYear, targetYearSource, true, reached, threshold,
            threshold > 0m ? ProgressPercent(capital, threshold) : 100m,
            null, reached ? 0 : null, null);

        if (reached)
        {
            return result;
        }

        var monthLimit = Math.Min(projection.MonthlyPoints.Count, years * 12 - 1);
        for (var index = 0; index < monthLimit; index++)
        {
            var point = projection.MonthlyPoints[index];
            var months = index + 1;
            var monthlyThreshold = coastThreshold * Math.Pow((double)(1m + returnRate), months / 12d);
            if ((double)point.Capital >= monthlyThreshold)
            {
                return result with { EtaMonth = point.Month, MonthsRemaining = months };
            }
        }

        return result;
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
                pair.Value.IsGoalYear,
                pair.Value.PassiveMonthlyCashflow,
                pair.Value.FreedomPercent))
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

    private static decimal Grow(decimal amount, decimal annualGrowth, int yearOffset)
    {
        if (amount == 0m || yearOffset <= 0 || annualGrowth == 0m)
        {
            return amount;
        }

        var factor = Math.Pow((double)(1m + annualGrowth), yearOffset);

        if (!double.IsFinite(factor) || factor >= (double)LedgerEntry.MaximumAmount)
        {
            return LedgerEntry.MaximumAmount;
        }

        var grown = (decimal)factor * amount;

        return grown > LedgerEntry.MaximumAmount ? LedgerEntry.MaximumAmount : grown;
    }

    private static decimal NonNegative(decimal value)
    {
        return value < 0m ? 0m : value;
    }

    private static decimal GrowAnnualExpense(decimal amount, decimal annualGrowth, int yearOffset)
    {
        if (amount == 0m || yearOffset <= 0 || annualGrowth == 0m)
        {
            return amount;
        }

        var factor = Math.Pow((double)(1m + annualGrowth), yearOffset);
        var limit = ProjectionBalanceLimit / amount;
        return factor >= (double)limit ? ProjectionBalanceLimit : amount * (decimal)factor;
    }

    private static decimal ProgressPercent(decimal value, decimal target)
    {
        return target > 0m ? Round1(Math.Min(value, target) / target * 100m) : 0m;
    }

    internal static decimal Round1(decimal value)
    {
        return decimal.Round(value, 1, MidpointRounding.AwayFromZero);
    }

    internal static decimal Round(decimal value)
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

        public decimal PassiveMonthlyCashflow { get; set; }

        public decimal FreedomPercent { get; set; }
    }
}

namespace LifeUp.Domain.Ledgers;

public sealed record CapitalGoal(
    decimal? TargetAmount,
    decimal? MonthlyIncome,
    decimal? MonthlyExpense,
    decimal? MonthlyProvidentDeposit,
    decimal? IncomeGrowthPercent,
    decimal? ExpenseGrowthPercent,
    decimal? InvestmentAnnualReturnPercent,
    decimal? ProvidentAnnualReturnPercent,
    decimal? AnnualBonus = null,
    int? BonusPayoutMonth = null,
    bool UseSalaryProfile = false,
    string GoalMode = "fire",
    string FireMode = "standard",
    decimal? SafeWithdrawalRatePercent = null,
    IReadOnlyList<string>? ExcludedExpenseCategories = null,
    IReadOnlyList<string>? LeanExpenseCategories = null,
    decimal? FireMonthlyExpenseOverride = null,
    decimal? LeanMonthlyExpenseOverride = null,
    int? CoastTargetYear = null)
{
    private const decimal MinimumPercent = -100m;
    private const decimal MaximumPercent = 100m;
    private const int MaximumExpenseCategories = 100;

    public CapitalGoal Normalize()
    {
        return this with
        {
            GoalMode = string.IsNullOrWhiteSpace(GoalMode) ? "fire" : GoalMode,
            FireMode = string.IsNullOrWhiteSpace(FireMode) ? "standard" : FireMode,
            ExcludedExpenseCategories = NormalizeCategories(ExcludedExpenseCategories),
            LeanExpenseCategories = NormalizeCategories(LeanExpenseCategories)
        };
    }

    public static bool TryValidate(CapitalGoal? goal, out IReadOnlyList<string> errors)
    {
        var validationErrors = new List<string>();

        if (goal is null)
        {
            errors = validationErrors;
            return true;
        }

        if (!IsValidTarget(goal.TargetAmount))
        {
            validationErrors.Add("目标金额必须大于零、不超过 10 亿元，且最多保留两位小数。");
        }

        if (!IsValidAmount(goal.MonthlyIncome))
        {
            validationErrors.Add("月收入必须不小于零、不超过 10 亿元，且最多保留两位小数。");
        }

        if (!IsValidAmount(goal.MonthlyExpense))
        {
            validationErrors.Add("月支出必须不小于零、不超过 10 亿元，且最多保留两位小数。");
        }

        if (!IsValidAmount(goal.MonthlyProvidentDeposit))
        {
            validationErrors.Add("公积金月缴存必须不小于零、不超过 10 亿元，且最多保留两位小数。");
        }

        if (!IsValidPercent(goal.IncomeGrowthPercent))
        {
            validationErrors.Add("收入年增长必须在 -100 到 100 之间，且最多保留两位小数。");
        }

        if (!IsValidPercent(goal.ExpenseGrowthPercent))
        {
            validationErrors.Add("支出年增长必须在 -100 到 100 之间，且最多保留两位小数。");
        }

        if (!IsValidPercent(goal.InvestmentAnnualReturnPercent))
        {
            validationErrors.Add("投资年化必须在 -100 到 100 之间，且最多保留两位小数。");
        }

        if (!IsValidPercent(goal.ProvidentAnnualReturnPercent))
        {
            validationErrors.Add("公积金年化必须在 -100 到 100 之间，且最多保留两位小数。");
        }

        if (!IsValidAmount(goal.AnnualBonus))
        {
            validationErrors.Add("年终奖到手必须不小于零、不超过 10 亿元，且最多保留两位小数。");
        }

        if (goal.BonusPayoutMonth is < 1 or > 12)
        {
            validationErrors.Add("年终奖发放月必须在 1 到 12 之间。");
        }

        var normalized = goal.Normalize();
        if (normalized.GoalMode is not ("fire" or "custom"))
        {
            validationErrors.Add("目标模式必须为 fire 或 custom。");
        }

        if (normalized.FireMode is not ("standard" or "lean"))
        {
            validationErrors.Add("FIRE 模式必须为 standard 或 lean。");
        }

        if (goal.SafeWithdrawalRatePercent is decimal rate
            && (rate is < 1m or > 15m || decimal.Round(rate, 2) != rate))
        {
            validationErrors.Add("安全提取率必须在 1 到 15 之间，且最多保留两位小数。");
        }

        if (!IsValidAmount(goal.FireMonthlyExpenseOverride) || !IsValidAmount(goal.LeanMonthlyExpenseOverride))
        {
            validationErrors.Add("FIRE 月支出必须不小于零、不超过 10 亿元，且最多保留两位小数。");
        }

        if (goal.CoastTargetYear is < 2025 or > 2200)
        {
            validationErrors.Add("Coast 目标年份必须在 2025 到 2200 之间。");
        }

        if (!IsValidCategories(normalized.ExcludedExpenseCategories)
            || !IsValidCategories(normalized.LeanExpenseCategories))
        {
            validationErrors.Add(
                $"每组 FIRE 分类最多 {MaximumExpenseCategories} 项，名称不能超过 {EntryCategory.MaximumNameLength} 个字符。");
        }

        errors = validationErrors;
        return validationErrors.Count == 0;
    }

    private static IReadOnlyList<string>? NormalizeCategories(IReadOnlyList<string>? categories)
    {
        return categories?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsValidCategories(IReadOnlyList<string>? categories)
    {
        return categories is null
            || (categories.Count <= MaximumExpenseCategories
                && categories.All(name => name.Length <= EntryCategory.MaximumNameLength));
    }

    private static bool IsValidTarget(decimal? value)
    {
        return value is null
            || (value > 0m
                && value <= LedgerEntry.MaximumAmount
                && decimal.Round(value.Value, 2) == value.Value);
    }

    private static bool IsValidAmount(decimal? value)
    {
        return value is null
            || (value >= 0m
                && value <= LedgerEntry.MaximumAmount
                && decimal.Round(value.Value, 2) == value.Value);
    }

    private static bool IsValidPercent(decimal? value)
    {
        return value is null
            || (value > MinimumPercent
                && value <= MaximumPercent
                && decimal.Round(value.Value, 2) == value.Value);
    }
}

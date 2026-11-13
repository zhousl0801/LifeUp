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
    bool UseSalaryProfile = false)
{
    private const decimal MinimumPercent = -100m;
    private const decimal MaximumPercent = 100m;

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

        errors = validationErrors;
        return validationErrors.Count == 0;
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

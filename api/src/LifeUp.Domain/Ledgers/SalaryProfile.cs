namespace LifeUp.Domain.Ledgers;

public sealed record SalaryProfile(
    decimal BaseMonthlySalary,
    decimal NetMonthlySalary,
    decimal ProvidentMonthly,
    decimal BonusMonths,
    decimal BonusNetAmount,
    int BonusPayoutMonth,
    int PayDay)
{
    public const decimal MaximumBonusMonths = 24m;

    public static bool TryValidate(SalaryProfile? profile, out IReadOnlyList<string> errors)
    {
        var validationErrors = new List<string>();

        if (profile is null)
        {
            errors = validationErrors;
            return true;
        }

        if (!IsValidPositiveAmount(profile.BaseMonthlySalary))
        {
            validationErrors.Add("税前 Base 必须大于 0、不超过 10 亿元，且最多保留两位小数。");
        }

        if (!IsValidPositiveAmount(profile.NetMonthlySalary))
        {
            validationErrors.Add("税后工资必须大于 0、不超过 10 亿元，且最多保留两位小数。");
        }

        if (!IsValidNonNegativeAmount(profile.ProvidentMonthly))
        {
            validationErrors.Add("公积金月缴存不能为负、不能超过 10 亿元，且最多保留两位小数。");
        }

        if (!IsValidBonusMonths(profile.BonusMonths))
        {
            validationErrors.Add("年终奖月数必须在 0 到 24 之间，且最多保留两位小数。");
        }

        if (!IsValidNonNegativeAmount(profile.BonusNetAmount))
        {
            validationErrors.Add("年终奖到手不能为负、不能超过 10 亿元，且最多保留两位小数。");
        }

        if (profile.BonusMonths > 0m && profile.BonusNetAmount <= 0m)
        {
            validationErrors.Add("填写年终奖月数后，年终奖到手必须大于 0。");
        }

        if (profile.BonusPayoutMonth is < 1 or > 12)
        {
            validationErrors.Add("年终奖发放月必须在 1 到 12 之间。");
        }

        if (profile.PayDay is < 1 or > 31)
        {
            validationErrors.Add("发薪日必须在 1 到 31 之间。");
        }

        errors = validationErrors;
        return validationErrors.Count == 0;
    }

    private static bool IsValidPositiveAmount(decimal value)
    {
        return value > 0m
            && value <= LedgerEntry.MaximumAmount
            && decimal.Round(value, 2) == value;
    }

    private static bool IsValidNonNegativeAmount(decimal value)
    {
        return value >= 0m
            && value <= LedgerEntry.MaximumAmount
            && decimal.Round(value, 2) == value;
    }

    private static bool IsValidBonusMonths(decimal value)
    {
        return value >= 0m
            && value <= MaximumBonusMonths
            && decimal.Round(value, 2) == value;
    }
}

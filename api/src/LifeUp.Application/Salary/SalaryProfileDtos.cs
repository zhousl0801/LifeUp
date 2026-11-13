using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Salary;

public sealed record SalaryProfileDto(
    decimal BaseMonthlySalary,
    decimal NetMonthlySalary,
    decimal ProvidentMonthly,
    decimal BonusMonths,
    decimal BonusNetAmount,
    int BonusPayoutMonth,
    int PayDay)
{
    public static SalaryProfileDto From(SalaryProfile profile)
    {
        return new SalaryProfileDto(
            profile.BaseMonthlySalary,
            profile.NetMonthlySalary,
            profile.ProvidentMonthly,
            profile.BonusMonths,
            profile.BonusNetAmount,
            profile.BonusPayoutMonth,
            profile.PayDay);
    }
}

public sealed record SalaryProfileOverviewDto(SalaryProfileDto? Profile);

public sealed record SaveSalaryProfileRequest(
    Guid RequestId,
    decimal BaseMonthlySalary,
    decimal NetMonthlySalary,
    decimal ProvidentMonthly,
    decimal BonusMonths,
    decimal BonusNetAmount,
    int BonusPayoutMonth,
    int PayDay)
{
    public SalaryProfile ToProfile()
    {
        return new SalaryProfile(
            BaseMonthlySalary,
            NetMonthlySalary,
            ProvidentMonthly,
            BonusMonths,
            BonusNetAmount,
            BonusPayoutMonth,
            PayDay);
    }
}

public sealed record SalaryProfileActionRequest(Guid RequestId);

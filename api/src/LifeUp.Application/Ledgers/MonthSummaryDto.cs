namespace LifeUp.Application.Ledgers;

public sealed record MonthSummaryDto(decimal Income, decimal Expense, decimal Surplus, bool HasRecords)
{
    /// <summary>
    /// 本月已记账的公积金缴存合计。
    /// </summary>
    public decimal ProvidentDeposit { get; init; }

    /// <summary>
    /// 本月已记账的公积金提取合计，以正数表示。
    /// </summary>
    public decimal ProvidentWithdrawal { get; init; }
}

namespace LifeUp.Application.Ledgers;

public enum ChangeScope
{
    Day,
    Month
}

public sealed record AssetChangeDto(string AssetType, decimal? Change, bool IsNew)
{
    /// <summary>
    /// 用于计算该资产变化比例的期初金额。暂无可比基线时为空。
    /// </summary>
    public decimal? BaselineAmount { get; init; }
}

/// <summary>当日资产变动明细。现金支出为正数，参与合计时扣减，盈亏和差额保留正负号。</summary>
public sealed record DayChangeBreakdownDto(
    decimal CashIncome,
    decimal CashExpense,
    decimal ProvidentDeposit,
    DayInvestmentChangeDto? Fund,
    DayInvestmentChangeDto? Stock,
    DayInvestmentChangeDto? Gold,
    decimal CashAdjustment,
    decimal HoldingAdjustment,
    decimal OtherDifference);

/// <summary>当日投资盈亏的估值与资金依据。新录入和持仓核对金额从盈亏中扣除。</summary>
public sealed record DayInvestmentChangeDto(
    decimal Profit,
    decimal PreviousValue,
    decimal CurrentValue,
    decimal CashTransfer,
    decimal OpeningValue,
    decimal Adjustment);

public sealed record MonthChangeDto(
    DateOnly From,
    DateOnly To,
    decimal? TotalChange,
    decimal LivingSurplus,
    decimal ProvidentDeposit,
    decimal? InvestmentProfit,
    decimal ReconciliationDifference,
    bool IsComplete,
    IReadOnlyList<string> Notes,
    IReadOnlyList<AssetChangeDto> AssetChanges,
    decimal? ComparableChange = null,
    decimal? ComparableBase = null,
    decimal? AddedRecords = null,
    decimal? MarketValueChange = null,
    decimal? NetDeposit = null,
    bool HasComparableData = false,
    DayChangeBreakdownDto? DayBreakdown = null);

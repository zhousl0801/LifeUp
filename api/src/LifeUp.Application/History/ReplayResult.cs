using LifeUp.Domain.Assets;

namespace LifeUp.Application.History;

public sealed record HoldingReplay(Guid? PositionId, decimal Quantity, decimal Cost, decimal RealizedProfit,
    DateOnly? CoverageFrom, bool CanRebuild, decimal? Price, DateOnly? PriceDate, string? PriceSource,
    decimal PendingAmount = 0m, decimal AppliedShares = 0m, decimal PendingArrivalAmount = 0m,
    decimal? PendingMark = null)
{
    public decimal? Value => Price is null ? (Quantity == 0m && CoverageFrom is not null ? 0m : null) : decimal.Round(Quantity * Price.Value, 2);

    /// <summary>
    /// 待确认买入的估值：份额在 T 日就已确定，确认前后都按 T 日净值折算，避免确认当天凭空产生差价。
    /// T 日净值未知时退回承诺金额（未开始计算盈亏）。
    /// </summary>
    public decimal PendingValue => PendingMark ?? PendingAmount;
}

public sealed record AdjustmentReplay(Guid Id, decimal PreviousBalance, decimal Difference);

public sealed record ReplayResult(
    DateOnly AsOfDate,
    decimal? CashBalance,
    DateOnly? CashCoverageFrom,
    DateOnly CashBalanceDate,
    decimal? ProvidentBalance,
    DateOnly? ProvidentCoverageFrom,
    DateOnly ProvidentBalanceDate,
    IReadOnlyList<HoldingReplay> Funds,
    IReadOnlyList<HoldingReplay> Stocks,
    HoldingReplay? Gold,
    IReadOnlyList<AdjustmentReplay> Adjustments,
    IReadOnlyList<string> Issues)
{
    public decimal KnownTotalAssets => (CashBalance ?? 0m) + (ProvidentBalance ?? 0m)
        + Funds.Sum(position => (position.Value ?? 0m) + position.PendingValue + position.PendingArrivalAmount)
        + Stocks.Sum(position => position.Value ?? 0m)
        + (Gold?.Value ?? 0m) + (Gold?.PendingArrivalAmount ?? 0m);

    public decimal? TotalAssets => Issues.Count == 0 ? KnownTotalAssets : null;
}

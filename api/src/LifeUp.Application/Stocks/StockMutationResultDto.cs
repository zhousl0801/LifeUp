namespace LifeUp.Application.Stocks;

public sealed record StockMutationResultDto(
    StockPositionDto Position,
    decimal CashBalance,
    StockActivityDto Activity);

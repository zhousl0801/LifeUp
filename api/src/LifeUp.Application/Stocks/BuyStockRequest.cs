using LifeUp.Domain.Assets;

namespace LifeUp.Application.Stocks;

public sealed record BuyStockRequest(
    StockMarket Market,
    string Code,
    string? Name,
    decimal Shares,
    decimal Price,
    decimal Fee,
    DateOnly BusinessDate,
    string? Note,
    Guid RequestId);

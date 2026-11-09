using LifeUp.Domain.Assets;

namespace LifeUp.Application.Quotes;

public sealed record ExternalStockSearchResult(
    StockMarket Market,
    string Code,
    string Name,
    string Currency = "CNY",
    decimal? Price = null,
    DateOnly? PriceDate = null);

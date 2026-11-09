using LifeUp.Domain.Assets;

namespace LifeUp.Application.Stocks;

public sealed record StockSearchResultDto(
    StockMarket Market,
    string Code,
    string Name,
    string Currency = "CNY",
    decimal? Price = null,
    DateOnly? PriceDate = null);

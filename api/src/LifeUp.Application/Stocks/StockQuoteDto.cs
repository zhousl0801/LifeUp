using LifeUp.Domain.Assets;

namespace LifeUp.Application.Stocks;

// 只读的最新行情，用于录入持仓时自动带上现价，不落库。
public sealed record StockQuoteDto(
    StockMarket Market,
    string Code,
    decimal Price,
    DateOnly PriceDate,
    decimal? PreviousClose,
    string Source);

using LifeUp.Domain.Assets;

namespace LifeUp.Application.Stocks;

public sealed record StockActivityDto(
    Guid Id,
    Guid PositionId,
    string PositionName,
    StockActivityType Type,
    decimal? Shares,
    decimal Amount,
    decimal CashEffect,
    decimal Fee,
    decimal? Price,
    DateOnly BusinessDate,
    string? Note,
    DateTimeOffset CreatedAt);

using LifeUp.Domain.Assets;
using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Cash;

public sealed record CashActivityDto(
    Guid Id,
    CashActivityType Type,
    string Title,
    string? Note,
    decimal Amount,
    DateOnly BusinessDate,
    LedgerEntrySource? Source,
    AssetType? AssetType);

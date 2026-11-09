using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Ledgers;

public sealed record LedgerEntryDto(
    Guid Id,
    LedgerEntryKind Kind,
    string Category,
    decimal Amount,
    DateOnly BusinessDate,
    string? Note,
    LedgerEntrySource Source,
    LedgerEntryStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReversedAt,
    string? ReversalReason,
    Guid? RelatedCashAdjustmentId = null);

using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Ledgers;

public sealed record CreateLedgerEntryRequest(
    LedgerEntryKind Kind,
    string Category,
    decimal Amount,
    DateOnly BusinessDate,
    string? Note,
    Guid RequestId,
    Guid? RelatedCashAdjustmentId = null);

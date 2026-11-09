namespace LifeUp.Application.Ledgers;

public sealed record ReverseLedgerEntryRequest(string? Reason, Guid RequestId = default);

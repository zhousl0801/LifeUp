using LifeUp.Domain.Ledgers;

namespace LifeUp.Application.Ledgers;

public sealed record EntryPage(IReadOnlyList<LedgerEntry> Items, int TotalCount);

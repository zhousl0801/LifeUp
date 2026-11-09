namespace LifeUp.Application.Ledgers;

public sealed record EntryMutationResultDto(LedgerEntryDto Entry, decimal CashBalance);

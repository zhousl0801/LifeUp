namespace LifeUp.Application.Ledgers;

public sealed record InitializeLedgerRequest(DateOnly StartDate, decimal CashBalance, DateOnly? CashAsOfDate = null);

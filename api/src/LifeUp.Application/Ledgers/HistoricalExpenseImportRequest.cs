namespace LifeUp.Application.Ledgers;

public sealed record HistoricalExpenseImportRequest(Guid RequestId, IReadOnlyList<HistoricalExpenseRow> Rows);

public sealed record HistoricalExpenseRow(DateOnly Month, string Category, decimal Amount);

public sealed record HistoricalExpenseImportResult(
    EntryOperationStatus Status,
    int Added,
    int Skipped,
    decimal AddedAmount,
    decimal? CashBalance,
    IReadOnlyList<string> Errors)
{
    public static HistoricalExpenseImportResult Failure(EntryOperationStatus status, string message)
    {
        return new(status, 0, 0, 0m, null, [message]);
    }
}

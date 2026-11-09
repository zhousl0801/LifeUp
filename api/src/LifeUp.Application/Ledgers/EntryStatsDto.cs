namespace LifeUp.Application.Ledgers;

public sealed record EntryStatsDto(
    EntryPeriod Period,
    DateOnly Anchor,
    DateOnly From,
    DateOnly To,
    int CoveredDays,
    decimal Income,
    decimal Expense,
    decimal IncomeDailyAverage,
    decimal ExpenseDailyAverage,
    IReadOnlyList<EntryStatsPointDto> Points,
    IReadOnlyList<EntryStatsCategoryDto> IncomeCategories,
    IReadOnlyList<EntryStatsCategoryDto> ExpenseCategories,
    string? Note,
    IReadOnlyList<EntryStatsCategoryDto>? IncomeGroups = null,
    IReadOnlyList<EntryStatsCategoryDto>? ExpenseGroups = null);

public sealed record EntryStatsPointDto(
    DateOnly Date,
    string Label,
    decimal Income,
    decimal Expense,
    string? Category = null);

public sealed record EntryStatsCategoryDto(
    string Category,
    decimal Amount,
    int Count,
    decimal Share);

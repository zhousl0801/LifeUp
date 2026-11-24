using LifeUp.Domain.Ledgers;

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
    IReadOnlyList<EntryStatsCategoryDto>? ExpenseGroups = null,
    EntryStatsComparisonDto? Comparison = null,
    IReadOnlyList<EntryStatsTopEntryDto>? TopExpenses = null,
    IReadOnlyList<EntryStatsTopEntryDto>? TopIncomes = null);

public sealed record EntryStatsPointDto(
    DateOnly Date,
    string Label,
    decimal Income,
    decimal Expense,
    string? Category = null,
    string? IconKey = null);

public sealed record EntryStatsCategoryDto(
    string Category,
    decimal Amount,
    int Count,
    decimal Share,
    decimal? PreviousAmount = null,
    string? IconKey = null,
    string? ParentGroup = null);

public sealed record EntryStatsComparisonDto(
    bool Comparable,
    string Label,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    decimal PreviousIncome,
    decimal PreviousExpense,
    decimal PreviousSurplus,
    decimal? IncomeDelta,
    decimal? IncomeDeltaPercent,
    decimal? ExpenseDelta,
    decimal? ExpenseDeltaPercent,
    decimal? SurplusDelta);

public sealed record EntryStatsTopEntryDto(
    Guid Id,
    LedgerEntryKind Kind,
    DateOnly BusinessDate,
    string Category,
    string? IconKey,
    string? Note,
    decimal Amount);

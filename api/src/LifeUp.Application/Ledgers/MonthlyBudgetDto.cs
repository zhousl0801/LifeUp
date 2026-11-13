using System.Text.Json.Serialization;

namespace LifeUp.Application.Ledgers;

public sealed record MonthlyBudgetDto(string Month, decimal? Amount);

public sealed record UpdateMonthlyBudgetRequest([property: JsonRequired] decimal? Amount);

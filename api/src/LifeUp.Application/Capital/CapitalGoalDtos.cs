namespace LifeUp.Application.Capital;

public sealed record CapitalEffectiveValue(decimal Value, string Source);

public sealed record CapitalEffectiveInputs(
    CapitalEffectiveValue MonthlyIncome,
    CapitalEffectiveValue MonthlyExpense,
    CapitalEffectiveValue MonthlyProvidentDeposit,
    CapitalEffectiveValue IncomeGrowthPercent,
    CapitalEffectiveValue ExpenseGrowthPercent,
    CapitalEffectiveValue InvestmentAnnualReturnPercent,
    CapitalEffectiveValue ProvidentAnnualReturnPercent);

namespace LifeUp.Application.Cash;

public sealed record CashReconcileResultDto(
    bool IsBalanced,
    decimal Difference,
    decimal CashBalance,
    CashAdjustmentDto? Adjustment);

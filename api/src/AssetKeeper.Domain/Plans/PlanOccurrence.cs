namespace AssetKeeper.Domain.Plans;

public sealed class PlanOccurrence
{
    public const int MaximumPeriodKeyLength = 32;
    public const int MaximumErrorLength = 300;

    /// <summary>同一期次在当天最多自动尝试的次数，超过后不再自动重试，避免反复失败。</summary>
    public const int MaximumAutoAttempts = 3;

    private PlanOccurrence()
    {
    }

    public Guid Id { get; private set; }

    public Guid PlanId { get; private set; }

    public Guid LedgerId { get; private set; }

    public string PeriodKey { get; private set; } = string.Empty;

    public AutoPlanType Type { get; private set; }

    public string PlanName { get; private set; } = string.Empty;

    public Guid? FundPositionId { get; private set; }

    public decimal Amount { get; private set; }

    public decimal Fee { get; private set; }

    public int PlanVersion { get; private set; }

    public DateOnly DueDate { get; private set; }

    public TimeOnly DueTime { get; private set; }

    public PlanOccurrenceStatus Status { get; private set; } = PlanOccurrenceStatus.Pending;

    public Guid? RelatedRecordId { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset? ExecutedAt { get; private set; }

    /// <summary>已执行过的次数（含手动重试与失败重试）。</summary>
    public int AttemptCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int Version { get; private set; }

    public static PlanOccurrence Create(
        AutoPlan plan,
        string periodKey,
        DateOnly dueDate,
        TimeOnly dueTime,
        DateTimeOffset now)
    {
        if (plan.Id == Guid.Empty || plan.LedgerId == Guid.Empty)
        {
            throw new ArgumentException("计划标识无效。");
        }

        if (string.IsNullOrWhiteSpace(periodKey) || periodKey.Length > MaximumPeriodKeyLength)
        {
            throw new ArgumentException("计划期次标识无效。", nameof(periodKey));
        }

        if (dueDate == default)
        {
            throw new ArgumentException("计划应执行日期无效。", nameof(dueDate));
        }

        return new PlanOccurrence
        {
            Id = Guid.NewGuid(),
            PlanId = plan.Id,
            LedgerId = plan.LedgerId,
            PeriodKey = periodKey,
            Type = plan.Type,
            PlanName = plan.Name,
            FundPositionId = plan.FundPositionId,
            Amount = plan.Amount,
            Fee = plan.Fee,
            PlanVersion = plan.Version,
            DueDate = dueDate,
            DueTime = dueTime,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>记录一次执行尝试；次数用于封顶当天的自动重试。</summary>
    public void BeginAttempt(DateTimeOffset now)
    {
        AttemptCount++;
        UpdatedAt = now;
    }

    /// <summary>失败的期次只在到期当天自动重试，且受次数上限约束；隔天的失败期次留给人工决定。</summary>
    public bool CanAutoRetry(DateOnly businessDate)
    {
        return Status == PlanOccurrenceStatus.Failed
            && DueDate == businessDate
            && AttemptCount < MaximumAutoAttempts;
    }

    public void Complete(Guid relatedRecordId, DateTimeOffset now)
    {
        Status = PlanOccurrenceStatus.Completed;
        RelatedRecordId = relatedRecordId;
        ErrorMessage = null;
        ExecutedAt = now;
        UpdatedAt = now;
        Version++;
    }

    public void Wait(string error, DateTimeOffset now)
    {
        Status = PlanOccurrenceStatus.Waiting;
        ErrorMessage = Normalize(error);
        UpdatedAt = now;
        Version++;
    }

    public void Fail(string error, DateTimeOffset now)
    {
        Status = PlanOccurrenceStatus.Failed;
        ErrorMessage = Normalize(error);
        UpdatedAt = now;
        Version++;
    }

    public void Skip(DateTimeOffset now)
    {
        if (Status is PlanOccurrenceStatus.Completed)
        {
            throw new InvalidOperationException("已记账的期次不能跳过。");
        }

        Status = PlanOccurrenceStatus.Skipped;
        ErrorMessage = null;
        UpdatedAt = now;
        Version++;
    }

    public void Retry(DateTimeOffset now)
    {
        if (Status is PlanOccurrenceStatus.Completed or PlanOccurrenceStatus.Skipped)
        {
            throw new InvalidOperationException("已记账或已跳过的期次不能重试。");
        }

        Status = PlanOccurrenceStatus.Pending;
        ErrorMessage = null;
        ExecutedAt = null;
        UpdatedAt = now;
        Version++;
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= MaximumErrorLength ? value.Trim() : value.Trim()[..MaximumErrorLength];
    }
}

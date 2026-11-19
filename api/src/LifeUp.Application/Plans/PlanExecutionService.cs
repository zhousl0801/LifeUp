using System.Collections.Concurrent;
using System.Text.Json;
using LifeUp.Application.Assets;
using LifeUp.Application.Funds;
using LifeUp.Application.Ledgers;
using LifeUp.Application.Mutations;
using LifeUp.Application.Provident;
using LifeUp.Domain.Ledgers;
using LifeUp.Domain.Plans;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LifeUp.Application.Plans;

public sealed class PlanExecutionService
{
    private const string RecordOccurrenceOperation = "plan.occurrence.record";

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> RunLocks = new();

    private readonly ILedgerRepository _ledgerRepository;
    private readonly IPlanRepository _planRepository;
    private readonly BusinessDateProvider _dates;
    private readonly LedgerEntryService _entries;
    private readonly ProvidentFundService _provident;
    private readonly FundService _funds;
    private readonly ILogger<PlanExecutionService> _logger;

    public PlanExecutionService(
        ILedgerRepository ledgerRepository,
        IPlanRepository planRepository,
        BusinessDateProvider dates,
        LedgerEntryService entries,
        ProvidentFundService provident,
        FundService funds,
        ILogger<PlanExecutionService>? logger = null)
    {
        _ledgerRepository = ledgerRepository;
        _planRepository = planRepository;
        _dates = dates;
        _entries = entries;
        _provident = provident;
        _funds = funds;
        _logger = logger ?? NullLogger<PlanExecutionService>.Instance;
    }

    public async Task<PlanRunResultDto> RunDueAsync(CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetForUpdateAsync(cancellationToken);

        if (ledger is null)
        {
            return new PlanRunResultDto(0, 0, 0, 0);
        }

        var runLock = RunLocks.GetOrAdd(ledger.Id, _ => new SemaphoreSlim(1, 1));

        if (!await runLock.WaitAsync(0, cancellationToken))
        {
            return new PlanRunResultDto(0, 0, 0, 0);
        }

        try
        {
            var now = _dates.UtcNow;
            var today = _dates.Today;
            var plans = await _planRepository.GetPlansAsync(ledger.Id, cancellationToken);
            var activePlans = plans.Where(plan => plan.IsActive).ToList();
            var activeIds = activePlans.Select(plan => plan.Id).ToHashSet();
            var existingKeys = new Dictionary<Guid, HashSet<string>>(
                await _planRepository.GetOccurrenceKeysAsync(ledger.Id, activeIds, cancellationToken));
            var created = 0;

            foreach (var plan in activePlans)
            {
                if (!existingKeys.TryGetValue(plan.Id, out var planKeys))
                {
                    planKeys = new HashSet<string>(StringComparer.Ordinal);
                    existingKeys[plan.Id] = planKeys;
                }

                foreach (var period in PlanSchedule.GetDuePeriods(plan, plan.GenerateFrom, today))
                {
                    if (_dates.ToBusinessInstant(period.DueDate, period.DueTime) > now)
                    {
                        continue;
                    }

                    if (!planKeys.Add(period.PeriodKey))
                    {
                        continue;
                    }

                    await _planRepository.AddOccurrenceAsync(
                        PlanOccurrence.Create(plan, period.PeriodKey, period.DueDate, period.DueTime, now),
                        cancellationToken);
                    created++;
                }
            }

            if (created > 0)
            {
                await _planRepository.SaveChangesAsync(cancellationToken);
            }

            var pending = (await _planRepository.GetPendingOccurrencesAsync(ledger.Id, today, cancellationToken))
                .Where(occurrence => activeIds.Contains(occurrence.PlanId)
                    && (occurrence.Status == PlanOccurrenceStatus.Pending || occurrence.CanAutoRetry(today))
                    && _dates.ToBusinessInstant(occurrence.DueDate, occurrence.DueTime) <= now)
                .OrderBy(occurrence => occurrence.DueDate)
                .ThenBy(occurrence => occurrence.DueTime)
                .ThenBy(occurrence => PlanSchedule.TypeOrder(occurrence.Type))
                .ThenBy(occurrence => occurrence.PlanId)
                .ToList();
            var completed = 0;
            var waiting = 0;
            var failed = 0;

            foreach (var occurrence in pending)
            {
                var status = await ExecuteOccurrenceAsync(ledger.Id, occurrence, cancellationToken);

                if (status == PlanOccurrenceStatus.Completed)
                {
                    completed++;
                }
                else if (status == PlanOccurrenceStatus.Waiting)
                {
                    waiting++;
                }
                else if (status == PlanOccurrenceStatus.Failed)
                {
                    failed++;
                }
            }

            return new PlanRunResultDto(created, completed, waiting, failed);
        }
        finally
        {
            runLock.Release();
        }
    }

    public async Task<AssetOperationResult<RecordOccurrenceResultDto>> RecordOccurrenceAsync(
        Guid occurrenceId,
        PlanActionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.RequestId == Guid.Empty)
        {
            return AssetOperationResult<RecordOccurrenceResultDto>.Invalid(["请求标识无效。"]);
        }

        var ledger = await _ledgerRepository.GetAsync(cancellationToken);

        if (ledger is null)
        {
            return AssetOperationResult<RecordOccurrenceResultDto>.NotFound("账本尚未初始化。");
        }

        var fingerprint = MutationExecutor.ComputeFingerprint(new { OccurrenceId = occurrenceId, Request = request });
        var receipt = await _ledgerRepository.FindMutationReceiptAsync(ledger.Id, request.RequestId, cancellationToken);

        if (receipt is not null)
        {
            if (receipt.Operation != RecordOccurrenceOperation || receipt.Fingerprint != fingerprint)
            {
                return AssetOperationResult<RecordOccurrenceResultDto>.Conflict("该请求标识已用于其他内容，请核对原记录后重新操作。");
            }

            return JsonSerializer.Deserialize<AssetOperationResult<RecordOccurrenceResultDto>>(
                receipt.ResponseJson,
                MutationExecutor.JsonOptions)!;
        }

        if (await _ledgerRepository.FindEntryByRequestIdAsync(ledger.Id, request.RequestId, cancellationToken) is not null
            || await _ledgerRepository.FindAssetTransactionByRequestIdAsync(ledger.Id, request.RequestId, cancellationToken) is not null)
        {
            return AssetOperationResult<RecordOccurrenceResultDto>.Conflict("该历史请求已入账，缺少完整请求快照，请查看原记录，勿重复保存。");
        }

        var runLock = RunLocks.GetOrAdd(ledger.Id, _ => new SemaphoreSlim(1, 1));

        if (!await runLock.WaitAsync(0, cancellationToken))
        {
            return AssetOperationResult<RecordOccurrenceResultDto>.Conflict("已有计划执行任务在运行，请稍后重试。");
        }

        try
        {
            var occurrence = await _planRepository.GetOccurrenceForUpdateAsync(
                ledger.Id,
                occurrenceId,
                cancellationToken);

            if (occurrence is null)
            {
                return AssetOperationResult<RecordOccurrenceResultDto>.NotFound("执行期次不存在。");
            }

            if (occurrence.Status is PlanOccurrenceStatus.Completed or PlanOccurrenceStatus.Skipped)
            {
                return AssetOperationResult<RecordOccurrenceResultDto>.Invalid(["已记账或已跳过的期次不能补记。"]);
            }

            if (_dates.ToBusinessInstant(occurrence.DueDate, occurrence.DueTime) > _dates.UtcNow)
            {
                return AssetOperationResult<RecordOccurrenceResultDto>.Invalid(["该期次尚未到期，请等待自动执行。"]);
            }

            // 子业务各自以 occurrence.Id 为幂等键独立落库；这里不再嵌套外层 MutationExecutor，
            // 避免内层 DiscardChanges 或 SaveChanges 破坏外层跟踪状态。
            var status = await ExecuteOccurrenceAsync(ledger.Id, occurrence, cancellationToken);
            var current = await _planRepository.GetOccurrenceForUpdateAsync(ledger.Id, occurrenceId, cancellationToken)
                ?? occurrence;
            var recorded = status == PlanOccurrenceStatus.Completed;
            var result = AssetOperationResult<RecordOccurrenceResultDto>.Success(new RecordOccurrenceResultDto(
                PlanService.Map(current),
                recorded,
                recorded ? null : current.ErrorMessage));

            _ledgerRepository.DiscardChanges();
            await _ledgerRepository.AddMutationReceiptAsync(
                MutationReceipt.Create(
                    ledger.Id,
                    request.RequestId,
                    RecordOccurrenceOperation,
                    fingerprint,
                    JsonSerializer.Serialize(result, MutationExecutor.JsonOptions),
                    _dates.UtcNow,
                    beforeJson: null),
                cancellationToken);

            try
            {
                await _ledgerRepository.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                _ledgerRepository.DiscardChanges();
                var existing = await _ledgerRepository.FindMutationReceiptAsync(ledger.Id, request.RequestId, cancellationToken);

                if (existing is not null
                    && existing.Operation == RecordOccurrenceOperation
                    && existing.Fingerprint == fingerprint)
                {
                    return JsonSerializer.Deserialize<AssetOperationResult<RecordOccurrenceResultDto>>(
                        existing.ResponseJson,
                        MutationExecutor.JsonOptions)!;
                }

                return AssetOperationResult<RecordOccurrenceResultDto>.Conflict("数据已被其他操作修改，请核实保存结果或刷新后重试。");
            }

            return result;
        }
        finally
        {
            runLock.Release();
        }
    }

    private async Task<PlanOccurrenceStatus> ExecuteOccurrenceAsync(
        Guid ledgerId,
        PlanOccurrence occurrence,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ExecuteBusinessAsync(occurrence, cancellationToken);
            var current = await _planRepository.GetOccurrenceForUpdateAsync(ledgerId, occurrence.Id, cancellationToken);

            if (current is null)
            {
                return PlanOccurrenceStatus.Pending;
            }

            // 另一个执行者可能已经完成或跳过该期次，不能再被本次结果覆盖。
            if (current.Status is PlanOccurrenceStatus.Completed or PlanOccurrenceStatus.Skipped)
            {
                return current.Status;
            }

            current.BeginAttempt(_dates.UtcNow);

            if (result.Succeeded)
            {
                current.Complete(result.RelatedRecordId ?? occurrence.Id, _dates.UtcNow);
            }
            else if (result.Failed)
            {
                current.Fail(result.Error ?? "计划执行失败。", _dates.UtcNow);
            }
            else
            {
                current.Wait(result.Error ?? "余额或资料不足，等待处理。", _dates.UtcNow);
            }

            await _planRepository.SaveChangesAsync(cancellationToken);

            return current.Status;
        }
        catch (Exception exception)
        {
            try
            {
                var current = await _planRepository.GetOccurrenceForUpdateAsync(ledgerId, occurrence.Id, cancellationToken);

                if (current is not null)
                {
                    if (current.Status is PlanOccurrenceStatus.Completed or PlanOccurrenceStatus.Skipped)
                    {
                        return current.Status;
                    }

                    current.BeginAttempt(_dates.UtcNow);
                    current.Fail(exception.Message, _dates.UtcNow);
                    await _planRepository.SaveChangesAsync(cancellationToken);

                    return PlanOccurrenceStatus.Failed;
                }
            }
            catch (Exception markException)
            {
                _logger.LogWarning(
                    markException,
                    "标记计划期次 {OccurrenceId} 失败状态时出错，原异常：{OriginalError}",
                    occurrence.Id,
                    exception.Message);
            }

            return PlanOccurrenceStatus.Failed;
        }
    }

    private async Task<EntryCategory?> ResolveBonusCategoryAsync(
        Guid ledgerId,
        CancellationToken cancellationToken)
    {
        var category = await _ledgerRepository.GetCategoryBySystemKeyAsync(
            ledgerId,
            LedgerEntryKind.Income,
            "bonus",
            cancellationToken);

        if (category is { IsActive: true })
        {
            return category;
        }

        var categories = await _ledgerRepository.GetCategoriesAsync(ledgerId, cancellationToken);

        return categories.FirstOrDefault(item => item.Kind == LedgerEntryKind.Income
            && item.Name == "奖金"
            && item.IsActive);
    }

    private async Task<ExecutionResult> ExecuteBusinessAsync(PlanOccurrence occurrence, CancellationToken cancellationToken)
    {
        switch (occurrence.Type)
        {
            case AutoPlanType.SalaryIncome:
            case AutoPlanType.BonusIncome:
            {
                var category = occurrence.Type == AutoPlanType.BonusIncome
                    ? await ResolveBonusCategoryAsync(occurrence.LedgerId, cancellationToken)
                    : await _ledgerRepository.GetCategoryBySystemKeyAsync(
                        occurrence.LedgerId,
                        LedgerEntryKind.Income,
                        "salary",
                        cancellationToken);

                if (category is null && occurrence.Type == AutoPlanType.BonusIncome)
                {
                    category = await _ledgerRepository.GetCategoryBySystemKeyAsync(
                        occurrence.LedgerId,
                        LedgerEntryKind.Income,
                        "salary",
                        cancellationToken);
                }

                var result = await _entries.CreateAsync(
                    new CreateLedgerEntryRequest(
                        LedgerEntryKind.Income,
                        category?.Name ?? "工资",
                        occurrence.Amount,
                        occurrence.DueDate,
                        occurrence.PlanName,
                        occurrence.Id),
                    LedgerEntrySource.Automatic,
                    cancellationToken);

                if (result.Status is EntryOperationStatus.Created or EntryOperationStatus.Success)
                {
                    return ExecutionResult.Success(result.Result?.Entry.Id);
                }

                return result.Status == EntryOperationStatus.Conflict
                    ? ExecutionResult.Fail(Join(result.Errors))
                    : ExecutionResult.Wait(Join(result.Errors));
            }
            case AutoPlanType.ProvidentDeposit:
            {
                var result = await _provident.DepositAsync(
                    new ProvidentDepositRequest(occurrence.Amount, occurrence.DueDate, occurrence.PlanName, occurrence.Id),
                    cancellationToken);

                return FromAssetResult(result);
            }
            case AutoPlanType.ProvidentWithdrawal:
            {
                var result = await _provident.WithdrawAsync(
                    new ProvidentWithdrawalRequest(occurrence.Amount, occurrence.DueDate, occurrence.PlanName, occurrence.Id),
                    cancellationToken);

                return FromAssetResult(result);
            }
            default:
            {
                if (occurrence.FundPositionId is not Guid positionId)
                {
                    return ExecutionResult.Wait("基金定投缺少基金持仓。");
                }

                var result = await _funds.BuyAsync(
                    new BuyFundRequest(
                        positionId,
                        occurrence.Amount,
                        occurrence.DueDate,
                        occurrence.PlanName,
                        occurrence.Id,
                        occurrence.Fee,
                        OrderTime: occurrence.DueTime),
                    cancellationToken);

                return FromAssetResult(result);
            }
        }
    }

    private static ExecutionResult FromAssetResult<T>(AssetOperationResult<T> result)
        where T : class
    {
        if (result.Status is AssetOperationStatus.Created or AssetOperationStatus.Success)
        {
            var id = result.Result switch
            {
                ProvidentMutationResultDto provident => provident.Activity.Id,
                FundMutationResultDto fund => fund.Activity.Id,
                _ => (Guid?)null
            };

            return ExecutionResult.Success(id);
        }

        return result.Status is AssetOperationStatus.Conflict or AssetOperationStatus.Unavailable
            ? ExecutionResult.Fail(Join(result.Errors))
            : ExecutionResult.Wait(Join(result.Errors));
    }

    private static string Join(IReadOnlyList<string> errors)
    {
        return errors.Count == 0 ? "计划执行失败。" : string.Join(" ", errors);
    }

    private sealed record ExecutionResult(bool Succeeded, bool Failed, Guid? RelatedRecordId, string? Error)
    {
        public static ExecutionResult Success(Guid? relatedRecordId)
        {
            return new ExecutionResult(true, false, relatedRecordId, null);
        }

        public static ExecutionResult Wait(string error)
        {
            return new ExecutionResult(false, false, null, error);
        }

        public static ExecutionResult Fail(string error)
        {
            return new ExecutionResult(false, true, null, error);
        }
    }
}

using LifeUp.Application.Assets;
using LifeUp.Application.Ledgers;
using LifeUp.Application.Mutations;
using LifeUp.Application.Plans;
using LifeUp.Domain.Ledgers;
using LifeUp.Domain.Plans;

namespace LifeUp.Application.Salary;

public sealed class SalaryProfileService
{
    private const string SaveOperation = "salary.save";
    private const string DisableOperation = "salary.disable";
    private const int SalaryPlanDay = 15;

    private static readonly TimeOnly PlanTime = new(9, 0);

    private readonly ILedgerRepository _ledgerRepository;
    private readonly IPlanRepository _planRepository;
    private readonly BusinessDateProvider _dates;

    public SalaryProfileService(
        ILedgerRepository ledgerRepository,
        IPlanRepository planRepository,
        BusinessDateProvider dates)
    {
        _ledgerRepository = ledgerRepository;
        _planRepository = planRepository;
        _dates = dates;
    }

    public async Task<SalaryProfileOverviewDto?> GetAsync(CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetAsync(cancellationToken);

        return ledger is null
            ? null
            : new SalaryProfileOverviewDto(ToDto(ledger.GetSalaryProfile()));
    }

    public Task<AssetOperationResult<SalaryProfileOverviewDto>> SaveAsync(
        SaveSalaryProfileRequest request,
        CancellationToken cancellationToken)
    {
        return new MutationExecutor(_ledgerRepository, _dates).ExecuteAsync(
            SaveOperation,
            request.RequestId,
            request,
            () => SaveCoreAsync(request, cancellationToken),
            result => result.Status is AssetOperationStatus.Created or AssetOperationStatus.Success,
            result => result with { Status = AssetOperationStatus.Success },
            AssetOperationResult<SalaryProfileOverviewDto>.Conflict,
            message => AssetOperationResult<SalaryProfileOverviewDto>.Invalid([message]),
            cancellationToken);
    }

    public Task<AssetOperationResult<SalaryProfileOverviewDto>> DisableAsync(
        SalaryProfileActionRequest request,
        CancellationToken cancellationToken)
    {
        return new MutationExecutor(_ledgerRepository, _dates).ExecuteAsync(
            DisableOperation,
            request.RequestId,
            request,
            () => DisableCoreAsync(cancellationToken),
            result => result.Status is AssetOperationStatus.Success,
            result => result,
            AssetOperationResult<SalaryProfileOverviewDto>.Conflict,
            message => AssetOperationResult<SalaryProfileOverviewDto>.Invalid([message]),
            cancellationToken);
    }

    private async Task<AssetOperationResult<SalaryProfileOverviewDto>> SaveCoreAsync(
        SaveSalaryProfileRequest request,
        CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetForUpdateAsync(cancellationToken);

        if (ledger is null)
        {
            return AssetOperationResult<SalaryProfileOverviewDto>.NotFound("账本尚未初始化。");
        }

        var profile = request.ToProfile();

        if (!SalaryProfile.TryValidate(profile, out var errors))
        {
            return AssetOperationResult<SalaryProfileOverviewDto>.Invalid(errors);
        }

        var previous = ledger.GetSalaryProfile();
        var now = _dates.UtcNow;

        ledger.SetSalaryProfile(profile, now);

        if (previous is null && ledger.GetCapitalGoal() is { } goal)
        {
            ledger.SetCapitalGoal(goal with { UseSalaryProfile = true }, now);
        }

        await SyncPlansAsync(ledger, profile, previous, now, cancellationToken);

        var overview = new SalaryProfileOverviewDto(SalaryProfileDto.From(profile));

        return previous is null
            ? AssetOperationResult<SalaryProfileOverviewDto>.Created(overview)
            : AssetOperationResult<SalaryProfileOverviewDto>.Success(overview);
    }

    private async Task<AssetOperationResult<SalaryProfileOverviewDto>> DisableCoreAsync(
        CancellationToken cancellationToken)
    {
        var ledger = await _ledgerRepository.GetForUpdateAsync(cancellationToken);

        if (ledger is null)
        {
            return AssetOperationResult<SalaryProfileOverviewDto>.NotFound("账本尚未初始化。");
        }

        var profile = ledger.GetSalaryProfile();
        var now = _dates.UtcNow;

        if (profile is null)
        {
            return AssetOperationResult<SalaryProfileOverviewDto>.Success(
                new SalaryProfileOverviewDto(null));
        }

        ledger.SetSalaryProfile(null, now);

        if (ledger.GetCapitalGoal() is { } goal)
        {
            ledger.SetCapitalGoal(
                goal with
                {
                    UseSalaryProfile = false,
                    MonthlyIncome = goal.MonthlyIncome ?? profile.NetMonthlySalary,
                    MonthlyProvidentDeposit = goal.MonthlyProvidentDeposit ?? profile.ProvidentMonthly,
                    AnnualBonus = goal.AnnualBonus ?? (profile.BonusMonths > 0m ? profile.BonusNetAmount : 0m),
                    BonusPayoutMonth = goal.BonusPayoutMonth ?? profile.BonusPayoutMonth
                },
                now);
        }

        return AssetOperationResult<SalaryProfileOverviewDto>.Success(
            new SalaryProfileOverviewDto(null));
    }

    private async Task SyncPlansAsync(
        Ledger ledger,
        SalaryProfile profile,
        SalaryProfile? previous,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var plans = await _planRepository.GetPlansAsync(ledger.Id, cancellationToken);
        var salaryPlan = plans.FirstOrDefault(plan => plan.Type == AutoPlanType.SalaryIncome);
        var providentPlan = plans.FirstOrDefault(plan => plan.Type == AutoPlanType.ProvidentDeposit);
        var bonusPlan = plans.FirstOrDefault(plan => plan.Type == AutoPlanType.BonusIncome);
        var day = profile.PayDay > 0 ? profile.PayDay : SalaryPlanDay;

        await UpsertPlanAsync(
            ledger,
            salaryPlan,
            AutoPlanType.SalaryIncome,
            "工资到账",
            profile.NetMonthlySalary,
            AutoPlanCycle.Monthly,
            day,
            null,
            NextMonthlyDue(_dates.Today, day),
            now,
            cancellationToken);

        if (profile.ProvidentMonthly > 0m)
        {
            await UpsertPlanAsync(
                ledger,
                providentPlan,
                AutoPlanType.ProvidentDeposit,
                "公积金缴存",
                profile.ProvidentMonthly,
                AutoPlanCycle.Monthly,
                day,
                null,
                NextMonthlyDue(_dates.Today, day),
                now,
                cancellationToken);

            if (previous is { ProvidentMonthly: 0m } && providentPlan is not null)
            {
                await ResumeIfPausedAsync(ledger.Id, providentPlan.Id, now, cancellationToken);
            }
        }
        else if (providentPlan is not null)
        {
            await PauseIfActiveAsync(ledger.Id, providentPlan.Id, now, cancellationToken);
        }

        if (profile.BonusMonths > 0m)
        {
            await UpsertPlanAsync(
                ledger,
                bonusPlan,
                AutoPlanType.BonusIncome,
                "年终奖",
                profile.BonusNetAmount,
                AutoPlanCycle.Yearly,
                day,
                profile.BonusPayoutMonth,
                NextYearlyDue(_dates.Today, profile.BonusPayoutMonth, day),
                now,
                cancellationToken);

            if (previous is { BonusMonths: 0m } && bonusPlan is not null)
            {
                await ResumeIfPausedAsync(ledger.Id, bonusPlan.Id, now, cancellationToken);
            }
        }
        else if (bonusPlan is not null)
        {
            await PauseIfActiveAsync(ledger.Id, bonusPlan.Id, now, cancellationToken);
        }
    }

    private async Task UpsertPlanAsync(
        Ledger ledger,
        AutoPlan? existing,
        AutoPlanType type,
        string defaultName,
        decimal amount,
        AutoPlanCycle cycle,
        int dayOfMonth,
        int? monthOfYear,
        DateOnly startDate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (existing is null)
        {
            var plan = AutoPlan.Create(
                ledger.Id,
                type,
                defaultName,
                amount,
                cycle,
                dayOfMonth,
                null,
                PlanTime,
                startDate,
                null,
                now,
                0m,
                AutoPlanTradingScope.ThreeMarkets,
                monthOfYear);

            await _planRepository.AddPlanAsync(plan, cancellationToken);
            return;
        }

        var tracked = await _planRepository.GetPlanForUpdateAsync(ledger.Id, existing.Id, cancellationToken);

        if (tracked is null)
        {
            return;
        }

        var floor = _dates.ToBusinessInstant(_dates.Today, tracked.Time) > now
            ? _dates.Today
            : _dates.Today.AddDays(1);

        tracked.Update(
            tracked.Type,
            tracked.Name,
            amount,
            tracked.Cycle,
            dayOfMonth,
            null,
            tracked.Time,
            tracked.StartDate,
            tracked.FundPositionId,
            now,
            floor,
            tracked.Fee,
            tracked.TradingScope,
            monthOfYear);
    }

    private async Task PauseIfActiveAsync(
        Guid ledgerId,
        Guid planId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetPlanForUpdateAsync(ledgerId, planId, cancellationToken);

        if (plan is { IsActive: true })
        {
            plan.Pause(now);
        }
    }

    private async Task ResumeIfPausedAsync(
        Guid ledgerId,
        Guid planId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetPlanForUpdateAsync(ledgerId, planId, cancellationToken);

        if (plan is { IsActive: false })
        {
            plan.Resume(_dates.Today, now);
        }
    }

    private static DateOnly NextMonthlyDue(DateOnly today, int day)
    {
        var currentMonthDay = Math.Min(day, DateTime.DaysInMonth(today.Year, today.Month));
        var currentMonth = new DateOnly(today.Year, today.Month, currentMonthDay);

        if (currentMonth > today)
        {
            return currentMonth;
        }

        var nextMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(1);
        var nextMonthDay = Math.Min(day, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));

        return new DateOnly(nextMonth.Year, nextMonth.Month, nextMonthDay);
    }

    private static DateOnly NextYearlyDue(DateOnly today, int month, int day)
    {
        var currentYearDay = Math.Min(day, DateTime.DaysInMonth(today.Year, month));
        var currentYear = new DateOnly(today.Year, month, currentYearDay);

        if (currentYear > today)
        {
            return currentYear;
        }

        var nextYear = today.Year + 1;
        var nextYearDay = Math.Min(day, DateTime.DaysInMonth(nextYear, month));

        return new DateOnly(nextYear, month, nextYearDay);
    }

    private static SalaryProfileDto? ToDto(SalaryProfile? profile)
    {
        return profile is null ? null : SalaryProfileDto.From(profile);
    }
}

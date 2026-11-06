using AssetKeeper.Application.Plans;
using AssetKeeper.Domain.Plans;
using Microsoft.EntityFrameworkCore;

namespace AssetKeeper.Infrastructure.Persistence;

public sealed class PlanRepository : IPlanRepository
{
    private readonly AppDbContext _dbContext;

    public PlanRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AutoPlan>> GetPlansAsync(Guid ledgerId, CancellationToken cancellationToken)
    {
        return await _dbContext.AutoPlans
            .AsNoTracking()
            .Where(plan => plan.LedgerId == ledgerId)
            .OrderBy(plan => plan.Type)
            .ThenBy(plan => plan.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<AutoPlan?> GetPlanForUpdateAsync(Guid ledgerId, Guid planId, CancellationToken cancellationToken)
    {
        return _dbContext.AutoPlans
            .SingleOrDefaultAsync(plan => plan.Id == planId && plan.LedgerId == ledgerId, cancellationToken);
    }

    public async Task AddPlanAsync(AutoPlan plan, CancellationToken cancellationToken)
    {
        await _dbContext.AutoPlans.AddAsync(plan, cancellationToken);
    }

    public async Task<IReadOnlyList<PlanOccurrence>> GetOccurrencesByPlanAsync(Guid planId, CancellationToken cancellationToken)
    {
        return await _dbContext.PlanOccurrences
            .Where(occurrence => occurrence.PlanId == planId)
            .ToListAsync(cancellationToken);
    }

    public void RemovePlan(AutoPlan plan)
    {
        _dbContext.AutoPlans.Remove(plan);
    }

    public void RemoveOccurrences(IEnumerable<PlanOccurrence> occurrences)
    {
        _dbContext.PlanOccurrences.RemoveRange(occurrences);
    }

    public async Task<IReadOnlyList<PlanOccurrence>> GetOccurrencesAsync(
        Guid ledgerId,
        DateOnly? from,
        DateOnly? to,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.PlanOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.LedgerId == ledgerId);

        if (from is not null)
        {
            query = query.Where(occurrence => occurrence.DueDate >= from);
        }

        if (to is not null)
        {
            query = query.Where(occurrence => occurrence.DueDate <= to);
        }

        return await query
            .OrderByDescending(occurrence => occurrence.DueDate)
            .ThenByDescending(occurrence => occurrence.DueTime)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<PlanOccurrence> Items, int Total)> GetOccurrencePageAsync(
        Guid ledgerId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.PlanOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.LedgerId == ledgerId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(occurrence => occurrence.DueDate)
            .ThenByDescending(occurrence => occurrence.DueTime)
            .ThenBy(occurrence => occurrence.PlanId)
            .ThenBy(occurrence => occurrence.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<PlanOccurrence>> GetPendingOccurrencesAsync(
        Guid ledgerId,
        DateOnly? dueThrough,
        CancellationToken cancellationToken)
    {
        // 同时取回失败的期次：当天内的失败可以在后续运行里自动重试，是否重试由服务层判定。
        var query = _dbContext.PlanOccurrences
            .Where(occurrence => occurrence.LedgerId == ledgerId
                && (occurrence.Status == PlanOccurrenceStatus.Pending
                    || occurrence.Status == PlanOccurrenceStatus.Failed));

        if (dueThrough is not null)
        {
            query = query.Where(occurrence => occurrence.DueDate <= dueThrough);
        }

        return await query
            .OrderBy(occurrence => occurrence.DueDate)
            .ThenBy(occurrence => occurrence.DueTime)
            .ThenBy(occurrence => occurrence.Type)
            .ThenBy(occurrence => occurrence.PlanId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, HashSet<string>>> GetOccurrenceKeysAsync(
        Guid ledgerId,
        IReadOnlyCollection<Guid> planIds,
        CancellationToken cancellationToken)
    {
        if (planIds.Count == 0)
        {
            return new Dictionary<Guid, HashSet<string>>();
        }

        var rows = await _dbContext.PlanOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.LedgerId == ledgerId && planIds.Contains(occurrence.PlanId))
            .Select(occurrence => new { occurrence.PlanId, occurrence.PeriodKey })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.PlanId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.PeriodKey).ToHashSet(StringComparer.Ordinal));
    }

    public async Task<IReadOnlyList<PlanOccurrence>> GetActionableOccurrencesAsync(Guid ledgerId, CancellationToken cancellationToken)
    {
        return await _dbContext.PlanOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.LedgerId == ledgerId
                && (occurrence.Status == PlanOccurrenceStatus.Pending
                    || occurrence.Status == PlanOccurrenceStatus.Waiting
                    || occurrence.Status == PlanOccurrenceStatus.Failed))
            .OrderBy(occurrence => occurrence.DueDate)
            .ThenBy(occurrence => occurrence.DueTime)
            .ToListAsync(cancellationToken);
    }

    public Task<PlanOccurrence?> GetOccurrenceForUpdateAsync(Guid ledgerId, Guid occurrenceId, CancellationToken cancellationToken)
    {
        return _dbContext.PlanOccurrences
            .SingleOrDefaultAsync(occurrence => occurrence.Id == occurrenceId && occurrence.LedgerId == ledgerId, cancellationToken);
    }

    public Task<PlanOccurrence?> FindOccurrenceAsync(Guid planId, string periodKey, CancellationToken cancellationToken)
    {
        return _dbContext.PlanOccurrences
            .AsNoTracking()
            .SingleOrDefaultAsync(
                occurrence => occurrence.PlanId == planId && occurrence.PeriodKey == periodKey,
                cancellationToken);
    }

    public async Task AddOccurrenceAsync(PlanOccurrence occurrence, CancellationToken cancellationToken)
    {
        await _dbContext.PlanOccurrences.AddAsync(occurrence, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

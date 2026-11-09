using LifeUp.Domain.Plans;

namespace LifeUp.Application.Plans;

public interface IPlanRepository
{
    Task<IReadOnlyList<AutoPlan>> GetPlansAsync(Guid ledgerId, CancellationToken cancellationToken);

    Task<AutoPlan?> GetPlanForUpdateAsync(Guid ledgerId, Guid planId, CancellationToken cancellationToken);

    Task AddPlanAsync(AutoPlan plan, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanOccurrence>> GetOccurrencesByPlanAsync(Guid planId, CancellationToken cancellationToken);

    void RemovePlan(AutoPlan plan);

    void RemoveOccurrences(IEnumerable<PlanOccurrence> occurrences);

    Task<IReadOnlyList<PlanOccurrence>> GetOccurrencesAsync(
        Guid ledgerId,
        DateOnly? from,
        DateOnly? to,
        int take,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<PlanOccurrence> Items, int Total)> GetOccurrencePageAsync(
        Guid ledgerId,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanOccurrence>> GetPendingOccurrencesAsync(
        Guid ledgerId,
        DateOnly? dueThrough,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, HashSet<string>>> GetOccurrenceKeysAsync(
        Guid ledgerId,
        IReadOnlyCollection<Guid> planIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PlanOccurrence>> GetActionableOccurrencesAsync(Guid ledgerId, CancellationToken cancellationToken);

    Task<PlanOccurrence?> GetOccurrenceForUpdateAsync(Guid ledgerId, Guid occurrenceId, CancellationToken cancellationToken);

    Task<PlanOccurrence?> FindOccurrenceAsync(Guid planId, string periodKey, CancellationToken cancellationToken);

    Task AddOccurrenceAsync(PlanOccurrence occurrence, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

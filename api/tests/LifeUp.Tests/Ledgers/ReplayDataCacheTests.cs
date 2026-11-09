using LifeUp.Application.History;
using LifeUp.Application.Ledgers;
using LifeUp.Domain.Assets;
using LifeUp.Domain.Cash;
using LifeUp.Domain.Ledgers;
using LifeUp.Infrastructure.Persistence;
using LifeUp.Tests.Quotes;
using Microsoft.EntityFrameworkCore;

namespace LifeUp.Tests.Ledgers;

public sealed class ReplayDataCacheTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RepositorySaveInvalidatesCachedReplayData()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var cache = new ReplayDataCache();
        var repository = new LedgerRepository(db, cache);
        var ledgerService = new LedgerService(repository, new BusinessDateProvider(new FixedTimeProvider(Now)));
        await ledgerService.InitializeAsync(new InitializeLedgerRequest(Today, 1000m), CancellationToken.None);
        var ledgerId = db.Ledgers.Single().Id;

        var first = await repository.GetReplayDataReadOnlyAsync(ledgerId, CancellationToken.None);
        Assert.Empty(first.Entries);

        var entry = LedgerEntry.Create(
            ledgerId,
            LedgerEntryKind.Expense,
            "餐饮",
            100m,
            Today,
            Today,
            null,
            LedgerEntrySource.Manual,
            Guid.NewGuid(),
            Now);
        await repository.AddEntryAsync(entry, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        var second = await repository.GetReplayDataReadOnlyAsync(ledgerId, CancellationToken.None);

        Assert.Single(second.Entries);
    }

    [Fact]
    public async Task CachedReadDoesNotReloadUntilInvalidated()
    {
        var cache = new ReplayDataCache();
        var ledgerId = Guid.NewGuid();
        var calls = 0;

        var first = await cache.GetOrCreateAsync(ledgerId, _ => Load(), CancellationToken.None);
        var cached = await cache.GetOrCreateAsync(ledgerId, _ => Load(), CancellationToken.None);

        Assert.Same(first, cached);
        Assert.Equal(1, calls);

        cache.InvalidateAll();
        var reloaded = await cache.GetOrCreateAsync(ledgerId, _ => Load(), CancellationToken.None);

        Assert.NotSame(first, reloaded);
        Assert.Equal(2, calls);

        Task<LedgerReplayData> Load()
        {
            calls++;
            return Task.FromResult(Empty());
        }
    }

    [Fact]
    public async Task ConcurrentMissesShareOneLoad()
    {
        var cache = new ReplayDataCache();
        var ledgerId = Guid.NewGuid();
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = Enumerable.Range(0, 5)
            .Select(_ => cache.GetOrCreateAsync(
                ledgerId,
                async _ =>
                {
                    Interlocked.Increment(ref calls);
                    await release.Task;
                    return Empty();
                },
                CancellationToken.None))
            .ToList();

        await Task.Delay(50);
        release.SetResult();
        var results = await Task.WhenAll(reads);

        Assert.Equal(1, calls);
        Assert.All(results, result => Assert.Same(results[0], result));
    }

    [Fact]
    public async Task InvalidationDuringLoadPreventsStaleCache()
    {
        var cache = new ReplayDataCache();
        var ledgerId = Guid.NewGuid();
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loading = cache.GetOrCreateAsync(
            ledgerId,
            async _ =>
            {
                Interlocked.Increment(ref calls);
                await release.Task;
                return Empty();
            },
            CancellationToken.None);

        await Task.Delay(50);
        cache.InvalidateAll();
        release.SetResult();
        await loading;

        await cache.GetOrCreateAsync(
            ledgerId,
            _ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult(Empty());
            },
            CancellationToken.None);

        Assert.Equal(2, calls);
    }

    private static LedgerReplayData Empty()
    {
        return new LedgerReplayData(
            null!,
            Array.Empty<LedgerEntry>(),
            Array.Empty<CashAdjustment>(),
            Array.Empty<AssetTransaction>(),
            Array.Empty<PriceObservation>(),
            Array.Empty<FundPosition>(),
            Array.Empty<StockPosition>(),
            null,
            null);
    }
}

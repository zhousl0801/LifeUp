using System.Collections.Concurrent;
using LifeUp.Application.History;

namespace LifeUp.Infrastructure.Persistence;

/// <summary>
/// 只读回放数据的短缓存。仅用于 GET 查询；账本写入和备份恢复会立即失效。
/// 同一账本的并发未命中会合并为一次加载，避免首页多个请求重复读取全部表。
/// 缓存为进程内缓存；多实例部署需要改为分布式失效或缩短有效期。
/// </summary>
public sealed class ReplayDataCache
{
    // 略长于前端 15 秒读缓存，前端重新请求时通常还能命中；写入会立即失效。
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(20);

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();
    private readonly object _sync = new();
    private long _version;

    public async Task<LedgerReplayData> GetOrCreateAsync(
        Guid ledgerId,
        Func<CancellationToken, Task<LedgerReplayData>> load,
        CancellationToken cancellationToken)
    {
        var version = Volatile.Read(ref _version);

        if (TryRead(ledgerId, out var cached))
        {
            return cached;
        }

        var gate = _gates.GetOrAdd(ledgerId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            if (TryRead(ledgerId, out cached))
            {
                return cached;
            }

            var data = await load(cancellationToken);

            // 加载期间发生过写入时不写入缓存，避免把旧快照留给后续请求。
            lock (_sync)
            {
                if (version == _version)
                {
                    _entries[ledgerId] = new Entry(DateTimeOffset.UtcNow.Add(Lifetime), data);
                }
            }

            return data;
        }
        finally
        {
            gate.Release();
        }
    }

    public void InvalidateAll()
    {
        lock (_sync)
        {
            _version++;
            _entries.Clear();
        }
    }

    private bool TryRead(Guid ledgerId, out LedgerReplayData data)
    {
        if (_entries.TryGetValue(ledgerId, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            data = entry.Data;
            return true;
        }

        data = null!;
        return false;
    }

    private sealed record Entry(DateTimeOffset ExpiresAt, LedgerReplayData Data);
}

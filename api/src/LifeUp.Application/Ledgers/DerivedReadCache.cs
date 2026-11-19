using System.Collections.Concurrent;

namespace LifeUp.Application.Ledgers;

/// <summary>
/// 进程内有界派生读缓存（用于趋势图、基金收益等基于重放结果的汇总查询）。
/// 缓存键携带仓储读缓存版本号，账本写入或备份恢复后会自动失效。
/// </summary>
public sealed class DerivedReadCache
{
    private readonly int _maxEntries;
    private readonly TimeSpan _lifetime;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);

    public DerivedReadCache(
        TimeProvider? timeProvider = null,
        TimeSpan? lifetime = null,
        int maxEntries = 128)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lifetime = lifetime ?? TimeSpan.FromSeconds(20);
        _maxEntries = Math.Max(8, maxEntries);
    }

    public bool TryGet<T>(string key, out T value)
        where T : class
    {
        var now = _timeProvider.GetUtcNow();

        if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > now && entry.Value is T typed)
        {
            value = typed;
            return true;
        }

        if (entry is not null && entry.ExpiresAt <= now)
        {
            _entries.TryRemove(key, out _);
        }

        value = null!;
        return false;
    }

    public void Set<T>(string key, T value)
        where T : class
    {
        var now = _timeProvider.GetUtcNow();

        if (_entries.Count >= _maxEntries)
        {
            foreach (var pair in _entries)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _entries.TryRemove(pair.Key, out _);
                }
            }

            // 过期清理后仍达上限时，按最早过期时间淘汰一部分，防止内存无界增长。
            if (_entries.Count >= _maxEntries)
            {
                var overflow = _entries.Count - _maxEntries + Math.Max(1, _maxEntries / 4);

                foreach (var pair in _entries.OrderBy(item => item.Value.ExpiresAt).Take(overflow))
                {
                    _entries.TryRemove(pair.Key, out _);
                }
            }
        }

        _entries[key] = new CacheEntry(now.Add(_lifetime), value);
    }

    public void Clear()
    {
        _entries.Clear();
    }

    private sealed record CacheEntry(DateTimeOffset ExpiresAt, object Value);
}

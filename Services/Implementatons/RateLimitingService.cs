using Authentication.Fido2.Services.Interfaces;
using System.Collections.Concurrent;

namespace Authentication.Fido2.Services.Implementations;

/// <summary>
/// In-memory rate limiting service. For production, consider using Redis or a distributed cache.
/// </summary>
public class RateLimitingService : IRateLimitingService, IDisposable
{
    // Longest window in use across the codebase (900s = 15min)
    private const int MaxWindowSeconds = 900;

    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets;
    private readonly ILogger<RateLimitingService> _logger;
    private readonly Timer _cleanupTimer;

    public RateLimitingService(ILogger<RateLimitingService> logger)
    {
        _logger = logger;
        _buckets = new ConcurrentDictionary<string, RateLimitBucket>();
        // Clean up expired buckets every 5 minutes to prevent unbounded memory growth
        _cleanupTimer = new Timer(_ => CleanupExpiredBuckets(), null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public bool IsAllowed(string key, int maxAttempts, int windowSeconds)
    {
        var now = DateTime.UtcNow;
        var bucket = _buckets.AddOrUpdate(
            key,
            new RateLimitBucket { FirstAttemptAt = now, Count = 1 },
            (_, existing) =>
            {
                var windowStart = now.AddSeconds(-windowSeconds);
                if (existing.FirstAttemptAt < windowStart)
                {
                    // Window expired, reset
                    return new RateLimitBucket { FirstAttemptAt = now, Count = 1 };
                }

                existing.Count++;
                return existing;
            }
        );

        var windowStart2 = now.AddSeconds(-windowSeconds);
        if (bucket.FirstAttemptAt < windowStart2)
        {
            bucket.Count = 1;
            bucket.FirstAttemptAt = now;
        }

        return bucket.Count <= maxAttempts;
    }

    public int GetAttempts(string key, int windowSeconds)
    {
        var now = DateTime.UtcNow;
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            return 0;
        }

        var windowStart = now.AddSeconds(-windowSeconds);
        if (bucket.FirstAttemptAt < windowStart)
        {
            return 0;
        }

        return bucket.Count;
    }

    public void Reset(string key)
    {
        _buckets.TryRemove(key, out _);
    }

    private void CleanupExpiredBuckets()
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-MaxWindowSeconds);
        var removed = 0;
        foreach (var key in _buckets.Keys)
        {
            if (_buckets.TryGetValue(key, out var b) && b.FirstAttemptAt < cutoff)
                if (_buckets.TryRemove(key, out _))
                    removed++;
        }

        if (removed > 0)
            _logger.LogDebug("Rate limiter: removed {Count} expired buckets. Active: {Active}.",
                removed, _buckets.Count);
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }

    private class RateLimitBucket
    {
        public DateTime FirstAttemptAt { get; set; }
        public int Count { get; set; }
    }
}

using Authentication.Fido2.Services.Interfaces;
using System.Collections.Concurrent;

namespace Authentication.Fido2.Services.Implementations;

/// <summary>
/// In-memory rate limiting service. For production, consider using Redis or a distributed cache.
/// </summary>
public class RateLimitingService : IRateLimitingService
{
    private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets;
    private readonly ILogger<RateLimitingService> _logger;

    public RateLimitingService(ILogger<RateLimitingService> logger)
    {
        _logger = logger;
        _buckets = new ConcurrentDictionary<string, RateLimitBucket>();
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

    private class RateLimitBucket
    {
        public DateTime FirstAttemptAt { get; set; }
        public int Count { get; set; }
    }
}

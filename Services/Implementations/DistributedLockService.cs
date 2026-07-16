using System.Collections.Concurrent;
using Authentication.Fido2.Common;
using Authentication.Fido2.Services.Interfaces;


namespace Authentication.Fido2.Services.Implementations;

/// <summary>
/// In-memory distributed lock service for preventing concurrent access to shared resources.
/// Suitable for single-instance deployments. For multi-instance setups, use Redis-based locking.
/// </summary>
public class DistributedLockService : IDistributedLockService
{
    private class LockEntry
    {
        public string LockId { get; set; } = Guid.NewGuid().ToString();
        public DateTime ExpiresAtUtc { get; set; }
        public int RetryCount { get; set; }
    }

    private readonly ConcurrentDictionary<string, LockEntry> _locks =
        new();

    public async Task<string?> AcquireLockAsync(
        string key,
        int timeoutSeconds = 5,
        CancellationToken cancellationToken = default
    )
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var lockId = Guid.NewGuid().ToString();

        while (DateTime.UtcNow < deadline)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var newEntry = new LockEntry
            {
                LockId = lockId,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(30), // Lock expires after 30 seconds if not released
            };

            // Try to add the lock. TryAdd returns true if key didn't exist.
            if (_locks.TryAdd(key, newEntry))
            {
                return lockId;
            }

            // Clean up expired locks
            if (_locks.TryGetValue(key, out var existing) &&
                existing.ExpiresAtUtc < DateTime.UtcNow)
            {
                _locks.TryRemove(key, out _);
                continue;
            }

            // Wait a short time before retrying
            await Task.Delay(10, cancellationToken);
        }

        return null; // Timeout - could not acquire lock
    }

    public async Task ReleaseLockAsync(
        string key,
        string lockId,
        CancellationToken cancellationToken = default
    )
    {
        await Task.Run(() =>
        {
            if (_locks.TryGetValue(key, out var entry) &&
                entry.LockId == lockId)
            {
                _locks.TryRemove(key, out _);
            }
        }, cancellationToken);
    }

    public async Task<Result<T>> ExecuteLockedAsync<T>(
        string key,
        Func<CancellationToken, Task<Result<T>>> operation,
        int timeoutSeconds = 5,
        CancellationToken cancellationToken = default
    )
    {
        var lockId = await AcquireLockAsync(key, timeoutSeconds, cancellationToken);

        if (lockId is null)
        {
            return Result<T>.Failure(
                "Could not acquire lock. Resource is currently in use. Please retry.",
                StatusCodes.Status503ServiceUnavailable
            );
        }

        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            await ReleaseLockAsync(key, lockId, cancellationToken);
        }
    }
}

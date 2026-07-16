using Authentication.Fido2.Common;

namespace Authentication.Fido2.Services.Interfaces;

/// <summary>
/// Provides distributed locking mechanism to prevent concurrent access to shared resources.
/// Useful for preventing duplicate token issuance during concurrent MFA verification requests.
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// Acquires an exclusive lock on a resource key with timeout.
    /// </summary>
    /// <param name="key">Unique key identifying the resource (e.g., mfa_verify_{challengeId})</param>
    /// <param name="timeoutSeconds">Maximum time to wait for lock acquisition (default 5 seconds)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Lock ID if acquired, null if timeout or error</returns>
    Task<string?> AcquireLockAsync(
        string key,
        int timeoutSeconds = 5,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Releases an acquired lock.
    /// </summary>
    /// <param name="key">Resource key</param>
    /// <param name="lockId">Lock ID returned from AcquireLockAsync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReleaseLockAsync(string key, string lockId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an async operation while holding a lock.
    /// Automatically acquires and releases the lock.
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="key">Resource key</param>
    /// <param name="operation">Async operation to execute under lock</param>
    /// <param name="timeoutSeconds">Maximum time to wait for lock acquisition</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the operation, or failure if lock could not be acquired</returns>
    Task<Result<T>> ExecuteLockedAsync<T>(
        string key,
        Func<CancellationToken, Task<Result<T>>> operation,
        int timeoutSeconds = 5,
        CancellationToken cancellationToken = default
    );
}

namespace Authentication.Fido2.Services.Interfaces;

public interface IRateLimitingService
{
    /// <summary>
    /// Checks if an action is allowed based on rate limiting.
    /// </summary>
    /// <param name="key">Unique key for rate limiting (e.g., "login_10.0.0.1", "mfa_verify_user_123")</param>
    /// <param name="maxAttempts">Maximum allowed attempts</param>
    /// <param name="windowSeconds">Time window in seconds</param>
    /// <returns>true if allowed, false if rate limit exceeded</returns>
    bool IsAllowed(string key, int maxAttempts, int windowSeconds);

    /// <summary>
    /// Gets the number of attempts in the current window.
    /// </summary>
    int GetAttempts(string key, int windowSeconds);

    /// <summary>
    /// Resets the rate limit counter for a key.
    /// </summary>
    void Reset(string key);
}

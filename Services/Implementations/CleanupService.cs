using Authentication.Fido2.Data.Repositories.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Authentication.Fido2.Services.Implementations;

/// <summary>
/// Background service for cleaning up expired and revoked security artifacts.
/// Runs periodically to maintain database hygiene and performance.
/// </summary>
public class CleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1); // Run every hour

    public CleanupService(IServiceProvider serviceProvider, ILogger<CleanupService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cleanup service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                using (var scope = _serviceProvider.CreateScope())
                {
                    await RunCleanupAsync(scope.ServiceProvider, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when application is shutting down
                _logger.LogInformation("Cleanup service cancellation requested.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during cleanup service execution.");
                // Continue with next cycle even if one fails
            }
        }

        _logger.LogInformation("Cleanup service stopped.");
    }

    private async Task RunCleanupAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var cleanupStats = new CleanupStatistics();

        try
        {
            var mfaChallengeRepository = serviceProvider.GetRequiredService<IMfaChallengeRepository>();
            var accessTokenSessionRepository = serviceProvider.GetRequiredService<IAccessTokenSessionRepository>();
            var refreshTokenSessionRepository = serviceProvider.GetRequiredService<IRefreshTokenSessionRepository>();

            // Clean up expired MFA challenges (older than 5 minutes)
            cleanupStats.ExpiredMfaChallenges = await mfaChallengeRepository.DeleteExpiredChallengesAsync(
                DateTime.UtcNow.AddMinutes(-5),
                cancellationToken
            );

            // Clean up locked MFA challenges (older than 1 day)
            cleanupStats.LockedMfaChallenges = await mfaChallengeRepository.DeleteLockedChallengesAsync(
                DateTime.UtcNow.AddDays(-1),
                cancellationToken
            );

            // Clean up completed/consumed MFA challenges (older than 1 hour)
            cleanupStats.CompletedMfaChallenges = await mfaChallengeRepository.DeleteCompletedChallengesAsync(
                DateTime.UtcNow.AddHours(-1),
                cancellationToken
            );

            // Clean up revoked access token sessions (older than 30 days)
            cleanupStats.RevokedAccessTokenSessions = await accessTokenSessionRepository.DeleteRevokedSessionsAsync(
                DateTime.UtcNow.AddDays(-30),
                cancellationToken
            );

            // Clean up revoked refresh token sessions (older than 30 days)
            cleanupStats.RevokedRefreshTokenSessions = await refreshTokenSessionRepository.DeleteRevokedSessionsAsync(
                DateTime.UtcNow.AddDays(-30),
                cancellationToken
            );

            // Clean up expired refresh token sessions
            cleanupStats.ExpiredRefreshTokenSessions = await refreshTokenSessionRepository.DeleteExpiredSessionsAsync(
                DateTime.UtcNow,
                cancellationToken
            );

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "Cleanup completed in {DurationMs}ms. " +
                "Deleted: {ExpiredChallenges} expired challenges, " +
                "{LockedChallenges} locked challenges, " +
                "{CompletedChallenges} completed challenges, " +
                "{RevokedAccessTokens} revoked access tokens, " +
                "{RevokedRefreshTokens} revoked refresh tokens, " +
                "{ExpiredRefreshTokens} expired refresh tokens.",
                duration.TotalMilliseconds,
                cleanupStats.ExpiredMfaChallenges,
                cleanupStats.LockedMfaChallenges,
                cleanupStats.CompletedMfaChallenges,
                cleanupStats.RevokedAccessTokenSessions,
                cleanupStats.RevokedRefreshTokenSessions,
                cleanupStats.ExpiredRefreshTokenSessions
            );
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Cleanup service encountered an error after {DurationMs}ms.", duration.TotalMilliseconds);
            throw;
        }
    }

    private class CleanupStatistics
    {
        public int ExpiredMfaChallenges { get; set; }
        public int LockedMfaChallenges { get; set; }
        public int CompletedMfaChallenges { get; set; }
        public int RevokedAccessTokenSessions { get; set; }
        public int RevokedRefreshTokenSessions { get; set; }
        public int ExpiredRefreshTokenSessions { get; set; }
    }
}

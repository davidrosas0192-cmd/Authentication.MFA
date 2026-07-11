using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IUserRecoveryCodeRepository
{
    Task<(UserRecoveryCodeBatch? Batch, int RemainingCount)> GetStatusAsync(
        long userId,
        CancellationToken cancellationToken
    );

    Task<bool> HasUnusedCodesAsync(long userId, CancellationToken cancellationToken);

    Task<UserRecoveryCodeBatch> ReplaceBatchAsync(
        long userId,
        IReadOnlyCollection<string> codeHashes,
        CancellationToken cancellationToken
    );

    Task<bool> TryConsumeCodeAsync(long userId, string code, CancellationToken cancellationToken);
}

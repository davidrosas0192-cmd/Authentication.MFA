using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Repositories.Interfaces;

public interface IUserRecoveryCodeRepository
{
    Task<(UserRecoveryCodeBatch? Batch, int RemainingCount)> GetStatusAsync(
        Guid userId,
        CancellationToken cancellationToken
    );

    Task<bool> HasUnusedCodesAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserRecoveryCodeBatch> ReplaceBatchAsync(
        Guid userId,
        IReadOnlyCollection<string> codeHashes,
        CancellationToken cancellationToken
    );

    Task<bool> TryConsumeCodeAsync(Guid userId, string code, CancellationToken cancellationToken);
}

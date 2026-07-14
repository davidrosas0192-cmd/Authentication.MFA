namespace Authentication.Fido2.DTOs.Mfa;

public class StartMfaChallengeResponse
{
    public Guid MfaTransactionId { get; set; }
    public string? ContinuationToken { get; set; }
    public string Method { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
}

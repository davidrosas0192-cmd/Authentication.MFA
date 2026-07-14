namespace Authentication.Fido2.DTOs.Mfa;

public class StartMfaManagementChallengeRequest
{
    public Guid MfaTransactionId { get; set; }
    public string ContinuationToken { get; set; } = default!;
    public string Method { get; set; } = default!;
}

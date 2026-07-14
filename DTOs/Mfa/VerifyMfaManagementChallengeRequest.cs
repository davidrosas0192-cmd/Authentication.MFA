namespace Authentication.Fido2.DTOs.Mfa;

public class VerifyMfaManagementChallengeRequest
{
    public Guid MfaTransactionId { get; set; }
    public string ContinuationToken { get; set; } = default!;
    public string Code { get; set; } = default!;
}

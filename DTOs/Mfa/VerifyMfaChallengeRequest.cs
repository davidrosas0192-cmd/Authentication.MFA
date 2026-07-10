namespace Authentication.Fido2.DTOs.Mfa;

public class VerifyMfaChallengeRequest
{
    public Guid MfaTransactionId { get; set; }
    public string Code { get; set; } = default!;
}

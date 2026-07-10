namespace Authentication.Fido2.DTOs.Mfa;

public class StartMfaChallengeRequest
{
    public Guid MfaTransactionId { get; set; }
    public string Method { get; set; } = default!;
}

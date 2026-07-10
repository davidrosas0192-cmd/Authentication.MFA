namespace Authentication.Fido2.DTOs.Mfa;

public class VerifyMfaChallengeRequest
{
    public string Code { get; set; } = default!;
}

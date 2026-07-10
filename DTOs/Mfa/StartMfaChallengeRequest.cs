namespace Authentication.Fido2.DTOs.Mfa;

public class StartMfaChallengeRequest
{
    public string Method { get; set; } = default!;
}

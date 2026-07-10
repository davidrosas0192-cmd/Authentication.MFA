namespace Authentication.Fido2.DTOs.Mfa;

public class StartMfaEnrollmentRequest
{
    public string Method { get; set; } = default!;
    public string ContactValue { get; set; } = default!;
}

namespace Authentication.Fido2.DTOs.Mfa;

public class VerifyMfaEnrollmentResponse
{
    public string Method { get; set; } = default!;
    public bool IsVerified { get; set; }
    public List<string> RecoveryCodes { get; set; } = [];
}

namespace Authentication.Fido2.DTOs.Mfa;

public class VerifyLoginEnrollmentResponse
{
    public Guid EnrollmentSessionId { get; set; }
    public string SessionStatus { get; set; } = default!;
    public string SessionContinuationToken { get; set; } = default!;
    public string Method { get; set; } = default!;
    public bool IsVerified { get; set; }
    public List<string> RecoveryCodes { get; set; } = [];
    public List<string> RemainingSetupOptions { get; set; } = [];
}
namespace Authentication.Fido2.DTOs.Auth;

public class LoginResponse
{
    public string Status { get; set; } = default!;
    public string? AccessToken { get; set; }
    public string? MfaToken { get; set; }
    public string? EnrollmentToken { get; set; }
    public string? RefreshToken { get; set; }
    public int? ExpiresIn { get; set; }
    public int? MfaExpiresIn { get; set; }
    public int? EnrollmentExpiresIn { get; set; }
    public bool MfaRequired { get; set; }
    public Guid? EnrollmentSessionId { get; set; }
    public string? EnrollmentContinuationToken { get; set; }
    public List<string> AllowedMfaMethods { get; set; } = [];
    public List<string> AvailableMfaSetupOptions { get; set; } = [];
}

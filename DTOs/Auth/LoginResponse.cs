namespace Authentication.Fido2.DTOs.Auth;

public class LoginResponse
{
    public string Status { get; set; } = default!;
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public int? ExpiresIn { get; set; }
    public bool RequiresFido2 { get; set; }
    public bool MfaRequired { get; set; }
    public Guid? MfaTransactionId { get; set; }
    public List<string> AllowedMfaMethods { get; set; } = [];
}

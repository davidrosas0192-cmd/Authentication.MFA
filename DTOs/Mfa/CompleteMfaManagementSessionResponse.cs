namespace Authentication.Fido2.DTOs.Mfa;

public class CompleteMfaManagementSessionResponse
{
    public string Status { get; set; } = default!;
    public DateTime CompletedAtUtc { get; set; }
}

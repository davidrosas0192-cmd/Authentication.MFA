namespace Authentication.Fido2.DTOs.Mfa;

public class CancelMfaManagementSessionResponse
{
    public string Status { get; set; } = default!;
    public DateTime CancelledAtUtc { get; set; }
}

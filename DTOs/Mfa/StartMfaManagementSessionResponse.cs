namespace Authentication.Fido2.DTOs.Mfa;

public class StartMfaManagementSessionResponse
{
    public string Status { get; set; } = default!;
    public Guid MfaTransactionId { get; set; }
    public string ContinuationToken { get; set; } = default!;
    public List<string> AvailableMethods { get; set; } = [];
    public DateTime ExpiresAtUtc { get; set; }
}

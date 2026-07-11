namespace Authentication.Fido2.DTOs.Mfa;

public class StartMfaReconfigureResponse
{
    public Guid ReconfigureTransactionId { get; set; }
    public string Method { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
}

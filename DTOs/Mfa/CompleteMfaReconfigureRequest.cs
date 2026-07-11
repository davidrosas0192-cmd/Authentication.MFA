namespace Authentication.Fido2.DTOs.Mfa;

public class CompleteMfaReconfigureRequest
{
    public Guid ReconfigureTransactionId { get; set; }
    public string Code { get; set; } = default!;
}

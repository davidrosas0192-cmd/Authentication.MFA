namespace Authentication.Fido2.DTOs.Mfa;

public class CompleteMfaManagementSessionRequest
{
    public Guid MfaTransactionId { get; set; }
    public string ContinuationToken { get; set; } = default!;
}

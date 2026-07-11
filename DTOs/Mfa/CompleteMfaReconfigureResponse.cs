namespace Authentication.Fido2.DTOs.Mfa;

public class CompleteMfaReconfigureResponse
{
    public string Method { get; set; } = default!;
    public bool IsReconfigured { get; set; }
}

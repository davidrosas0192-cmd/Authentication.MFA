namespace Authentication.Fido2.DTOs.Mfa;

public class CreateRecoveryCodesResponse
{
    public List<string> RecoveryCodes { get; set; } = [];
    public int Count { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

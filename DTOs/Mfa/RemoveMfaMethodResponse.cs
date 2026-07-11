namespace Authentication.Fido2.DTOs.Mfa;

public class RemoveMfaMethodResponse
{
    public string Method { get; set; } = default!;
    public bool IsEnabled { get; set; }
}

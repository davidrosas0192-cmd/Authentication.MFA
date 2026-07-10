namespace Authentication.Fido2.DTOs.Mfa;

public class MfaDevicesAvailableResponse
{
    public List<string> AllowedMfaMethods { get; set; } = [];
    public List<string> AvailableMfaSetupOptions { get; set; } = [];
}

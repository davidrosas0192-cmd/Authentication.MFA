namespace Authentication.Fido2.Options;

public class TwilioOptions
{
    public string AccountSid { get; set; } = default!;
    public string AuthToken { get; set; } = default!;
    public string VerifyServiceSid { get; set; } = default!;
}

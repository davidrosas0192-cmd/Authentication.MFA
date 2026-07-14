namespace Authentication.Fido2.Options;

public sealed class LoginEnrollmentJwtOptions
{
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string SecretKey { get; set; } = default!;
    public int ExpirationMinutes { get; set; }
}
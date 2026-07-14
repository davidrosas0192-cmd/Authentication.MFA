namespace Authentication.Fido2.DTOs.Mfa;

public class StartLoginEnrollmentRequest
{
    public string ContinuationToken { get; set; } = default!;
    public string Method { get; set; } = default!;
    public string ContactValue { get; set; } = default!;
}
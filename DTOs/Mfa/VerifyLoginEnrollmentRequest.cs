namespace Authentication.Fido2.DTOs.Mfa;

public class VerifyLoginEnrollmentRequest
{
    public Guid EnrollmentTransactionId { get; set; }
    public string ContinuationToken { get; set; } = default!;
    public string Code { get; set; } = default!;
}
namespace Authentication.Fido2.DTOs.Mfa;

public class VerifyMfaEnrollmentRequest
{
    public Guid EnrollmentTransactionId { get; set; }
    public string Code { get; set; } = default!;
}

namespace Authentication.Fido2.DTOs.Mfa;

public class StartMfaEnrollmentResponse
{
    public Guid EnrollmentTransactionId { get; set; }
    public string ContinuationToken { get; set; } = default!;
    public string Method { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
}

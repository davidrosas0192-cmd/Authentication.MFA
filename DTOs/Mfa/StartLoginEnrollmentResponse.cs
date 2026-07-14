namespace Authentication.Fido2.DTOs.Mfa;

public class StartLoginEnrollmentResponse
{
    public Guid EnrollmentSessionId { get; set; }
    public Guid EnrollmentTransactionId { get; set; }
    public string SessionContinuationToken { get; set; } = default!;
    public string ChallengeContinuationToken { get; set; } = default!;
    public string Method { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
}
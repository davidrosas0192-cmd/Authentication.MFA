namespace Authentication.Fido2.DTOs.Mfa;

public class CompleteLoginEnrollmentSessionRequest
{
    public Guid EnrollmentSessionId { get; set; }
    public string ContinuationToken { get; set; } = default!;
}
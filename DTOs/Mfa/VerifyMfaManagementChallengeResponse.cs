namespace Authentication.Fido2.DTOs.Mfa;

public class VerifyMfaManagementChallengeResponse
{
    public string Status { get; set; } = default!;
    public string ContinuationToken { get; set; } = default!;
    public DateTime VerifiedAtUtc { get; set; }
    public DateTime StepUpValidUntilUtc { get; set; }
}

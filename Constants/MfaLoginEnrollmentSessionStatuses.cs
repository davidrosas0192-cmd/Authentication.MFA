namespace Authentication.Fido2.Constants;

public static class MfaLoginEnrollmentSessionStatuses
{
    public const string EnrollmentRequired = "enrollment_required";
    public const string EnrollmentInProgress = "enrollment_in_progress";
    public const string ReadyToComplete = "ready_to_complete";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}
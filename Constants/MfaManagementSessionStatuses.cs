namespace Authentication.Fido2.Constants;

public static class MfaManagementSessionStatuses
{
    public const string StepUpRequired = "step_up_required";
    public const string StepUpCompleted = "step_up_completed";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

namespace Authentication.Fido2.Constants;

public static class MfaChallengeStatuses
{
    public const string PendingSelection = "pending_selection";
    public const string Pending = "pending";
    public const string Verified = "verified";
    public const string Consumed = "consumed";
    public const string Revoked = "revoked";
    public const string Expired = "expired";
    public const string Failed = "failed";
    public const string Locked = "locked";
}

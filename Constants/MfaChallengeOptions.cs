namespace Authentication.Fido2.Constants;

public static class MfaChallengeOptions
{
    public const int MaxFailedAttempts = 5;
    public const int ChallengeExpirationMinutes = 5;
    public const int MaxConcurrentChallenges = 3;
}

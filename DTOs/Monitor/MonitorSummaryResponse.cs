namespace Authentication.Fido2.DTOs.Monitor;

public class MonitorSummaryResponse
{
    public int LoginsToday { get; set; }
    public int LoginFailuresToday { get; set; }
    public int ActiveAccessSessions { get; set; }
    public int ActiveRefreshSessions { get; set; }
    public int PendingChallenges { get; set; }
    public int LockedChallenges { get; set; }
    public int EnrollmentsInProgress { get; set; }
    public int EnrollmentsCompleted { get; set; }
    public int SecurityWarningsToday { get; set; }
    public int SecurityErrorsToday { get; set; }
    public int UsersTotal { get; set; }
    public int UsersActive { get; set; }
    public int UsersWithMfa { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
}

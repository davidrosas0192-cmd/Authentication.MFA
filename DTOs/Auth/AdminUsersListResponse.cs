namespace Authentication.Fido2.DTOs.Auth;

public class AdminUsersListResponse
{
    public List<AdminUserSummaryResponse> Users { get; set; } = [];
}

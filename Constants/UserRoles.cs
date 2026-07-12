namespace Authentication.Fido2.Constants;

public static class UserRoles
{
    public const string Admin = "admin";
    public const string User = "user";
    public const string Support = "support";

    public static bool IsValid(string value)
    {
        return string.Equals(value, Admin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, User, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, Support, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    public static string ToDisplayName(string value)
    {
        var normalized = Normalize(value);
        return normalized switch
        {
            Admin => "Admin",
            Support => "Support",
            _ => "User",
        };
    }
}
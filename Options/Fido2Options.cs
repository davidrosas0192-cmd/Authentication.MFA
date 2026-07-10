namespace Authentication.Fido2.Options;

public class Fido2Options
{
    public string ServerDomain { get; set; } = default!;

    public string ServerName { get; set; } = default!;

    public HashSet<string> Origins { get; set; } = default!;
}

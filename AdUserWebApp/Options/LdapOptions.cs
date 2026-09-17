namespace AdUserWebApp.Options;

public sealed class LdapOptions
{
    public const string SectionName = "Ldap";

    public string Domain { get; set; } = "";
    public string? Container { get; set; }
    public string? Server { get; set; }
    public string? ServiceAccountUserName { get; set; }
    public string? ServiceAccountPassword { get; set; }
}

namespace AdUserWebApp.Options;

public sealed class LdapOptions
{
    public const string SectionName = "Ldap";

    public string Domain { get; set; } = "";
    public string? Container { get; set; }
    public string? Server { get; set; }
    public string? ServiceAccountUserName { get; set; }
    public string? ServiceAccountPassword { get; set; }

    /// <summary>
    /// How long a full directory export is reused before the next request triggers a fresh
    /// one. Paging, searching, and the "include disabled" toggle are served from this cache
    /// instead of re-querying AD every time. 0 disables caching.
    /// </summary>
    public int CacheMinutes { get; set; } = 10;
}

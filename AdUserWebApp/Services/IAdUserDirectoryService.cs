using AdUserWebApp.Models;

namespace AdUserWebApp.Services;

public interface IAdUserDirectoryService
{
    /// <summary>
    /// Binds to the configured domain and reports which domain controller answered,
    /// without reading any user data. Mirrors the connectivity probe used to validate
    /// AD reachability before running a bulk export.
    /// </summary>
    AdConnectivityStatus Probe();

    /// <summary>
    /// Returns every person/user object in the configured domain or container. Results are
    /// cached in memory (see <see cref="Options.LdapOptions.CacheMinutes"/>) so that paging,
    /// searching, or repeat page loads don't each re-walk the whole directory.
    /// </summary>
    AdUserSnapshot GetUsers(bool includeDisabled = false, bool forceRefresh = false);
}

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
    /// Enumerates every person/user object in the configured domain or container.
    /// </summary>
    IEnumerable<AdUserRecord> GetAllUsers(bool includeDisabled = false);
}

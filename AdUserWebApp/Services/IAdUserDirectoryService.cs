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

    /// <summary>
    /// Updates the profile attributes of an existing user. Never touches account state or
    /// credentials. Throws <see cref="KeyNotFoundException"/> if no such account exists.
    /// </summary>
    AdUserRecord UpdateUser(string samAccountName, AdUserUpdateRequest request);

    /// <summary>
    /// Enables or disables an existing account (the userAccountControl ADS_UF_ACCOUNTDISABLE
    /// bit). Throws <see cref="KeyNotFoundException"/> if no such account exists.
    /// </summary>
    AdUserRecord SetAccountEnabled(string samAccountName, bool enabled);

    /// <summary>
    /// Resets an existing user's password. Throws <see cref="KeyNotFoundException"/> if no
    /// such account exists. Never logs the password.
    /// </summary>
    void ResetPassword(string samAccountName, AdPasswordResetRequest request);

    /// <summary>
    /// Creates a new user account. Throws <see cref="InvalidOperationException"/> if the
    /// sAMAccountName is already taken.
    /// </summary>
    AdUserRecord CreateUser(AdUserCreateRequest request);
}

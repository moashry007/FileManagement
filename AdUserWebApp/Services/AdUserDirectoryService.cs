using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Net;
using System.Runtime.Versioning;
using AdUserWebApp.Models;
using AdUserWebApp.Options;
using Microsoft.Extensions.Options;

namespace AdUserWebApp.Services;

/// <summary>
/// Reads Active Directory user accounts via <see cref="DirectorySearcher"/> rather than
/// PrincipalSearcher: it gives control over paging and which attributes are loaded, which
/// matters at whole-domain scale (see ad-full-user-export-spec.md).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class AdUserDirectoryService : IAdUserDirectoryService
{
    private static readonly string[] PropertiesToLoad =
    {
        "sAMAccountName", "displayName", "givenName", "sn", "mail",
        "telephoneNumber", "mobile", "employeeID", "title", "department",
        "userAccountControl", "distinguishedName", "whenCreated", "whenChanged",
    };

    private const int AdsUfAccountDisable = 0x2;
    private static readonly TimeSpan LdapTimeout = TimeSpan.FromMinutes(2);

    private readonly LdapOptions _options;
    private readonly ILogger<AdUserDirectoryService> _logger;

    private readonly object _cacheLock = new();
    private AdUserSnapshot? _activeOnlyCache;
    private AdUserSnapshot? _includingDisabledCache;
    private DateTimeOffset _activeOnlyCacheExpiresAt;
    private DateTimeOffset _includingDisabledCacheExpiresAt;

    public AdUserDirectoryService(IOptions<LdapOptions> options, ILogger<AdUserDirectoryService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public AdUserSnapshot GetUsers(bool includeDisabled = false, bool forceRefresh = false)
    {
        var cacheDuration = TimeSpan.FromMinutes(Math.Max(_options.CacheMinutes, 0));

        lock (_cacheLock)
        {
            var cached = includeDisabled ? _includingDisabledCache : _activeOnlyCache;
            var expiresAt = includeDisabled ? _includingDisabledCacheExpiresAt : _activeOnlyCacheExpiresAt;

            if (!forceRefresh && cacheDuration > TimeSpan.Zero && cached is not null && DateTimeOffset.UtcNow < expiresAt)
            {
                _logger.LogInformation("Serving {Count} users from cache (as of {AsOf})", cached.Users.Count, cached.AsOf);
                return cached;
            }
        }

        var snapshot = new AdUserSnapshot
        {
            Users = FetchAllUsersFromDirectory(includeDisabled).ToList(),
            AsOf = DateTimeOffset.UtcNow,
        };

        lock (_cacheLock)
        {
            if (includeDisabled)
            {
                _includingDisabledCache = snapshot;
                _includingDisabledCacheExpiresAt = snapshot.AsOf + cacheDuration;
            }
            else
            {
                _activeOnlyCache = snapshot;
                _activeOnlyCacheExpiresAt = snapshot.AsOf + cacheDuration;
            }
        }

        return snapshot;
    }

    public AdConnectivityStatus Probe()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var context = CreatePrincipalContext();
            var connectedServer = context.ConnectedServer;
            stopwatch.Stop();

            _logger.LogInformation("AD probe succeeded in {ElapsedMs}ms against {Server}",
                stopwatch.ElapsedMilliseconds, connectedServer);

            return new AdConnectivityStatus
            {
                Connected = true,
                ConnectedServer = connectedServer,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "AD probe failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            return new AdConnectivityStatus
            {
                Connected = false,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Error = ex.Message,
            };
        }
    }

    private IEnumerable<AdUserRecord> FetchAllUsersFromDirectory(bool includeDisabled)
    {
        if (string.IsNullOrWhiteSpace(_options.Domain))
            throw new InvalidOperationException("Ldap:Domain is not configured.");

        var path = BuildLdapPath();
        var credential = BuildCredential();

        using var root = credential is null
            ? new DirectoryEntry(path)
            : new DirectoryEntry(path, credential.UserName, credential.Password);

        using var searcher = new DirectorySearcher(root)
        {
            // Real users only: excludes computer accounts, service/contact objects,
            // and the built-in disabled template accounts.
            Filter = "(&(objectCategory=person)(objectClass=user))",
            PageSize = 1000, // Required for >1000 results: turns on LDAP paged-results control.
            SizeLimit = 0,
            SearchScope = SearchScope.Subtree,
            ClientTimeout = LdapTimeout,
            ServerTimeLimit = LdapTimeout,
        };

        foreach (var property in PropertiesToLoad)
            searcher.PropertiesToLoad.Add(property);

        var stopwatch = Stopwatch.StartNew();
        var scanned = 0;
        var returned = 0;

        using var results = searcher.FindAll();
        foreach (SearchResult result in results)
        {
            scanned++;
            var record = MapToRecord(result);

            if (scanned % 1000 == 0)
                _logger.LogInformation("AD export in progress: {Scanned} objects scanned ({Elapsed})", scanned, stopwatch.Elapsed);

            if (!includeDisabled && record.IsDisabled)
                continue;

            returned++;
            yield return record;
        }

        _logger.LogInformation("AD export complete: {Scanned} objects scanned, {Returned} returned in {Elapsed}",
            scanned, returned, stopwatch.Elapsed);
    }

    private PrincipalContext CreatePrincipalContext()
    {
        var credential = BuildCredential();
        return credential is null
            ? new PrincipalContext(ContextType.Domain, _options.Domain)
            : new PrincipalContext(ContextType.Domain, _options.Domain, credential.UserName, credential.Password);
    }

    private string BuildLdapPath()
    {
        var domainDn = $"DC={string.Join(",DC=", _options.Domain.Split('.', StringSplitOptions.RemoveEmptyEntries))}";

        string target;
        if (string.IsNullOrWhiteSpace(_options.Container))
        {
            // No explicit container: bind to the whole domain. Prefer the bare domain name
            // (lets AD site/DC discovery pick a server) unless a specific DC was requested.
            target = string.IsNullOrWhiteSpace(_options.Server) ? _options.Domain : domainDn;
        }
        else if (_options.Container.Contains("DC=", StringComparison.OrdinalIgnoreCase))
        {
            // Container is already a full distinguished name.
            target = _options.Container;
        }
        else
        {
            target = $"{_options.Container},{domainDn}";
        }

        return string.IsNullOrWhiteSpace(_options.Server)
            ? $"LDAP://{target}"
            : $"LDAP://{_options.Server}/{target}";
    }

    private NetworkCredential? BuildCredential()
    {
        if (string.IsNullOrWhiteSpace(_options.ServiceAccountUserName) || string.IsNullOrWhiteSpace(_options.ServiceAccountPassword))
            return null;

        return new NetworkCredential(_options.ServiceAccountUserName, _options.ServiceAccountPassword);
    }

    private static AdUserRecord MapToRecord(SearchResult result)
    {
        string Get(string name) => result.Properties.Contains(name) ? result.Properties[name][0]?.ToString() ?? "" : "";
        var userAccountControl = result.Properties.Contains("userAccountControl") ? (int)result.Properties["userAccountControl"][0] : 0;

        return new AdUserRecord
        {
            SamAccountName = Get("sAMAccountName"),
            DisplayName = Get("displayName"),
            FirstName = Get("givenName"),
            Surname = Get("sn"),
            Email = Get("mail"),
            Telephone = Get("telephoneNumber"),
            Mobile = Get("mobile"),
            EmployeeId = Get("employeeID"),
            JobTitle = Get("title"),
            Department = Get("department"),
            DistinguishedName = Get("distinguishedName"),
            IsDisabled = (userAccountControl & AdsUfAccountDisable) != 0,
        };
    }
}

# AdUserWebApp

Standalone ASP.NET Core web app that connects to Active Directory and lists user
accounts in a browser UI. Built from `ad-full-user-export-spec.md`.

**Windows only** — `System.DirectoryServices` requires Windows, so this project
targets `net10.0-windows` and must run on a machine that is domain-joined to (or
has network line-of-sight to) the target AD domain.

## Configuration

Set `Ldap` in `appsettings.json` / `appsettings.Development.json` / environment
variables / user-secrets:

```json
{
  "Ldap": {
    "Domain": "ksuhs.edu.sa",
    "Container": null,
    "Server": null,
    "ServiceAccountUserName": null,
    "ServiceAccountPassword": null
  }
}
```

- `Domain` (required) — DNS domain name to bind to.
- `Container` (optional) — scope to an OU, e.g. `"OU=Users,DC=ksuhs,DC=edu,DC=sa"`.
- `Server` (optional) — explicit DC hostname; leave `null` to let AD site
  discovery pick the nearest DC.
- `ServiceAccountUserName` / `ServiceAccountPassword` — only set these if
  Windows Integrated Authentication isn't available in the deployment
  environment. Use a dedicated, least-privilege, **read-only** service
  account, and supply the password via a secret store
  (`dotnet user-secrets`, environment variable, Key Vault) — never commit it
  to `appsettings.json`.
- `CacheMinutes` (default `10`) — how long a full directory export is reused.
  Paging, searching, and toggling "include disabled" are served from this
  in-memory cache instead of re-querying AD on every request; hit **Refresh**
  in the UI to force a fresh read. Set to `0` to disable caching (not
  recommended against a large domain — see below).

## Running

```
dotnet run --project AdUserWebApp
```

Then open the served URL in a browser. The page:
1. Calls `GET /api/adusers/probe` to confirm it can bind to the domain and
   shows which domain controller answered.
2. Calls `GET /api/adusers` to list users, with search and pagination.

The API also exposes Scalar/OpenAPI docs at `/scalar`.

## Endpoints

- `GET /api/adusers/probe` — connectivity check only, reads no user data.
- `GET /api/adusers?q=&includeDisabled=&skip=&take=&forceRefresh=` — paged
  user list, served from the in-memory cache unless `forceRefresh=true`.
  Disabled accounts (`userAccountControl` bit `ADS_UF_ACCOUNTDISABLE`) are
  excluded unless `includeDisabled=true` is passed explicitly. The response
  includes `asOf` (when that snapshot was read from AD) so the UI can show
  how fresh the data is.

## Why the first load can be slow

A full, uncached export walks every person/user object in the domain — for a
large organization (thousands to tens of thousands of accounts) that single
LDAP query can legitimately take tens of seconds to a couple of minutes, even
though it completes in one continuous paged operation rather than one round
trip per user. The **first** request after startup (or after the cache
expires, or after hitting Refresh) pays that cost; every request after that,
including every page turn, search keystroke, and disabled-accounts toggle, is
served from the cache and returns near-instantly until it expires or you
force a refresh. If the page looks stuck on "Loading…" for longer than a
minute or two on a fresh start, check the server console for the
`AD export in progress: N objects scanned` log line (emitted every 1000
records) — if the count is climbing, it's working; if there's no such line at
all, the bind itself is hanging or failing (check `/api/adusers/probe` and
the server log for the actual exception).

## Data handling

This app reads PII (name, email, phone, employee ID, job title, department)
for every account in the domain. Per the spec:
- Only the attributes needed for the directory view are requested — no group
  memberships, manager chains, or home directories.
- The API never logs the result set, only counts and timings.
- Deploy this behind the same access controls as any other system holding
  organization-wide PII; don't expose it on an open/public network without
  authentication in front of it.

## Verification status

This sandbox has no Windows and no line-of-sight to a real AD domain, so the
actual LDAP bind and enumeration can't be exercised here. What *was* verified
in this sandbox (via a temporary net8.0 retarget and a stubbed directory
service, both reverted before committing): the app builds, the web server
starts, the static UI and API wiring work end-to-end, AD-unreachable errors
degrade cleanly (503/502, no crash, no PII in logs), and the in-memory
caching behaves correctly (repeat requests are served from cache; only
`forceRefresh=true` or cache expiry triggers a new read).

Still to confirm on a domain-joined Windows machine, per §1 of the spec:
```
dotnet restore
dotnet build
dotnet run --project AdUserWebApp
```
Confirm `GET /api/adusers/probe` reports a real `ConnectedServer` before
relying on the full user list.

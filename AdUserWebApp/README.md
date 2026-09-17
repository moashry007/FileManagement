# AdUserWebApp

Standalone ASP.NET Core web app that connects to Active Directory: lists users,
and can edit profile fields, enable/disable accounts, reset passwords, and
create new accounts, all from a browser UI. The read path is built from
`ad-full-user-export-spec.md`; the write endpoints are an extension beyond
that spec — see **Security warning** below before enabling them.

**Windows only** — `System.DirectoryServices` requires Windows, so this project
must run on a machine that is domain-joined to (or has network line-of-sight
to) the target AD domain. It targets plain `net10.0` (not `net10.0-windows`):
combining an ASP.NET Core Web SDK project with a `-windows`-suffixed TFM is
known to make Visual Studio's Web Tools throw `An element with the same key
but a different value already exists. Key:
'Microsoft.WebTools.ProjectSystem.WebServer.SelfHostWebServer'` when starting
the debugger. Windows-only APIs are instead scoped with
`[SupportedOSPlatform("windows")]` on `AdUserDirectoryService` (and a scoped
`#pragma warning disable CA1416` around its one DI registration in
`Program.cs`), which was the spec's explicitly-sanctioned alternative to the
`-windows` TFM.

## ⚠️ Security warning: this app currently has no authentication

**Anyone who can reach this app's URL can read every employee's PII and, if
you configure a write-capable service account, create accounts, disable
accounts, and reset passwords for anyone in the domain.** There is no login
screen, no authorization check, and no rate limiting on any endpoint.

This was a deliberate, explicit decision for this iteration, not an
oversight. Before deploying this anywhere reachable by more than a trusted
operator on a locked-down machine:
- Put a real authentication/authorization layer in front of it (Windows
  Integrated Auth restricted to an admin AD group, an API gateway, a reverse
  proxy requiring SSO, or equivalent).
- Consider whether write endpoints should exist at all in a given
  deployment — see `LdapOptions` / dependency injection in `Program.cs` to
  register a read-only implementation if you only need the export path.
- Add audit logging with an authenticated identity attached (today, writes
  are logged with *what* changed and *who the target was*, but not
  *who made the request*, since there's no authenticated caller to log).

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
  environment. Supply the password via a secret store (`dotnet user-secrets`,
  environment variable, Key Vault) — never commit it to `appsettings.json`.
  **If you only need the read/export path, use a read-only service account**
  (the original spec's recommendation). The write endpoints (profile edits,
  enable/disable, password reset, account creation) need an account with
  actual write and "Reset Password" extended rights on the target OU — that
  is strictly more privileged than the read-only export this app started as,
  so treat it as a separate decision, not a default.
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
- `PATCH /api/adusers/{samAccountName}` — update profile fields (name,
  contact info, title, department, employee ID). A field left out of the
  body (`null`) is unchanged; an empty string clears that AD attribute.
  Never touches account state or credentials. `404` if the account doesn't
  exist.
- `POST /api/adusers/{samAccountName}/enable` / `.../disable` — flips the
  `ADS_UF_ACCOUNTDISABLE` bit. The account is never deleted.
- `POST /api/adusers/{samAccountName}/reset-password` — body
  `{ "newPassword": "...", "requireChangeAtNextLogon": true }`. The password
  is never logged or echoed back. `400` if AD rejects it (e.g. complexity
  policy), `404` if the account doesn't exist.
- `POST /api/adusers` — create a new account. Body requires
  `samAccountName`, `firstName`, `surname`; everything else (contact fields,
  `initialPassword`, `requirePasswordChangeAtNextLogon`, `enabled`) is
  optional. AD refuses to create an **enabled** account with no usable
  password, so an account created without `initialPassword` comes back
  disabled regardless of the `enabled` flag. `409` if the username is
  already taken.

All write endpoints invalidate the read cache, so the next `GET
/api/adusers` reflects the change immediately without needing
`forceRefresh`.

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
actual LDAP bind, enumeration, and writes can't be exercised against real AD
here. What *was* verified in this sandbox (via a temporary net8.0 retarget
and a stubbed/fake directory service, both reverted before committing):
- The app builds, the web server starts, and AD-unreachable errors degrade
  cleanly (503/502, no crash, no PII in logs).
- In-memory caching behaves correctly (repeat requests are served from
  cache; only `forceRefresh=true` or cache expiry triggers a new read; any
  write invalidates the cache).
- All write endpoints, driven through a real headless-browser session
  (Playwright/Chromium) against the actual UI, not just curl: editing a
  user's fields, disabling/re-enabling an account (including the
  disabled-accounts filter and the Enable/Disable button label swapping
  correctly), resetting a password, creating a new account, and a duplicate
  username producing an inline dialog error without closing the dialog.
- No password value ever appeared in server logs across any of the above.

What was **not** and cannot be verified here: the real LDAP write semantics
against actual AD (permission errors, password complexity rejection,
`SetPassword`'s channel-security requirements, VLV/paging quirks on a real
large domain).

Still to confirm on a domain-joined Windows machine, per §1 of the spec:
```
dotnet restore
dotnet build
dotnet run --project AdUserWebApp
```
Confirm `GET /api/adusers/probe` reports a real `ConnectedServer` before
relying on the full user list.

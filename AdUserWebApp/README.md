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
- `GET /api/adusers?q=&includeDisabled=&skip=&take=` — paged user list.
  Disabled accounts (`userAccountControl` bit `ADS_UF_ACCOUNTDISABLE`) are
  excluded unless `includeDisabled=true` is passed explicitly.

## Data handling

This app reads PII (name, email, phone, employee ID, job title, department)
for every account in the domain. Per the spec:
- Only the attributes needed for the directory view are requested — no group
  memberships, manager chains, or home directories.
- The API never logs the result set, only counts and timings.
- Deploy this behind the same access controls as any other system holding
  organization-wide PII; don't expose it on an open/public network without
  authentication in front of it.

## Not verified in this environment

This code was written against the spec's confirmed connection pattern but
could not be built or run here: there is no .NET SDK and no Windows/AD
connectivity in this sandbox. Before relying on it, on a domain-joined
Windows machine run:

```
dotnet restore
dotnet build
dotnet run --project AdUserWebApp
```

and confirm the probe endpoint reports a real `ConnectedServer`, per §1 of
the spec, before pointing it at the full domain.

---
name: api
description: "Use when working on the AssetTag ASP.NET Core Web API (AssetTag/ project): controllers, JWT auth, SQL Server, EF migrations, sync push/pull, invitations, or email links."
---

# AssetTag API

Backend for Portal and MobileApp. Project: `AssetTag/`. Shared models/DTOs live in `Shared/` — do not duplicate them here.

## Hard rules

- **ULID strings:** Entity IDs are `string` ULIDs (`NUlid`), never `Guid` or `int`. Controllers and DTOs expose `id` as `string`.
- **DeletedItem tracking:** `ApplicationDbContext.SaveChangesAsync` writes `DeletedItem` rows on delete for mobile pull. Do not bypass `SaveChanges` for entity deletes without understanding sync.
- **Sync:** `POST api/sync/push` is per-user distributed-locked (409 if another device is syncing). Push/pull uses `Shared.DTOs` (`SyncPushRequestDTO`, deleted-item lists).
- **Auth:** JWT Bearer. Middleware maps `X-Auth-Token` → `Authorization`. Mobile and Portal both send tokens; do not remove that mapping.
- **Email links:** Invitation and password-reset URLs use config `FrontendBaseUrl` (CI secret `FRONTEND_BASE_URL`). Never hardcode hosted Portal hostnames.
- **Migrations:** Not applied on API startup (seed only). Apply with `dotnet ef database update` or the CI `run-migrations` job.
- **Culture:** `en-GH` / `₵` is set in `Program.cs`. Keep money formatting consistent.

## Layout

- `AssetTag/Controllers/` — Auth, Assets, Locations, Categories, Departments, Sync, Users, Invitations, Reports, Dashboard, MobileVersion
- `AssetTag/Data/ApplicationDbContext.cs` — SQL Server + Identity + DeletedItem + DistributedLock
- `AssetTag/Program.cs` — JWT, CORS, culture, `X-Auth-Token` middleware

## Local

```bash
dotnet run --project AssetTag/AssetTag.csproj --launch-profile https
```

HTTPS `https://localhost:7135`. SQL Server `localhost:1433`; connection string in `appsettings.Development.json` (do not commit secrets).

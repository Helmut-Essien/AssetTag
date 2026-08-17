# AGENTS.md

## Solution overview

AssetTag is a 5-project .NET 9 solution (`AssetTag.sln`):

| Project | Type | Role |
|---|---|---|
| `Shared/` | Class Library | DTOs, Models, Constants — referenced by all other projects |
| `AssetTag/` | ASP.NET Core 9 Web API | Backend API |
| `Portal/` | Razor Pages | Web frontend (depends on API) |
| `MobileApp/` | .NET MAUI (Android only) | Mobile client |
| `MobileData/` | Class Library | Mobile local SQLite via EF Core |

Dependency chain: `MobileApp` → `MobileData` → `Shared` ← `AssetTag` ← `Portal`

`Portal` calls `AssetTag` over HTTP. They are separate processes and must both run for local dev.

**Hosted URLs (repo secrets in CI):**
- API `FrontendBaseUrl` — public Portal URL used in invitation/password-reset emails (`FRONTEND_BASE_URL`)
- Portal `Api:BaseUrl` — API the Portal talks to (`API_BASE_URL`)  
Set via GitHub Actions secrets in deploy, or `appsettings.Development.json` / user secrets locally. Never hardcode production/test hostnames in workflow or base appsettings.

## Essential commands

```bash
# Restore and build everything
dotnet restore AssetTag.sln && dotnet build AssetTag.sln

# Run API (HTTPS default)
dotnet run --project AssetTag/AssetTag.csproj --launch-profile https

# Run Portal (HTTPS default)
dotnet run --project Portal/Portal.csproj --launch-profile https

# Run both simultaneously (VS Code compound launch)
# Use the "Launch AssetTag + Portal" compound in .vscode/launch.json
```

Local dev URLs:
- API: `https://localhost:7135` / `http://localhost:5226`
- Portal: `https://localhost:7207` / `http://localhost:5219`

There are **no test projects** and **no lint/format commands** configured.

## Database & migrations

- Production: SQL Server. Local dev: SQL Server on `localhost:1433` (password in `appsettings.Development.json` — never commit).
- **Migrations are NOT auto-applied on startup.** Only seed data runs (creates admin user). Run migrations manually or via CI.
- CI runs `dotnet ef database update` against production in the `run-migrations` job.
- Mobile uses SQLite (`AssetTagOffline.db3` in `FileSystem.AppDataDirectory`). EF Core migrations for the mobile DB run via `MigrationBackgroundService` at MAUI startup.

## Architecture conventions

- **ULID, not GUID**: All entity IDs use `Ulid` from the NUlid package. The `BaseModel` class in `Shared` defines `Id` as ULID.
- **IDs are strings**: Controllers and DTOs expose `id` as `string`, not `Ulid`.
- **Deleted item tracking**: `ApplicationDbContext.SaveChangesAsync` automatically creates `DeletedItem` records for mobile sync. Do not bypass this without understanding the sync pipeline.
- **JWT auth logging**: `Program.cs` wires up extremely verbose JWT event logging (OnTokenValidated, OnChallenge, etc.) and a custom middleware that maps `X-Auth-Token` header → `Authorization` header. Auth failures log full token claims and expiry.
- **Culture**: Ghanaian Cedi (`en-GH`, `₵`) is forced via `CultureInfo.DefaultThreadCurrentCulture`.
- **Mobile is Android-only**: `MobileApp.csproj` targets `net9.0-android` only. Other platforms are commented out. CI builds a signed APK.
- **Mobile compiled bindings**: `MauiEnableXamlCBindingWithSourceCompilation` is enabled globally.
- **MVVM**: Mobile uses `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]` base classes). ViewModels follow the pattern in `MobileApp/ViewModels/BaseViewModel.cs`.

## Agent skill files

Read the skill that matches the area you are changing.

Cursor loads project skills from `.cursor/skills/<name>/SKILL.md`. Copies also live in `.github/skills/` (GitHub Copilot) and `.kiro/skills/` (Kiro). Keep them in sync when you edit a skill.

**Mobile (`MobileApp/`, `MobileData/`):**
- `.cursor/skills/mobile-app/SKILL.md` — MAUI shell, navigation, barcode scanning, session
- `.cursor/skills/mobile-app-sync/SKILL.md` — Offline sync, token refresh, SQLite
- `.cursor/skills/mobile-app-ui-ux/SKILL.md` — Design system (same content as `DESIGN.md`)
- `.cursor/skills/mobile-app-architecture/SKILL.md` — MVVM, DI, offline-first
- `.cursor/skills/mobile-app-data-models/SKILL.md` — `LocalDbContext` and mobile entity mapping
- `.github/instructions/mobile-app-ui-ux.instructions.md` — UI/UX workflow

**API, Portal, Shared, CI:**
- `.cursor/skills/api/SKILL.md` — ASP.NET API, JWT, SQL Server, DeletedItem, sync lock
- `.cursor/skills/portal/SKILL.md` — Razor Pages, `Api:BaseUrl`, cookie session
- `.cursor/skills/shared/SKILL.md` — ULID contracts and DTOs shared by all projects
- `.cursor/skills/ci-cd/SKILL.md` — GitHub Actions, Web Deploy, APK, secrets

## CI/CD

Triggered on push to `master` when any project source changes. Key jobs: detect-changes → build-and-publish → run-migrations → deploy (API/Portal via Web Deploy) → build-android (signed APK) → release-android (GitHub Release).

**Required Actions secrets** (Settings → Secrets and variables → Actions → Secrets) for deploy-time appsettings:

| Secret | Used by | Example (current test host) |
|---|---|---|
| `FRONTEND_BASE_URL` | API — invitation / password-reset links | `https://mugasset.runasp.net/` |
| `API_BASE_URL` | Portal — `Api:BaseUrl` for HttpClients | `https://mugassetapi.runasp.net/` |
| `EMAIL_USERNAME` / `EMAIL_PASSWORD` | API — SMTP auth | test vs production mailboxes |
| `EMAIL_FROM` | API — `EmailSettings:FromEmail` | address shown as sender |
| `EMAIL_FROM_NAME` | API — `EmailSettings:FromName` | display name in client |

Local overrides: `appsettings.Development.json` or user secrets (`FrontendBaseUrl`, `Api:BaseUrl`, `EmailSettings:*`). Do not hardcode hosted URLs or sender identity in workflow or base `appsettings.json`.

# QWEN.md — AssetTag

## Project Overview

**AssetTag** is a full-stack .NET 9 asset management system built for Methodist University Ghana. It provides enterprise-grade asset lifecycle tracking — from procurement through depreciation to disposal — with a web frontend (Razor Pages), REST API (ASP.NET Core), and an Android mobile client (.NET MAUI) with offline-first sync.

### Tech Stack

| Area | Technology |
|---|---|
| Framework | .NET 9 (C# 13) |
| API | ASP.NET Core 9 Web API |
| Frontend | Razor Pages, Bootstrap 5, Chart.js |
| Mobile | .NET MAUI (Android only), CommunityToolkit.Mvvm, Syncfusion |
| Database | SQL Server (prod/dev), SQLite (mobile) |
| ORM | Entity Framework Core 9 |
| Auth | JWT + ASP.NET Identity, refresh tokens |
| AI | Groq API (llama-3.3-70b) for natural-language → SQL |
| Icons | Material Icons (`AathifMahir.Maui.MauiIcons.Material`) |
| ID format | ULID (via NUlid package), exposed as `string` |

### Solution Structure (5 projects)

| Project | Type | Role |
|---|---|---|
| `Shared/` | Class Library | DTOs, Models, Constants — referenced by all |
| `AssetTag/` | ASP.NET Core 9 Web API | Backend REST API |
| `Portal/` | Razor Pages | Web frontend (calls API over HTTP) |
| `MobileApp/` | .NET MAUI (Android) | Mobile client with offline sync |
| `MobileData/` | Class Library | Mobile local SQLite via EF Core |

**Dependency chain:** `MobileApp` → `MobileData` → `Shared` ← `AssetTag` ← `Portal`

## Building & Running

```bash
# Restore and build everything
dotnet restore AssetTag.sln && dotnet build AssetTag.sln

# Run API (HTTPS default)
dotnet run --project AssetTag/AssetTag.csproj --launch-profile https

# Run Portal (HTTPS default)
dotnet run --project Portal/Portal.csproj --launch-profile https

# Run both simultaneously — use the VS Code compound launch:
# "Launch AssetTag + Portal" in .vscode/launch.json
```

**Local dev URLs:**
- API: `https://localhost:7135` / `http://localhost:5226`
- Portal: `https://localhost:7207` / `http://localhost:5219`

There are **no test projects** and **no lint/format commands** configured.

## Database & Migrations

- **Production:** SQL Server. **Local dev:** SQL Server on `localhost:1433` (password in `appsettings.Development.json` — never commit).
- **Migrations are NOT auto-applied on startup.** Only seed data runs (creates admin user). Apply migrations manually or via CI.
- CI runs `dotnet ef database update` against production in the `run-migrations` job.
- Mobile uses SQLite (`AssetTagOffline.db3` in `FileSystem.AppDataDirectory`). Mobile EF Core migrations run via `MigrationBackgroundService` at MAUI startup.

## Key Architecture Conventions

- **ULID, not GUID:** All entity IDs use `Ulid` from the NUlid package. The `BaseModel` class in `Shared` defines `Id` as ULID.
- **IDs are strings:** Controllers and DTOs expose `id` as `string`, not `Ulid`.
- **Deleted item tracking:** `ApplicationDbContext.SaveChangesAsync` automatically creates `DeletedItem` records for mobile sync. Do not bypass this without understanding the sync pipeline.
- **JWT auth logging:** `Program.cs` wires up extremely verbose JWT event logging (OnTokenValidated, OnChallenge, etc.) and a custom middleware that maps `X-Auth-Token` header → `Authorization` header. Auth failures log full token claims and expiry.
- **Culture:** Ghanaian Cedi (`en-GH`, `₵`) is forced via `CultureInfo.DefaultThreadCurrentCulture`.
- **Mobile is Android-only:** `MobileApp.csproj` targets `net9.0-android` only. CI builds a signed APK.
- **Mobile compiled bindings:** `MauiEnableXamlCBindingWithSourceCompilation` is enabled globally.
- **MVVM:** Mobile uses `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`). ViewModels follow `MobileApp/ViewModels/BaseViewModel.cs`.

## API Controllers

| Controller | Purpose |
|---|---|
| `AssetsController` | Asset CRUD operations |
| `AuthController` | Authentication & token endpoints |
| `CategoriesController` | Asset category management |
| `DashboardController` | Dashboard analytics data |
| `DepartmentsController` | Department management |
| `DiagnosticsController` | System diagnostics |
| `AssetHistoriesController` | Audit trail queries |
| `LocationsController` | Location management |
| `ReportsController` | Report generation & AI queries |
| `RoleController` | Role management |
| `TestController` | Test endpoints |
| `UsersController` | User management |

## Mobile App Details

Before modifying the mobile app, read the relevant skill file:

| Skill File | Content |
|---|---|
| `.kiro/skills/mobile-app-architecture.md` | MVVM patterns, DI lifecycle, offline-first architecture, sync, background services |
| `.kiro/skills/mobile-app-data-models.md` | Entity model reference, LocalDbContext rules |
| `.github/skills/mobile-app/SKILL.md` | MAUI shell, navigation, barcode scanning |
| `.github/skills/mobile-app-sync/SKILL.md` | Offline sync service details |
| `.github/skills/mobile-app-ui-ux/SKILL.md` | Design system (same content as `DESIGN.md`) |
| `.github/instructions/mobile-app-ui-ux.instructions.md` | Structured UI/UX workflow |

UI/UX design guidelines are in `DESIGN.md` (color system, typography, spacing, component library, dark mode, accessibility).

## CI/CD

Triggered on push to `master` when any project source changes. Pipeline:
`detect-changes` → `build-and-publish` → `run-migrations` → `deploy` (API/Portal via Web Deploy) → `build-android` (signed APK) → `release-android` (GitHub Release).

## User Secrets

| Project | Secrets ID |
|---|---|
| AssetTag | `48e1817c-9eb7-4bc2-b9f6-6fa87e951008` |
| Portal | `27aaf5cf-affe-4e6f-a34d-fbe0ff896331` |

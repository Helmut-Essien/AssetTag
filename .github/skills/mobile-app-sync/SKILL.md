---
name: mobile-app-sync
description: "Use when working on offline sync, local database, or asset persistence in the AssetTag MobileApp. Covers LocalDbContext, SyncService, AssetService, migrations, and queued push/pull behavior."
---

# AssetTag Mobile App Sync Skill

This skill focuses on the mobile app's offline data layer and synchronization logic.

## What this skill covers

- Local EF Core SQLite setup via `LocalDbContext`
- Service registration and scope management in `MauiProgram.cs`
- `AssetService` read/write methods and offline queue behavior
- `ISyncService` and sync background operations
- Data migrations and `MigrationBackgroundService`
- Query performance optimizations and `AsNoTracking()` usage
- How scanned assets are resolved by `AssetTag` or `DigitalAssetTag`
- Update and create logic that enqueues sync requests after local changes

## When to use this skill

Use this skill when the task involves:

- Debugging offline asset persistence
- Improving sync reliability, batching, or conflict handling
- Adjusting database migrations or schema changes
- Reviewing asset upsert and update paths
- Ensuring local changes queue properly for remote sync
- Adding new persistent fields or entity mappings

## Important paths

- `MobileApp/Services/AssetService.cs`
- `MobileApp/Services/SyncService.cs`
- `MobileApp/Services/ISyncService.cs`
- `MobileApp/Services/TokenRefreshHandler.cs`
- `MobileData/Data/LocalDbContext.cs`
- `MobileApp/MauiProgram.cs`

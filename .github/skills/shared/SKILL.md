---
name: shared
description: "Use when changing Shared/ models, DTOs, or constants used by the API, Portal, and mobile app. Covers ULID IDs, Asset/Location/Category contracts, and sync DTOs."
---

# Shared contracts

`Shared/` is referenced by **AssetTag, Portal, MobileApp, and MobileData**. A model change is an API + mobile + portal change.

Mobile SQLite mapping and `LocalDbContext` rules live in `.kiro/skills/mobile-app-data-models.md` — read that for mobile-only entities (`SyncQueueItem`, `DeviceInfo`).

## Hard rules

- IDs are **ULID strings** (`Ulid.NewUlid().ToString()`). Do not introduce `Guid` or `int` PKs.
- Keep domain types in `Shared.Models` and wire types in `Shared.DTOs`. Controllers should not define parallel DTO classes.
- `Asset` FKs (`CategoryId`, `LocationId`, `DepartmentId`) are required strings. Status/condition values come from `Shared.Constants.AssetConstants`.
- `DeletedItem` is how the API tells mobile what to remove on pull. Changing its shape breaks sync.
- Sync payloads are in `Shared/DTOs/SyncDto.cs`. Mobile `SyncService` and `AssetTag/Controllers/SyncController.cs` must stay aligned.
- Financial computed fields on `Asset` are `[NotMapped]` / `[JsonIgnore]` — do not persist them.

## Layout

- `Shared/Models/` — Asset, Category, Location, Department, AssetHistory, Invitation, DeletedItem
- `Shared/DTOs/` — auth, assets, sync, version check
- `Shared/Constants/` — status, condition, and similar enums-as-strings

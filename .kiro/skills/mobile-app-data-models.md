# Mobile App Data Models & DTO Reference

## Project Layout

Data is split across two projects:

| Project | Namespace | Contents |
|---------|-----------|----------|
| `Shared/` | `Shared.Models`, `Shared.DTOs`, `Shared.Constants` | Domain models, DTOs, constants — shared with API and Portal |
| `MobileData/` | `MobileData.Data` | `LocalDbContext`, `SyncQueueItem`, `DeviceInfo` — mobile-only |

**ID Strategy**: All entity primary keys use **ULID** (`NUlid.Ulid.NewUlid().ToString()`), stored as `string`. Never use `int` or `Guid` for entity IDs.

---

## Domain Entities (`Shared.Models`)

### Asset

The primary entity. All FK fields (`CategoryId`, `LocationId`, `DepartmentId`) are **required strings** pointing to ULID-keyed entities.

```csharp
public class Asset
{
    public string AssetId { get; set; } = Ulid.NewUlid().ToString();
    public required string AssetTag { get; set; }         // Unique index
    public string? OldAssetTag { get; set; }
    public string? DigitalAssetTag { get; set; }          // MaxLength(50)
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string CategoryId { get; set; }       // FK → Category
    public required string LocationId { get; set; }       // FK → Location
    public required string DepartmentId { get; set; }     // FK → Department
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? CurrentValue { get; set; }
    public required string Status { get; set; }           // See AssetConstants.Status
    public string? AssignedToUserId { get; set; }         // String only — no FK in mobile DB
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime DateModified { get; set; } = DateTime.UtcNow;
    public DateTime? LastScannedAt { get; set; }
    public string? SerialNumber { get; set; }
    public required string Condition { get; set; }        // See AssetConstants.Condition

    // Vendor / Invoice
    public string? VendorName { get; set; }
    public string? InvoiceNumber { get; set; }

    // Financial (stored)
    public int Quantity { get; set; } = 1;
    public decimal? CostPerUnit { get; set; }
    public int? UsefulLifeYears { get; set; }             // Overrides calculated value if set

    // Financial (computed — [NotMapped][JsonIgnore])
    // CalculatedUsefulLifeYears: UsefulLifeYears ?? ceil(100 / Category.DepreciationRate)
    // TotalCost:                 CostPerUnit × Quantity
    // AccumulatedDepreciation:   PurchasePrice × (DepreciationRate/100) × yearsOwned, capped at PurchasePrice
    // NetBookValue:              PurchasePrice − AccumulatedDepreciation (min 0)
    // GainLossOnDisposal:        DisposalValue − NetBookValue (only when disposed)

    // Lifecycle
    public DateTime? WarrantyExpiry { get; set; }
    public DateTime? DisposalDate { get; set; }
    public decimal? DisposalValue { get; set; }
    public string? Remarks { get; set; }

    // Navigation
    public Category? Category { get; set; }
    public Location? Location { get; set; }
    public Department? Department { get; set; }
    // AssignedToUser navigation is IGNORED in mobile DB (AssignedToUserId is a string only)
    public ICollection<AssetHistory> AssetHistories { get; set; } = new List<AssetHistory>();
}
```

### Category

```csharp
public class Category
{
    public string CategoryId { get; set; } = Ulid.NewUlid().ToString();
    public required string Name { get; set; }
    public string? Description { get; set; }
    // Annual depreciation % (0–100). Example: 20.0 = 20%/year → 5 year useful life
    public decimal? DepreciationRate { get; set; }
    public DateTime DateModified { get; set; } = DateTime.UtcNow;
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
```

### Location

```csharp
public class Location
{
    public string LocationId { get; set; } = Ulid.NewUlid().ToString();
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Campus { get; set; }
    public string? Building { get; set; }
    public string? Room { get; set; }
    public double? Latitude { get; set; }   // [Range(-90, 90)]
    public double? Longitude { get; set; }  // [Range(-180, 180)]
    public DateTime DateModified { get; set; } = DateTime.UtcNow;
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
// Unique index: (Name, Campus)
```

### Department

```csharp
public class Department
{
    public string DepartmentId { get; set; } = Ulid.NewUlid().ToString();
    public required string Name { get; set; }   // Unique index
    public string? Description { get; set; }
    public DateTime DateModified { get; set; } = DateTime.UtcNow;
    // Users navigation is IGNORED in mobile DB
    public ICollection<Asset>? Assets { get; set; } = new List<Asset>();
}
```

### AssetHistory

```csharp
public class AssetHistory
{
    public string HistoryId { get; set; } = Ulid.NewUlid().ToString();
    public required string AssetId { get; set; }
    public required string UserId { get; set; }
    public required string Action { get; set; }      // See AssetConstants.HistoryAction
    public required string Description { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? OldLocationId { get; set; }
    public string? NewLocationId { get; set; }
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    // Navigation (User/OldLocation/NewLocation navigations ignored in mobile DB)
    public Asset? Asset { get; set; }
}
// Index: Timestamp DESC
```

---

## Mobile-Only Tables (`MobileData.Data`)

These are defined inside `LocalDbContext.cs`, not in `Shared/`.

### SyncQueueItem

Tracks local writes pending upload. Auto-populated by `LocalDbContext.SaveChanges` via `QueueSyncOperations()`. Only `Asset` and `AssetHistory` are tracked (Categories, Locations, Departments are read-only on mobile).

```csharp
public class SyncQueueItem
{
    public int Id { get; set; }                    // Auto-increment PK
    public string EntityType { get; set; }         // "Asset" | "AssetHistory"
    public string EntityId { get; set; }           // ULID of the entity
    public string Operation { get; set; }          // "CREATE" | "UPDATE" | "DELETE"
    public string JsonData { get; set; }           // Full JSON snapshot of entity
    public DateTime CreatedAt { get; set; }        // Default: CURRENT_TIMESTAMP
    public int RetryCount { get; set; }            // Incremented on failed push
}
// Index: (EntityType, Operation)
```

### DeviceInfo

Singleton table (always 1 row). Tracks sync state.

```csharp
public class DeviceInfo
{
    public int Id { get; set; }
    public string DeviceId { get; set; }    // Unique device identifier
    public DateTime LastSync { get; set; }  // Used as delta timestamp for pull requests
    public string SyncToken { get; set; }   // Reserved for future use
}
```

**CRITICAL**: `LastSync` does NOT use `HasDefaultValueSql` — it resets on restart if it did. Always set it explicitly in code after a successful pull.

---

## DTOs (`Shared.DTOs`)

All DTOs are `record` types (immutable, init-only). Use the correct DTO for each operation.

### Asset DTOs

| DTO | Use Case | Required Fields |
|-----|----------|-----------------|
| `AssetCreateDTO` | POST new asset | `AssetTag`, `Name`, `CategoryId`, `LocationId`, `DepartmentId`, `Status`, `Condition` |
| `AssetUpdateDTO` | PUT/PATCH existing asset | `AssetId` (all others optional) |
| `AssetReadDTO` | Returned from API / used in sync pull | All fields + computed financials flattened |

**Key difference**: `AssetReadDTO` includes computed fields (`AccumulatedDepreciation`, `NetBookValue`, `TotalCost`, `GainLossOnDisposal`, `CalculatedUsefulLifeYears`, `DepreciationRate`) that are `[NotMapped]` on the domain model — the API computes and serializes these for the mobile client.

### Reference Data DTOs

```csharp
// Category
record CategoryReadDTO(string CategoryId, string Name, string? Description, decimal? DepreciationRate);

// Location
record LocationReadDTO(string LocationId, string Name, string? Description, string Campus, string? Building, string? Room, double? Latitude, double? Longitude);

// Department
record DepartmentReadDTO(string DepartmentId, string Name, string? Description);
```

### Sync DTOs

```csharp
// Push (Mobile → Server)
record SyncPushRequestDTO
{
    List<SyncOperationDTO> Operations;
    string DeviceId;
}

record SyncOperationDTO
{
    int QueueItemId;       // Local SyncQueue.Id — used to track which succeeded
    string EntityType;     // "Asset" | "AssetHistory"
    string EntityId;       // ULID
    string Operation;      // "CREATE" | "UPDATE" | "DELETE"
    string JsonData;       // Serialized entity
    DateTime CreatedAt;
}

record SyncPushResponseDTO
{
    int SuccessCount;
    int FailureCount;
    List<SyncErrorDTO> Errors;
    List<int> SuccessfulOperationIds;  // QueueItemIds to remove from local queue
}

// Pull (Server → Mobile)
record SyncPullRequestDTO
{
    DateTime? LastSyncTimestamp;   // From DeviceInfo.LastSync — null = full sync
    string DeviceId;
}

record SyncPullResponseDTO
{
    // Ordered by dependency (reference data FIRST, assets LAST)
    List<CategoryReadDTO> Categories;
    List<LocationReadDTO> Locations;
    List<DepartmentReadDTO> Departments;
    List<AssetReadDTO> Assets;
    DateTime ServerTimestamp;       // Store this in DeviceInfo.LastSync after successful pull
}
```

### Auth DTOs

```csharp
record LoginDTO(string Email, string Password);
record TokenResponseDTO(string AccessToken, string RefreshToken);
record TokenRequestDTO(string AccessToken, string RefreshToken);  // For refresh endpoint
```

---

## Constants (`Shared.Constants.AssetConstants`)

Always use constants — never hardcode status/condition strings.

```csharp
// Status values
AssetConstants.Status.Available          // "Available"
AssetConstants.Status.InUse              // "In Use"
AssetConstants.Status.UnderMaintenance   // "Under Maintenance"
AssetConstants.Status.Disposed           // "Disposed"
AssetConstants.Status.Retired            // "Retired"
AssetConstants.Status.Lost               // "Lost"
AssetConstants.Status.Stolen             // "Stolen"
AssetConstants.Status.All                // All values as string[]

// Condition values
AssetConstants.Condition.Excellent       // "Excellent"
AssetConstants.Condition.Good            // "Good"
AssetConstants.Condition.Fair            // "Fair"
AssetConstants.Condition.Poor            // "Poor"
AssetConstants.Condition.Broken          // "Broken"
AssetConstants.Condition.RequiresMaintenance  // ["Fair", "Poor", "Broken"]

// History actions
AssetConstants.HistoryAction.Added
AssetConstants.HistoryAction.Purchased
AssetConstants.HistoryAction.Maintenance
AssetConstants.HistoryAction.Transferred
AssetConstants.HistoryAction.Assigned
AssetConstants.HistoryAction.Unassigned
AssetConstants.HistoryAction.Disposed
AssetConstants.HistoryAction.Updated
AssetConstants.HistoryAction.StatusChanged

// Report constants
AssetConstants.Reports.DefaultPageSize            // 50
AssetConstants.Reports.MaxPageSize                // 500
AssetConstants.Reports.WarrantyExpiryWarningDays  // 90
AssetConstants.Reports.WarrantyExpiryCriticalDays // 30
AssetConstants.Reports.WarrantyExpiryHighDays     // 60
```

---

## LocalDbContext Key Rules

### What IS tracked in the mobile DB
- `Asset`, `AssetHistory`, `Category`, `Department`, `Location`
- `SyncQueueItem`, `DeviceInfo`

### What is NOT in the mobile DB
- `ApplicationUser` — **ignored** via `mb.Ignore<ApplicationUser>()`
- `Department.Users` navigation — **ignored**
- `Asset.AssignedToUser` navigation — **ignored**
- No shadow properties for sync state — sync state lives in `SyncQueue` only

### Delete Behaviors (mobile-safe)
| Relationship | Behavior |
|---|---|
| Asset → Category | `SetNull` (not Restrict) |
| Asset → Location | `SetNull` |
| Asset → Department | `SetNull` |
| Asset → AssetHistories | `Cascade` |

### Auto-tracked Entities
`LocalDbContext.SaveChanges` / `SaveChangesAsync` automatically call `QueueSyncOperations()` which serializes `Asset` and `AssetHistory` changes to `SyncQueue` — **but only when `ChangeTracker.AutoDetectChangesEnabled = true`**.

During pull sync, always disable change tracking:
```csharp
try
{
    dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
    // ...apply server data...
    await dbContext.SaveChangesAsync();
}
finally
{
    dbContext.ChangeTracker.AutoDetectChangesEnabled = true;
}
```

### Performance Indexes
| Table | Index |
|-------|-------|
| Asset | `AssetTag` (unique), `Status`, `AssignedToUserId` (partial, not null) |
| AssetHistory | `Timestamp` DESC |
| Location | `(Name, Campus)` unique |
| Department | `Name` unique |
| SyncQueueItem | `(EntityType, Operation)` |

---

## Adding a New Entity Checklist

When adding a new syncable entity to the mobile app:

1. **`Shared/Models/`** — Add entity class with ULID PK and `DateModified` field
2. **`Shared/DTOs/`** — Add `CreateDTO`, `UpdateDTO`, `ReadDTO` records
3. **`MobileData/Data/LocalDbContext.cs`**:
   - Add `DbSet<T>` property
   - Configure key, indexes, and relationships in `ConfigureCoreEntities`
4. **`Shared/DTOs/SyncDto.cs`** — Add `List<TReadDTO>` to `SyncPullResponseDTO` (reference data goes before Assets)
5. **`LocalDbContext.QueueSyncOperations()`** — Add `else if (entry.Entity is T)` block if the entity needs to be pushed (writable on mobile)
6. **`MobileData/Migrations/`** — Generate a new EF migration

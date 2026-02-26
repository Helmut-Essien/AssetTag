using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Shared.Models;
using System.IO;
using Location = Shared.Models.Location;

namespace MobileData.Data
{
    public sealed class LocalDbContext : DbContext
    {
        private readonly string _dbPath;

        /* ---------------  Core Tables --------------- */
        public DbSet<Asset> Assets => Set<Asset>();
        public DbSet<AssetHistory> AssetHistories => Set<AssetHistory>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Department> Departments => Set<Department>();
        public DbSet<Location> Locations => Set<Location>();

        /* ---------------  Optional Sync Tables --------------- */
        public DbSet<SyncQueueItem> SyncQueue => Set<SyncQueueItem>();
        public DbSet<DeviceInfo> DeviceInfo => Set<DeviceInfo>();

        /* ---------------  Path --------------- */
        public LocalDbContext(DbContextOptions<LocalDbContext> options, string dbPath)
            : base(options)
        {
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
        }

        /* --------------- Configuration --------------- */
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Fallback only (should not hit in production)
                optionsBuilder.UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), "AssetTagFallback.db3")}");
            }

#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
#endif

            // Optional: Enable WAL mode for better concurrency on mobile
            // (Add via connection string in MAUI registration if needed)
        }

        /* ---------------  Optimized Model --------------- */
        protected override void OnModelCreating(ModelBuilder mb)
        {
            // Your clean base configuration
            ConfigureCoreEntities(mb);

            // Optional sync tables (only if needed)
            ConfigureSyncEntities(mb);

            // Performance optimizations
            ConfigurePerformance(mb);
        }

        private void ConfigureCoreEntities(ModelBuilder mb)
        {
            /* ---- Keys & Indexes (same as API) ---- */
            mb.Entity<Asset>().HasKey(a => a.AssetId);
            mb.Entity<AssetHistory>().HasKey(h => h.HistoryId);
            mb.Entity<Category>().HasKey(c => c.CategoryId);
            mb.Entity<Department>().HasKey(d => d.DepartmentId);
            mb.Entity<Location>().HasKey(l => l.LocationId);

            /* ---- Unique indexes ---- */
            mb.Entity<Asset>().HasIndex(a => a.AssetTag).IsUnique();
            mb.Entity<Department>().HasIndex(d => d.Name).IsUnique();
            mb.Entity<Location>().HasIndex(l => new { l.Name, l.Campus }).IsUnique();

            /* ---- Relationships with SQLite-safe delete ---- */
            // Use SetNull instead of Restrict for better mobile UX
            mb.Entity<Asset>()
              .HasOne(a => a.Category)
              .WithMany(c => c.Assets)
              .HasForeignKey(a => a.CategoryId)
              .OnDelete(DeleteBehavior.SetNull); // Better than Restrict for mobile

            mb.Entity<Asset>()
              .HasOne(a => a.Location)
              .WithMany(l => l.Assets)
              .HasForeignKey(a => a.LocationId)
              .OnDelete(DeleteBehavior.SetNull);

            mb.Entity<Asset>()
              .HasOne(a => a.Department)
              .WithMany(d => d.Assets)
              .HasForeignKey(a => a.DepartmentId)
              .OnDelete(DeleteBehavior.SetNull);

            mb.Entity<Asset>()
              .HasMany(a => a.AssetHistories)
              .WithOne(h => h.Asset)
              .HasForeignKey(h => h.AssetId)
              .OnDelete(DeleteBehavior.Cascade);

            /* ---- Minimal sync tracking ---- */
            // Add only if you need basic sync
            mb.Entity<Asset>()
              .Property<DateTime?>("LastSyncedUtc")
              .HasDefaultValue(null);

            mb.Entity<Asset>()
              .Property<bool>("IsPendingSync")
              .HasDefaultValue(false);
        }

        private void ConfigureSyncEntities(ModelBuilder mb)
        {
            // Only include if you need offline write queuing
            mb.Entity<SyncQueueItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.HasIndex(e => new { e.EntityType, e.Operation });
            });

            mb.Entity<DeviceInfo>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.LastSync)
                      .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }

        private void ConfigurePerformance(ModelBuilder mb)
        {
            // Add indexes for common queries
            mb.Entity<Asset>()
              .HasIndex(a => a.Status);

            mb.Entity<Asset>()
              .HasIndex(a => a.AssignedToUserId)
              .HasFilter("[AssignedToUserId] IS NOT NULL");

            mb.Entity<AssetHistory>()
              .HasIndex(h => h.Timestamp)
              .IsDescending();
        }

        /* ---------------  Optional: Change Tracking --------------- */
        public override int SaveChanges()
        {
            QueueSyncOperations();
            UpdateSyncMetadata();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            QueueSyncOperations();
            UpdateSyncMetadata();
            return base.SaveChangesAsync(ct);
        }

        private void QueueSyncOperations()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is Asset asset)
                {
                    string? operation = entry.State switch
                    {
                        EntityState.Added => "CREATE",
                        EntityState.Modified => "UPDATE",
                        EntityState.Deleted => "DELETE",
                        _ => null
                    };

                    if (operation != null)
                    {
                        var queueItem = new SyncQueueItem
                        {
                            EntityType = "Asset",
                            EntityId = asset.AssetId,
                            Operation = operation,
                            JsonData = System.Text.Json.JsonSerializer.Serialize(asset),
                            CreatedAt = DateTime.UtcNow,
                            RetryCount = 0
                        };

                        SyncQueue.Add(queueItem);
                    }
                }
            }
        }

        private void UpdateSyncMetadata()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Modified ||
                    entry.State == EntityState.Added)
                {
                    // Mark as needing sync
                    if (entry.Metadata.ClrType == typeof(Asset) ||
                        entry.Metadata.ClrType == typeof(AssetHistory))
                    {
                        entry.Property("IsPendingSync").CurrentValue = true;
                        entry.Property("LastSyncedUtc").CurrentValue = null;
                    }
                }
            }
        }
    }

    /* ---------------  Optional Sync Classes --------------- */
    // Only define these IF you need queued sync
    public class SyncQueueItem
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty; // "CREATE", "UPDATE", "DELETE"
        public string JsonData { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int RetryCount { get; set; }
    }

    public class DeviceInfo
    {
        public int Id { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public DateTime LastSync { get; set; }
        public string SyncToken { get; set; } = string.Empty;
    }
}

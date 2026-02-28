using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

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
                var connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), "AssetTagFallback.db3")};";
                // Add SQLite pragmas to connection string for better performance
                connectionString += "Cache=Shared;Mode=Memory;Journal Mode=Wal;Synchronous=Normal;Temp Store=Memory;Cache Size=64000;";
                optionsBuilder.UseSqlite(connectionString);
            }

#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
#endif
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

            // BUG FIX #4: Removed redundant IsPendingSync and LastSyncedUtc shadow properties
            // Sync state is already tracked in SyncQueue table, no need to duplicate it here
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
            // BUG FIX #4: Removed UpdateSyncMetadata() - no longer needed
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            QueueSyncOperations();
            // BUG FIX #4: Removed UpdateSyncMetadata() - no longer needed
            return base.SaveChangesAsync(ct);
        }

        private void QueueSyncOperations()
        {
            // Configure JSON serializer to handle circular references
            var jsonOptions = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };

            // Materialize entries to a list to avoid "Collection was modified" exception
            // when adding SyncQueue items during enumeration
            var entries = ChangeTracker.Entries().ToList();
            
            foreach (var entry in entries)
            {
                string? operation = entry.State switch
                {
                    EntityState.Added => "CREATE",
                    EntityState.Modified => "UPDATE",
                    EntityState.Deleted => "DELETE",
                    _ => null
                };

                if (operation == null) continue;

                SyncQueueItem? queueItem = null;

                if (entry.Entity is Asset asset)
                {
                    queueItem = new SyncQueueItem
                    {
                        EntityType = "Asset",
                        EntityId = asset.AssetId,
                        Operation = operation,
                        JsonData = JsonSerializer.Serialize(asset, jsonOptions),
                        CreatedAt = DateTime.UtcNow,
                        RetryCount = 0
                    };
                }
                else if (entry.Entity is AssetHistory history)
                {
                    queueItem = new SyncQueueItem
                    {
                        EntityType = "AssetHistory",
                        EntityId = history.HistoryId.ToString(),
                        Operation = operation,
                        JsonData = JsonSerializer.Serialize(history, jsonOptions),
                        CreatedAt = DateTime.UtcNow,
                        RetryCount = 0
                    };
                }

                if (queueItem != null)
                {
                    SyncQueue.Add(queueItem);
                }
            }
        }
        
        // BUG FIX #4: Removed UpdateSyncMetadata() method entirely
        // Sync state is tracked in SyncQueue table only, no need for shadow properties
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

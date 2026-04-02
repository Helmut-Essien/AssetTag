using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace AssetTag.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<Asset> Assets { get; set; }
        public DbSet<AssetHistory> AssetHistories { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<RefreshTokens> RefreshTokens { get; set; }
        public DbSet<Invitation> Invitations { get; set; }
        
        // FIX #5: Track deleted items for mobile sync
        public DbSet<DeletedItem> DeletedItems { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // FIX #5: Override SaveChanges to automatically track deletions
        public override int SaveChanges()
        {
            TrackDeletions();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            TrackDeletions();
            return base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Automatically tracks entity deletions by creating DeletedItem records.
        /// This ensures mobile clients can sync deletions made through Portal/API.
        /// </summary>
        private void TrackDeletions()
        {
            var deletedEntries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in deletedEntries)
            {
                string? entityType = null;
                string? entityId = null;

                // Identify the entity type and extract its ID
                // Skip internal entities that don't need deletion tracking
                if (entry.Entity is AssetHistory ||
                    entry.Entity is RefreshTokens ||
                    entry.Entity is Invitation ||
                    entry.Entity is DeletedItem)
                {
                    continue;
                }

                switch (entry.Entity)
                {
                    case Asset asset:
                        entityType = "Asset";
                        entityId = asset.AssetId;
                        break;
                    case Category category:
                        entityType = "Category";
                        entityId = category.CategoryId;
                        break;
                    case Location location:
                        entityType = "Location";
                        entityId = location.LocationId;
                        break;
                    case Department department:
                        entityType = "Department";
                        entityId = department.DepartmentId;
                        break;
                }

                // Create deletion tracking record if entity type is tracked
                if (entityType != null && entityId != null)
                {
                    var deletedItem = new DeletedItem
                    {
                        EntityType = entityType,
                        EntityId = entityId,
                        DeletedAt = DateTime.UtcNow,
                        Reason = "Deleted via Portal/API"
                    };

                    DeletedItems.Add(deletedItem);
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ApplicationUser configurations
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Assets)
                .WithOne(a => a.AssignedToUser)
                .HasForeignKey(a => a.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.RefreshTokens)
                .WithOne(r => r.ApplicationUser)
                .HasForeignKey(r => r.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Asset configurations
            modelBuilder.Entity<Asset>()
                .HasKey(a => a.AssetId);

            modelBuilder.Entity<Asset>()
                .HasIndex(a => a.AssetTag)
                .IsUnique();

            modelBuilder.Entity<Asset>()
                .HasOne(a => a.Category)
                .WithMany(c => c.Assets)
                .HasForeignKey(a => a.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Asset>()
                .HasOne(a => a.Location)
                .WithMany(l => l.Assets)
                .HasForeignKey(a => a.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Asset>()
                .HasOne(a => a.Department)
                .WithMany(d => d.Assets)
                .HasForeignKey(a => a.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Asset>()
                .HasMany(a => a.AssetHistories)
                .WithOne(h => h.Asset)
                .HasForeignKey(h => h.AssetId)
                .OnDelete(DeleteBehavior.Cascade);

            // AssetHistory configurations
            modelBuilder.Entity<AssetHistory>()
                .HasKey(h => h.HistoryId);

            modelBuilder.Entity<AssetHistory>()
                .HasOne(h => h.User)
                .WithMany()
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AssetHistory>()
                .HasOne(h => h.OldLocation)
                .WithMany()
                .HasForeignKey(h => h.OldLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AssetHistory>()
                .HasOne(h => h.NewLocation)
                .WithMany()
                .HasForeignKey(h => h.NewLocationId)
                .OnDelete(DeleteBehavior.SetNull);

            // Category DeleteBehavior.Restrict
            modelBuilder.Entity<Category>()
                .HasKey(c => c.CategoryId);

            // Department configurations
            modelBuilder.Entity<Department>()
                .HasKey(d => d.DepartmentId);

            modelBuilder.Entity<Department>()
                .HasIndex(d => d.Name)
                .IsUnique();


            // Location configurations
            modelBuilder.Entity<Location>()
                .HasKey(l => l.LocationId);

            modelBuilder.Entity<Location>()
                .HasIndex(l => new { l.Name, l.Campus })
                .IsUnique();

            // RefreshTokens configurations
            modelBuilder.Entity<RefreshTokens>()
                .HasKey(r => r.Id);

            // Configure Invitation entity
            modelBuilder.Entity<Invitation>(entity =>
            {
                entity.HasIndex(i => i.Token).IsUnique();
                entity.HasIndex(i => i.Email);
                entity.HasIndex(i => i.IsUsed);

                entity.HasOne(i => i.InvitedByUser)
                    .WithMany()
                    .HasForeignKey(i => i.InvitedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // FIX #5: Configure DeletedItem entity for tracking deletions
            modelBuilder.Entity<DeletedItem>(entity =>
            {
                entity.HasKey(d => d.DeletedItemId);
                
                // Index on DeletedAt for efficient time-based queries
                entity.HasIndex(d => d.DeletedAt);
                
                // Composite index for efficient lookups by entity type and deletion time
                entity.HasIndex(d => new { d.EntityType, d.DeletedAt });
                
                // Index on EntityId for checking if specific entity was deleted
                entity.HasIndex(d => new { d.EntityType, d.EntityId });
            });
        }
    }
}
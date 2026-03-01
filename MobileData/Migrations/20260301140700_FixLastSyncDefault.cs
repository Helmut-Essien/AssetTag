using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileData.Migrations
{
    /// <inheritdoc />
    public partial class FixLastSyncDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite doesn't support ALTER COLUMN to remove default values properly
            // We need to recreate the table without the default value
            
            // Step 1: Create a new temporary table without the default value
            migrationBuilder.Sql(@"
                CREATE TABLE DeviceInfo_temp (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    DeviceId TEXT NOT NULL,
                    LastSync TEXT NOT NULL,
                    SyncToken TEXT NOT NULL
                );
            ");
            
            // Step 2: Copy existing data to the new table
            migrationBuilder.Sql(@"
                INSERT INTO DeviceInfo_temp (Id, DeviceId, LastSync, SyncToken)
                SELECT Id, DeviceId, LastSync, SyncToken FROM DeviceInfo;
            ");
            
            // Step 3: Drop the old table
            migrationBuilder.Sql("DROP TABLE DeviceInfo;");
            
            // Step 4: Rename the new table to the original name
            migrationBuilder.Sql("ALTER TABLE DeviceInfo_temp RENAME TO DeviceInfo;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate with default value if rolling back
            migrationBuilder.Sql(@"
                CREATE TABLE DeviceInfo_temp (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    DeviceId TEXT NOT NULL,
                    LastSync TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    SyncToken TEXT NOT NULL
                );
            ");
            
            migrationBuilder.Sql(@"
                INSERT INTO DeviceInfo_temp (Id, DeviceId, LastSync, SyncToken)
                SELECT Id, DeviceId, LastSync, SyncToken FROM DeviceInfo;
            ");
            
            migrationBuilder.Sql("DROP TABLE DeviceInfo;");
            
            migrationBuilder.Sql("ALTER TABLE DeviceInfo_temp RENAME TO DeviceInfo;");
        }
    }
}
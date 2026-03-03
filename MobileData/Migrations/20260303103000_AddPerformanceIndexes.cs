using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileData.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add index on DateModified for faster "scanned today" queries
            migrationBuilder.CreateIndex(
                name: "IX_Assets_DateModified",
                table: "Assets",
                column: "DateModified");

            // Add index on CategoryId for faster category filtering
            migrationBuilder.CreateIndex(
                name: "IX_Assets_CategoryId",
                table: "Assets",
                column: "CategoryId");

            // Add index on LocationId for faster location filtering
            migrationBuilder.CreateIndex(
                name: "IX_Assets_LocationId",
                table: "Assets",
                column: "LocationId");

            // Add composite index on SyncQueue for faster pending sync lookups
            migrationBuilder.CreateIndex(
                name: "IX_SyncQueue_EntityType_EntityId",
                table: "SyncQueue",
                columns: new[] { "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assets_DateModified",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_CategoryId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_LocationId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_SyncQueue_EntityType_EntityId",
                table: "SyncQueue");
        }
    }
}
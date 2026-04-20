using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTag.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedItemsTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeletedItems",
                columns: table => new
                {
                    DeletedItemId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedItems", x => x.DeletedItemId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeletedItems_DeletedAt",
                table: "DeletedItems",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedItems_EntityType_DeletedAt",
                table: "DeletedItems",
                columns: new[] { "EntityType", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeletedItems_EntityType_EntityId",
                table: "DeletedItems",
                columns: new[] { "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeletedItems");
        }
    }
}

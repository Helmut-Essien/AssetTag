using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileData.Migrations
{
    /// <inheritdoc />
    public partial class AddSkippedAssetsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SkippedAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssetId = table.Column<string>(type: "TEXT", nullable: false),
                    AssetTag = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    SkippedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MissingCategoryId = table.Column<string>(type: "TEXT", nullable: true),
                    MissingLocationId = table.Column<string>(type: "TEXT", nullable: true),
                    MissingDepartmentId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkippedAssets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SkippedAssets_AssetId",
                table: "SkippedAssets",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_SkippedAssets_SkippedAt",
                table: "SkippedAssets",
                column: "SkippedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SkippedAssets");
        }
    }
}

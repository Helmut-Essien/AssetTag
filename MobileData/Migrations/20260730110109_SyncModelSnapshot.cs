using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileData.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetHistories_Locations_NewLocationId",
                table: "AssetHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetHistories_Locations_OldLocationId",
                table: "AssetHistories");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetHistories_Locations_NewLocationId",
                table: "AssetHistories",
                column: "NewLocationId",
                principalTable: "Locations",
                principalColumn: "LocationId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetHistories_Locations_OldLocationId",
                table: "AssetHistories",
                column: "OldLocationId",
                principalTable: "Locations",
                principalColumn: "LocationId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetHistories_Locations_NewLocationId",
                table: "AssetHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetHistories_Locations_OldLocationId",
                table: "AssetHistories");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetHistories_Locations_NewLocationId",
                table: "AssetHistories",
                column: "NewLocationId",
                principalTable: "Locations",
                principalColumn: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetHistories_Locations_OldLocationId",
                table: "AssetHistories",
                column: "OldLocationId",
                principalTable: "Locations",
                principalColumn: "LocationId");
        }
    }
}

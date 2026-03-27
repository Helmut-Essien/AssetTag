using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTag.Migrations
{
    /// <inheritdoc />
    public partial class AddLastScannedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastScannedAt",
                table: "Assets",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastScannedAt",
                table: "Assets");
        }
    }
}

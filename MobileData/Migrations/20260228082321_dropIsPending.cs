using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileData.Migrations
{
    /// <inheritdoc />
    public partial class dropIsPending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPendingSync",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LastSyncedUtc",
                table: "Assets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPendingSync",
                table: "Assets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedUtc",
                table: "Assets",
                type: "TEXT",
                nullable: true);
        }
    }
}

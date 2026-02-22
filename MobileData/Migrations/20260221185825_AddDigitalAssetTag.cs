using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileData.Migrations
{
    /// <inheritdoc />
    public partial class AddDigitalAssetTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccumulatedDepreciation",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DepreciationRate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "NetBookValue",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "Assets");

            migrationBuilder.AlterColumn<decimal>(
                name: "DepreciationRate",
                table: "Categories",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DigitalAssetTag",
                table: "Assets",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DigitalAssetTag",
                table: "Assets");

            migrationBuilder.AlterColumn<int>(
                name: "DepreciationRate",
                table: "Categories",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AccumulatedDepreciation",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepreciationRate",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetBookValue",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "Assets",
                type: "TEXT",
                nullable: true);
        }
    }
}

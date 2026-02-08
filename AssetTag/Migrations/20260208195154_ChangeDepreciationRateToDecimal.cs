using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTag.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDepreciationRateToDecimal : Migration
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
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "DepreciationRate",
                table: "Categories",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AccumulatedDepreciation",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepreciationRate",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetBookValue",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);
        }
    }
}

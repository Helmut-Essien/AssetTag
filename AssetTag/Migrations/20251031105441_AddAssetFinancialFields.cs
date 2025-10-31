using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTag.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetFinancialFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AccumulatedDepreciation",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostPerUnit",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepreciationRate",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisposalDate",
                table: "Assets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DisposalValue",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetBookValue",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "Assets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsefulLifeYears",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorName",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WarrantyExpiry",
                table: "Assets",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccumulatedDepreciation",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CostPerUnit",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DepreciationRate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DisposalDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DisposalValue",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "NetBookValue",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "UsefulLifeYears",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "VendorName",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "WarrantyExpiry",
                table: "Assets");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTag.Migrations
{
    /// <inheritdoc />
    public partial class AddDepreciationRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepreciationRate",
                table: "Categories",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepreciationRate",
                table: "Categories");
        }
    }
}

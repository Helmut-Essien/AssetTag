using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetTag.Migrations
{
    /// <inheritdoc />
    public partial class AddDigitalAssetTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DigitalAssetTag",
                table: "Assets",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DigitalAssetTag",
                table: "Assets");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddWarrantyPeriodToServiceItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WarrantyPeriod",
                table: "ServiceItems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WarrantyPeriod",
                table: "ServiceItems");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountingMonth",
                table: "ServiceOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountingYear",
                table: "ServiceOrders",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountingMonth",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "AccountingYear",
                table: "ServiceOrders");
        }
    }
}

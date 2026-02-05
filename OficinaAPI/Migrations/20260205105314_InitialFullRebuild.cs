using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialFullRebuild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionDate",
                table: "ServiceOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ServiceOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletionDate",
                table: "ServiceOrders");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ServiceOrders");
        }
    }
}

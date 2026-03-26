using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OficinaAPI.Migrations
{
    public partial class UpdateHomeAndMigrateAvulsos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('Notes', 'U') IS NULL BEGIN CREATE TABLE [Notes] ([Id] int NOT NULL IDENTITY, [Content] nvarchar(max) NOT NULL, [CreatedAt] datetime2 NOT NULL, CONSTRAINT [PK_Notes] PRIMARY KEY ([Id])); END");

            migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Avulsos') " +
                                 "INSERT INTO Notes (Content, CreatedAt) " +
                                 "SELECT 'Valor Avulso Migrado: ' + CAST(Valor AS VARCHAR), GETDATE() FROM Avulsos");

            migrationBuilder.Sql("IF OBJECT_ID('PaymentRecords', 'U') IS NOT NULL DROP TABLE [PaymentRecords]");

            migrationBuilder.DropForeignKey(name: "FK_ServiceItems_Employees_MechanicId", table: "ServiceItems");

            migrationBuilder.DropIndex(name: "IX_ServiceItems_MechanicId", table: "ServiceItems");

            migrationBuilder.DropColumn(name: "MechanicId", table: "ServiceItems");

            migrationBuilder.Sql("IF OBJECT_ID('Employees', 'U') IS NOT NULL DROP TABLE [Employees]");

            migrationBuilder.Sql("IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Avulsos') DROP TABLE Avulsos");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    BaseSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_Employees", x => x.Id); });

            migrationBuilder.AddColumn<int>(
                name: "MechanicId",
                table: "ServiceItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_MechanicId",
                table: "ServiceItems",
                column: "MechanicId");

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceItems_Employees_MechanicId",
                table: "ServiceItems",
                column: "MechanicId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.CreateTable(
                name: "PaymentRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReferenceMonth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    AdminNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.DropTable(name: "Notes");
        }
    }
}


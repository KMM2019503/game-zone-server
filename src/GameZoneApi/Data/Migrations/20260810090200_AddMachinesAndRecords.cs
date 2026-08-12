using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GameZoneApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMachinesAndRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Specs = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    HourlyRate = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Records_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Records_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Machines",
                columns: new[] { "Id", "HourlyRate", "IsActive", "Name", "Specs" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), 8.00m, true, "Rig-01", "RTX 4090 / i9-14900K / 64GB" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), 6.00m, true, "Rig-02", "RTX 4070 Ti / i7-13700K / 32GB" },
                    { new Guid("33333333-3333-3333-3333-333333333333"), 4.50m, true, "Rig-03", "RTX 4060 / i5-13400F / 16GB" },
                    { new Guid("44444444-4444-4444-4444-444444444444"), 5.00m, true, "Console-01", "PlayStation 5 Pro" },
                    { new Guid("55555555-5555-5555-5555-555555555555"), 5.50m, false, "Rig-04 (maintenance)", "RTX 3080 / i7-12700 / 32GB" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Machines_Name",
                table: "Machines",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Records_MachineId",
                table: "Records",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_Records_UserId_StartedAt",
                table: "Records",
                columns: new[] { "UserId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Records");

            migrationBuilder.DropTable(
                name: "Machines");
        }
    }
}

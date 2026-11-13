using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class WelfareRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WelfareRates",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Percent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WelfareRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WelfareRates_StartDate",
                schema: "hr",
                table: "WelfareRates",
                column: "StartDate",
                unique: true);

            // UAT-43: the two ranges of แก้ครั้งที่ 5 — 1 ม.ค. 2570 – 30 ก.ย. 2574 = 0.25%, 1 ต.ค. 2574 เป็นต้นไป = 0.50%. Inserted once
            // here (not HasData), so HR's later edits are never undone by a migration.
            var seeded = new DateTimeOffset(2026, 11, 13, 0, 0, 0, TimeSpan.FromHours(7));
            migrationBuilder.InsertData(
                schema: "hr",
                table: "WelfareRates",
                columns: ["StartDate", "EndDate", "Percent", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"],
                values: new object[,]
                {
                    { new DateOnly(2027, 1, 1), new DateOnly(2031, 9, 30), 0.25m, "system", seeded, "system", seeded },
                    { new DateOnly(2031, 10, 1), null, 0.50m, "system", seeded, "system", seeded },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WelfareRates",
                schema: "hr");
        }
    }
}

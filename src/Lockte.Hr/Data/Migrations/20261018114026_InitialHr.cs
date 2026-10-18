using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialHr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hr");

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EnglishTitle = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    EnglishName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Sex = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NationalId = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    SocialSecurityNo = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    IdCardPlace = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IdCardIssued = table.Column<DateOnly>(type: "date", nullable: true),
                    RegisteredAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CurrentAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ListNo = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Education = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    Position = table.Column<string>(type: "nvarchar(18)", maxLength: 18, nullable: true),
                    Duty = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    EmployeeType = table.Column<int>(type: "int", nullable: true),
                    Salary = table.Column<double>(type: "float", nullable: false),
                    DailyWage = table.Column<double>(type: "float", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    HotAllowance = table.Column<bool>(type: "bit", nullable: false),
                    BankAccountTtb = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    BankAccountKbank = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Hospital = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ResignDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Memo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Code",
                schema: "hr",
                table: "Employees",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                schema: "hr",
                table: "Employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Name",
                schema: "hr",
                table: "Employees",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ShiftId",
                schema: "hr",
                table: "Employees",
                column: "ShiftId");

            // แผนก and กะ live in module องค์กร (ADR 0002: foreign keys across schemas are allowed; HrDbContext does not map org).
            migrationBuilder.Sql("""
                ALTER TABLE hr.Employees ADD CONSTRAINT FK_Employees_org_Departments_DepartmentId
                    FOREIGN KEY (DepartmentId) REFERENCES org.Departments (Id);
                ALTER TABLE hr.Employees ADD CONSTRAINT FK_Employees_org_Shifts_ShiftId
                    FOREIGN KEY (ShiftId) REFERENCES org.Shifts (Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Employees",
                schema: "hr");
        }
    }
}

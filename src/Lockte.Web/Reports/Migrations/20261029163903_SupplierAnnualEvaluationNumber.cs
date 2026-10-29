using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Web.Reports.Migrations
{
    /// <inheritdoc />
    public partial class SupplierAnnualEvaluationNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // เลขเอกสารควบคุมของ แบบประเมินผู้ขายประจำปี (RPT-08, วัตถุดิบ #255) as the sample prints it.
            migrationBuilder.Sql("""
                INSERT INTO report.DocumentNumbers ([Key], Name, Code, Edition, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                SELECT v.[Key], v.Name, v.Code, v.Edition, 'system', SYSDATETIMEOFFSET(), 'system', SYSDATETIMEOFFSET()
                FROM (VALUES (N'RPT-08-P', N'แบบประเมินผู้ขายประจำปี', N'PC-PU001-4/2547', N' ฉบับที่ 1')) AS v([Key], Name, Code, Edition)
                WHERE NOT EXISTS (SELECT 1 FROM report.DocumentNumbers d WHERE d.[Key] = v.[Key]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM report.DocumentNumbers WHERE [Key] = N'RPT-08-P';");
        }
    }
}

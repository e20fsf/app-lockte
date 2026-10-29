using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Web.Reports.Migrations
{
    /// <inheritdoc />
    public partial class ApprovedSupplierNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // เลขเอกสารควบคุมของ ทะเบียนผู้ขาย (Approved Supplier List, RPT-32) — วัตถุดิบ #264.
            migrationBuilder.Sql("""
                INSERT INTO report.DocumentNumbers ([Key], Name, Code, Edition, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                SELECT v.[Key], v.Name, v.Code, v.Edition, 'system', SYSDATETIMEOFFSET(), 'system', SYSDATETIMEOFFSET()
                FROM (VALUES (N'RPT-32-P', N'ทะเบียนผู้ขาย (Approved Supplier List)', N'PC-PU001-3/2547', N' ฉบับที่ 1')) AS v([Key], Name, Code, Edition)
                WHERE NOT EXISTS (SELECT 1 FROM report.DocumentNumbers d WHERE d.[Key] = v.[Key]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM report.DocumentNumbers WHERE [Key] = N'RPT-32-P';");
        }
    }
}

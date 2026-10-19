using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Web.Reports.Migrations
{
    /// <inheritdoc />
    public partial class InitialReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "report");

            migrationBuilder.CreateTable(
                name: "DocumentNumbers",
                schema: "report",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Edition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentNumbers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentNumbers_Key",
                schema: "report",
                table: "DocumentNumbers",
                column: "Key",
                unique: true);

            // เลขเอกสารควบคุม as the legacy printed them (#40: character for character), checked against the sample PDFs:
            // RPT-56-3 prints PD-, not PC-, and the spaces before ฉบับที่ differ between forms.
            migrationBuilder.Sql("""
                INSERT INTO report.DocumentNumbers ([Key], Name, Code, Edition, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                SELECT v.[Key], v.Name, v.Code, v.Edition, 'system', SYSDATETIMEOFFSET(), 'system', SYSDATETIMEOFFSET()
                FROM (VALUES
                    (N'RPT-22-1', N'ใบขอบรรจุเป็นพนักงานประจำ', N'PC-PE001-7/2547', N' ฉบับที่2'),
                    (N'RPT-22-2', N'ใบนำส่งเอกสารแผนกบุคคลและธุรการ', N'PC-PE001-8/2547', N'ฉบับที่3'),
                    (N'RPT-22-3', N'ใบรายงานการประเมินผลงานพนักงานทดลองงาน', N'PC-PE001-6/2547', N'  ฉบับที่2'),
                    (N'RPT-43-D', N'ใบประเมินประจำปีรายวัน', N'WI-PE013-3/2548', N' ฉบับที่2'),
                    (N'RPT-43-M', N'ใบประเมินประจำปีรายเดือน', N'WI-PE013-2/2548', N' ฉบับที่2'),
                    (N'RPT-50', N'ใบลางาน', N'WI-PE-004-1/2547', N' ฉบับที่ 5'),
                    (N'RPT-51', N'ทะเบียนผู้ประกันตน', N'สปส.6-07', N''),
                    (N'RPT-53-W', N'หนังสือรับรองการทำงาน', N'WI-PE006-2/2547', N' ฉบับที่ 1'),
                    (N'RPT-53-S', N'หนังสือรับรองเงินเดือน', N'WI-PE003-1/2547', N' ฉบับที่ 1'),
                    (N'RPT-55', N'ใบลาออก', N'WI-PE006-1/2547', N' ฉบับที่ 1'),
                    (N'RPT-56-1', N'แบบฟอร์มการรับพนักงานเข้าทดลองงาน', N'PC-PE001-5/2547', N' ฉบับที่2'),
                    (N'RPT-56-2', N'สัญญาจ้างแรงงานรายวัน', N'PC-PE001-9/2547', N'ฉบับที่1'),
                    (N'RPT-56-3', N'สัญญาจ้างแรงงานรายเดือน', N'PD-PE001-9/2547', N'ฉบับที่1'),
                    (N'RPT-57', N'แบบ คร.2', N'แบบ คร.2', N'')
                ) AS v([Key], Name, Code, Edition)
                WHERE NOT EXISTS (SELECT 1 FROM report.DocumentNumbers d WHERE d.[Key] = v.[Key]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentNumbers",
                schema: "report");
        }
    }
}

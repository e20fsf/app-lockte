using Lockte.Hr.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Hr.Data.Migrations
{
    /// <summary>
    /// One-time data step (UAT 7/8, แบบ คร.2): the server's settings still printed the old phone "(662) 233-6002-3 และ (662) 233-5635"
    /// (two numbers overlapping in the box) and a blank ถนน of the สถานที่ทำงาน, hiding the defaults of #462. The addresses HR saves on
    /// the settings page (UAT-38) win over the server's, so the right ones are saved there on upgrade — nothing to run on the server.
    /// <list type="bullet">
    /// <item>An address never saved gets the company's whole address with โทรศัพท์ 02-2336002-3 / ถนน พุทธมณฑลสาย 5.</item>
    /// <item>An address HR already saved keeps every field; only a blank or two-number office phone and a blank workplace road are set.</item>
    /// </list>
    /// Running it again changes nothing. HR can still change every field on the settings page afterwards.
    /// </summary>
    [DbContext(typeof(HrDbContext))]
    [Migration("20261118090000_Kor2EmployerAddresses")]
    public partial class Kor2EmployerAddresses : Migration
    {
        public const string OfficePhone = "02-2336002-3";
        public const string WorkplaceRoad = "พุทธมณฑลสาย 5";

        public const string FixSql = """
            IF NOT EXISTS (SELECT 1 FROM hr.FormSigners)
                INSERT INTO hr.FormSigners (CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                VALUES (N'system', SYSDATETIMEOFFSET(), N'system', SYSDATETIMEOFFSET());

            UPDATE hr.FormSigners SET
                OfficeHouseNo = N'97', OfficeMoo = NULL, OfficeSoi = N'สีลม 9', OfficeRoad = N'สีลม', OfficeSubdistrict = N'สีลม',
                OfficeDistrict = N'บางรัก', OfficeProvince = N'กรุงเทพฯ', OfficePhone = N'02-2336002-3',
                UpdatedBy = N'system', UpdatedAt = SYSDATETIMEOFFSET()
            WHERE COALESCE(OfficeHouseNo, OfficeMoo, OfficeSoi, OfficeRoad, OfficeSubdistrict, OfficeDistrict, OfficeProvince, OfficePhone) IS NULL;

            UPDATE hr.FormSigners SET OfficePhone = N'02-2336002-3', UpdatedBy = N'system', UpdatedAt = SYSDATETIMEOFFSET()
            WHERE OfficePhone IS NULL OR OfficePhone LIKE N'%และ%';

            UPDATE hr.FormSigners SET
                WorkplaceHouseNo = N'85/36', WorkplaceMoo = NULL, WorkplaceSoi = N'สุขาภิบาล 2', WorkplaceRoad = N'พุทธมณฑลสาย 5',
                WorkplaceSubdistrict = N'อ้อมน้อย', WorkplaceDistrict = N'กระทุ่มแบน', WorkplaceProvince = N'สมุทรสาคร', WorkplacePhone = N'024204554',
                UpdatedBy = N'system', UpdatedAt = SYSDATETIMEOFFSET()
            WHERE COALESCE(WorkplaceHouseNo, WorkplaceMoo, WorkplaceSoi, WorkplaceRoad, WorkplaceSubdistrict, WorkplaceDistrict, WorkplaceProvince,
                WorkplacePhone) IS NULL;

            UPDATE hr.FormSigners SET WorkplaceRoad = N'พุทธมณฑลสาย 5', UpdatedBy = N'system', UpdatedAt = SYSDATETIMEOFFSET()
            WHERE WorkplaceRoad IS NULL;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(FixSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not undone: HR may have changed the addresses since; change them on the settings page.
        }
    }
}

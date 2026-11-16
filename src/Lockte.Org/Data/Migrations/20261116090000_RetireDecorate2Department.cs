using Lockte.Org.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Org.Data.Migrations
{
    /// <summary>
    /// One-time data step (UAT7-7, #472): แผนก ตกแต่ง 2 is no longer used, so it is retired (เลิกใช้) on upgrade instead of by
    /// hand on the server. Nothing is deleted, no employee is moved, no history is touched. The screens carry no rule about
    /// this name: they only hide whatever is retired.
    /// </summary>
    [DbContext(typeof(OrgDbContext))]
    [Migration("20261116090000_RetireDecorate2Department")]
    public partial class RetireDecorate2Department : Migration
    {
        /// <summary>Spaces are ignored, so "ตกแต่ง 2" and "ตกแต่ง2" both match; running it again changes nothing.</summary>
        public const string RetireSql =
            "UPDATE org.Departments SET IsActive = 0 WHERE IsActive = 1 AND REPLACE(LTRIM(RTRIM(Name)), N' ', N'') = N'ตกแต่ง2';";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(RetireSql);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not undone: the department may have been retired by hand before; bring it back from the register page.
        }
    }
}

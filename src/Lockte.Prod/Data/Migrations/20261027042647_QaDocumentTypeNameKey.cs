using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Prod.Data.Migrations
{
    /// <inheritdoc />
    public partial class QaDocumentTypeNameKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QaDocumentTypes_Code_Revision",
                schema: "prod",
                table: "QaDocumentTypes");

            migrationBuilder.CreateIndex(
                name: "IX_QaDocumentTypes_Code_Revision_Name",
                schema: "prod",
                table: "QaDocumentTypes",
                columns: new[] { "Code", "Revision", "Name" },
                unique: true,
                filter: "[Revision] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QaDocumentTypes_Code_Revision_Name",
                schema: "prod",
                table: "QaDocumentTypes");

            migrationBuilder.CreateIndex(
                name: "IX_QaDocumentTypes_Code_Revision",
                schema: "prod",
                table: "QaDocumentTypes",
                columns: new[] { "Code", "Revision" },
                unique: true,
                filter: "[Revision] IS NOT NULL");
        }
    }
}

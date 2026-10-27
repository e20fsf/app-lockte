using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lockte.Prod.Data.Migrations
{
    /// <inheritdoc />
    public partial class QaSpecs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QaDocumentTypes",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Revision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepartmentStep = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SampleUnit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ResultColumns = table.Column<byte>(type: "tinyint", nullable: false),
                    AttributeRanges = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PageLayout = table.Column<byte>(type: "tinyint", nullable: false),
                    Paper = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QaDocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QaSampleSizes",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UpTo = table.Column<int>(type: "int", nullable: true),
                    SampleSize = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QaSampleSizes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QaModelSpecs",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Models = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DocumentSequence = table.Column<int>(type: "int", nullable: true),
                    ImageData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ImageContentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QaModelSpecs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QaModelSpecs_QaDocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalSchema: "prod",
                        principalTable: "QaDocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QaDefects",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QaModelSpecId = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QaDefects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QaDefects_QaModelSpecs_QaModelSpecId",
                        column: x => x.QaModelSpecId,
                        principalSchema: "prod",
                        principalTable: "QaModelSpecs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QaMeasurePoints",
                schema: "prod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QaModelSpecId = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    PointNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Standard = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QaMeasurePoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QaMeasurePoints_QaModelSpecs_QaModelSpecId",
                        column: x => x.QaModelSpecId,
                        principalSchema: "prod",
                        principalTable: "QaModelSpecs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QaDefects_QaModelSpecId_Sequence",
                schema: "prod",
                table: "QaDefects",
                columns: new[] { "QaModelSpecId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QaDocumentTypes_Code_Revision",
                schema: "prod",
                table: "QaDocumentTypes",
                columns: new[] { "Code", "Revision" },
                unique: true,
                filter: "[Revision] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QaMeasurePoints_QaModelSpecId_Sequence",
                schema: "prod",
                table: "QaMeasurePoints",
                columns: new[] { "QaModelSpecId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QaModelSpecs_DocumentTypeId_Name",
                schema: "prod",
                table: "QaModelSpecs",
                columns: new[] { "DocumentTypeId", "Name" },
                unique: true);

            // จำนวนสุ่มตามขนาด Lot (BR-017): the table of the legacy, which the QA may edit.
            migrationBuilder.Sql("""
                INSERT INTO prod.QaSampleSizes (UpTo, SampleSize, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                SELECT v.UpTo, v.SampleSize, 'system', SYSDATETIMEOFFSET(), 'system', SYSDATETIMEOFFSET()
                FROM (VALUES (50, 5), (150, 20), (280, 32), (500, 50), (1200, 80), (3200, 125), (10000, 200), (35000, 315), (NULL, 500)) AS v(UpTo, SampleSize);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QaDefects",
                schema: "prod");

            migrationBuilder.DropTable(
                name: "QaMeasurePoints",
                schema: "prod");

            migrationBuilder.DropTable(
                name: "QaSampleSizes",
                schema: "prod");

            migrationBuilder.DropTable(
                name: "QaModelSpecs",
                schema: "prod");

            migrationBuilder.DropTable(
                name: "QaDocumentTypes",
                schema: "prod");
        }
    }
}

using System.Runtime.Versioning;
using System.Security.Cryptography;
using Lockte.Migration;
using Lockte.Org;
using Lockte.Purch;

namespace Lockte.Web.Tests;

/// <summary>
/// The master data of a made-up <c>วัตถุดิบ.accdb</c> (#217) with the legacy column types (data-model.md). Every name, code and number in it is fictional — the repo is public.
/// </summary>
[SupportedOSPlatform("windows")]
public static class FakePurchFile
{
    public static Dictionary<string, (string Column, string Type)[]> Schema { get; } = new()
    {
        ["SP"] =
        [
            ("CODE", "TEXT(255)"), ("NAME", "TEXT(255)"), ("ADDRESS1", "TEXT(255)"), ("ADDRESS2", "TEXT(255)"), ("TEL_1", "TEXT(255)"), ("TEL_2", "TEXT(255)"), ("FAX_NO", "TEXT(255)"),
            ("MG", "TEXT(255)"), ("CREDIT", "TEXT(50)"), ("VAT", "TEXT(50)"), ("PRODUCT", "TEXT(255)"), ("BUSINESS", "TEXT(255)"), ("CERTIFICATE", "TEXT(50)"), ("USE IN PRODUCT", "TEXT(50)"),
        ],
        ["MTR_NAME"] =
        [
            ("CODE", "TEXT(6)"), ("NAME", "TEXT(255)"), ("U_NAME", "TEXT(12)"), ("MINIMUM", "DOUBLE"), ("COST", "DOUBLE"), ("SUP_CODE", "TEXT(50)"), ("SUP_NAME", "TEXT(255)"),
            ("INCOMING", "TEXT(50)"), ("BUY", "SINGLE"),
        ],
        ["รายชื่อแผนก"] = [("department", "TEXT(50)"), ("CODE", "TEXT(50)"), ("CHIP", "TEXT(255)")],
        ["PURC"] = [("ID", "TEXT(50)"), ("NAME", "TEXT(50)")],
        ["CREDIT"] = [("CREDIT", "TEXT(50)")],
        ["ประเภทธุรกิจ"] = [("BUSINESS", "TEXT(255)")],
        ["เหตุผลในการขอซื้อ"] = [("REMARK", "TEXT(255)")],
        ["หมายเหตุ"] = [("MEM", "TEXT(255)")],
        ["รหัสตัวแรกของวัตถุดิบ"] = [("ID", "TEXT(50)"), ("NAME", "TEXT(255)")],
    };

    public static FakeLegacyFile Empty() => new(Schema);
}

/// <summary>เครื่องมือย้ายข้อมูล: ข้อมูลหลักของวัตถุดิบ (#217) — a made-up file in, the registers of module วัตถุดิบ out, plus the DQ report and the row counts.</summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class PurchMasterMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-purch-migration-test-");

    public void Dispose() => work.Delete(recursive: true);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private Task<MigrationReport> RunAsync(string source, string? codeMap = null, string output = "out")
        => PurchMigrationTool.RunAsync(db.Services, source, codeMap, Path.Combine(work.FullName, output));

    private string Save(FakeLegacyFile file) => file.Save(Path.Combine(work.FullName, "วัตถุดิบ.accdb"));

    private static string Unique() => Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    [Fact]
    public async Task Loads_the_lists_suppliers_and_materials_from_a_copy_and_reports_what_it_cannot()
    {
        // the test database is shared with the other migration tests: the documents of other tests would refuse a reload, so this one runs on a database without any
        var tag = Unique();
        var department = (await AsAsync(async s =>
        {
            var org = s.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, "ฝ่าย" + tag, 1, null))).Id!.Value;
            return (await org.SaveDepartmentAsync(new DepartmentInput(null, "P" + tag, "แผนก" + tag, "p", 3, section, null, [], null))).Id!.Value;
        }));

        var file = FakePurchFile.Empty()
            .Row("CREDIT", ("CREDIT", "30")).Row("CREDIT", ("CREDIT", "เงินสด"))
            .Row("ประเภทธุรกิจ", ("BUSINESS", "ผลิต (manufacturing)"))
            .Row("PURC", ("ID", "F"), ("NAME", "โรงงาน"))
            .Row("เหตุผลในการขอซื้อ", ("REMARK", "เหตุผลสมมุติ")).Row("เหตุผลในการขอซื้อ", ("REMARK", "เหตุผลสมมุติ"))
            .Row("หมายเหตุ", ("MEM", "หมายเหตุสมมุติ"))
            .Row("รหัสตัวแรกของวัตถุดิบ", ("ID", "R*")).Row("รหัสตัวแรกของวัตถุดิบ", ("ID", "E0*"))
            .Row("รายชื่อแผนก", ("department", "แผนก" + tag), ("CODE", "ZQ6*"), ("CHIP", "หัวหน้าสมมุติ"))
            .Row("รายชื่อแผนก", ("department", "แผนกที่ไม่มีในทะเบียน"), ("CODE", "Y*"))
            .Row("SP", ("CODE", "xs001"), ("NAME", "ผู้ขายสมมุติหนึ่ง"), ("CREDIT", "30"), ("VAT", "YES"), ("BUSINESS", "ผลิต (manufacturing)"), ("CERTIFICATE", "มี"), ("USE IN PRODUCT", "ใช่"))
            .Row("SP", ("CODE", "XS002"), ("NAME", "ผู้ขายสมมุติสอง"), ("CREDIT", "เงินสด"), ("VAT", "NO"), ("USE IN PRODUCT", "ไม่ใช่"))
            .Row("SP", ("CODE", "XS003"), ("NAME", "ผู้ขายสมมุติสาม"), ("CREDIT", "ตามตกลง"), ("BUSINESS", "อุปกรณ์ไฟฟ้า")) // credit in words, a business type outside the list, VAT blank
            .Row("SP", ("CODE", "XS001"), ("NAME", "ผู้ขายซ้ำรหัส"))          // a repeated code: never merged by the tool (Q-10)
            .Row("SP", ("CODE", "XS004"), ("NAME", null))                    // no name
            .Row("MTR_NAME", ("CODE", "R0001"), ("NAME", "วัตถุดิบสมมุติหนึ่ง"), ("U_NAME", "กก."), ("COST", 12.5), ("SUP_CODE", "XS001"), ("INCOMING", "ใช่"), ("BUY", 0f))
            .Row("MTR_NAME", ("CODE", "E0001"), ("NAME", "วัตถุดิบสมมุติหนึ่ง"), ("U_NAME", "ชิ้น"), ("COST", 3.0), ("INCOMING", "ไม่ใช่"))          // the same name, another code
            .Row("MTR_NAME", ("CODE", "QA"), ("NAME", "รหัสผิดรูปแบบ"))                                                                          // malformed code
            .Row("MTR_NAME", ("CODE", "R0002"), ("NAME", null))                                                                                  // no name
            .Row("MTR_NAME", ("CODE", "R0003"), ("NAME", "ผู้ขายล่าสุดไม่มี"), ("SUP_CODE", "XS999"), ("INCOMING", "ใช่"));                      // supplier not in the register
        var source = Save(file);
        var before = (Hash: SHA256.HashData(File.ReadAllBytes(source)), Written: File.GetLastWriteTimeUtc(source));
        var map = Path.Combine(work.FullName, "map.csv");
        File.WriteAllLines(map, ["kind,legacy,code,name,prefix", $"department,แผนก{tag},P{tag},,"]);

        var report = await RunAsync(source, map);

        Assert.Equal(before.Hash, SHA256.HashData(File.ReadAllBytes(source))); // the source is read through a copy
        Assert.Equal(before.Written, File.GetLastWriteTimeUtc(source));
        Assert.True(report.Balanced, string.Join("; ", report.Tables.Where(t => !t.Balanced).Select(t => $"{t.LegacyTable} {t.SourceRows}/{t.Excluded}/{t.Rejected}/{t.Loaded}/{t.InDatabase}")));

        var suppliers = await AsAsync(s => s.Get<SupplierDirectory>().GetAllAsync());
        Assert.Equal(["XS001", "XS002", "XS003"], suppliers.Select(x => x.Code));
        Assert.Equal("ผู้ขายสมมุติหนึ่ง", suppliers[0].Name);                  // the first row of the repeated code
        Assert.Equal((30, true, true, true), (suppliers[0].CreditDays, suppliers[0].Vat, suppliers[0].Certificate, suppliers[0].UsedInProduction));
        Assert.Equal((0, false), (suppliers[1].CreditDays, suppliers[1].Vat));
        Assert.Equal((null, true), (suppliers[2].CreditDays, suppliers[2].Vat));  // words are no days; a blank VAT charges VAT as the old system did
        Assert.True((await AsAsync(s => s.Get<SupplierDirectory>().GetBusinessTypesAsync())).Contains("อุปกรณ์ไฟฟ้า"));

        var materials = await AsAsync(s => s.Get<MaterialDirectory>().GetAllAsync());
        Assert.Equal(["E0001", "R0001", "R0003"], materials.Select(x => x.Code));
        Assert.True(materials.Single(m => m.Code == "R0001").IsMain);
        Assert.Equal(suppliers[0].Id, materials.Single(m => m.Code == "R0001").LatestSupplierId);
        Assert.Null(materials.Single(m => m.Code == "R0003").LatestSupplierId);
        Assert.Equal(["E0", "R"], (await AsAsync(s => s.Get<MaterialDirectory>().GetGroupsAsync())).Order().ToList());

        Assert.Equal("ZQ", await AsAsync(s => s.Get<RequestSetupDirectory>().GetPrefixAsync(department)));
        Assert.Single(await AsAsync(s => s.Get<RequestSetupDirectory>().GetReasonsAsync())); // the repeated reason is loaded once
        Assert.Single(await AsAsync(s => s.Get<RequestSetupDirectory>().GetNotesAsync()));

        // the exceptions: codes and reasons only
        Assert.Equal(1, report.Exceptions.Count(e => e.Table == "SP" && e.Rule == "DQ-01"));
        Assert.Equal(1, report.Exceptions.Count(e => e.Table == "MTR_NAME" && e.Rule == "DQ-15" && e.Key == "QA"));
        Assert.Equal(1, report.Exceptions.Count(e => e.Table == "MTR_NAME" && e.Rule == "DQ-02"));
        Assert.Equal(1, report.Exceptions.Count(e => e.Table == "รายชื่อแผนก" && e.Rule == "DQ-5"));
        Assert.DoesNotContain(report.Exceptions, e => e.Problem.Contains("ผู้ขายสมมุติ")); // the exceptions name the code and the reason, not the supplier
    }

    [Fact]
    public async Task Refuses_a_database_that_already_holds_purchasing_documents()
    {
        var tag = Unique();
        var supplier = (await AsAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, "DOC" + tag, "ผู้ขายมีเอกสาร", null)))).Id!.Value;
        var material = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, "D" + tag, "วัตถุดิบมีเอกสาร", null, "กก.")))).Id!.Value;
        await AsAsync(s => s.Get<StockLedger>().SaveAsync(new StockEntryInput(null, new DateOnly(2026, 1, 5), material, 5m, null)));

        try
        {
            var source = Save(FakePurchFile.Empty().Row("SP", ("CODE", "XS001"), ("NAME", "ผู้ขายสมมุติ")));
            await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(source));
            Assert.Contains(await AsAsync(s => s.Get<SupplierDirectory>().GetAllAsync()), x => x.Id == supplier); // nothing was deleted
        }
        finally
        {
            // the database is shared with the other migration tests: take the document away so that they can load again
            foreach (var entry in await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(material)))
                await AsAsync(s => s.Get<StockLedger>().DeleteAsync(entry.Id, entry.RowVersion));
        }
    }
}

/// <summary>The parts of the purch-master program that need no database.</summary>
public sealed class PurchMasterMigrationOptionsTests
{
    [Fact]
    public void The_prefix_of_a_pattern_loses_the_stars_and_the_digits_of_the_year()
    {
        Assert.Equal("D", PurchMasterDataMigration.Prefix("D6*"));
        Assert.Equal("MT", PurchMasterDataMigration.Prefix("MT*"));
        Assert.Equal("DM", PurchMasterDataMigration.Prefix(" dm* "));
        Assert.Null(PurchMasterDataMigration.Prefix("*"));
        Assert.Null(PurchMasterDataMigration.Prefix(null));
    }

    [Fact]
    public void The_command_line_knows_the_program()
    {
        var (options, error) = MigrationOptions.Parse(["--program", "purch-master", "--source", "a.accdb", "--connection", "Server=x;Database=d", "--confirm", "d", "--out", "o"]);
        Assert.Null(error);
        Assert.Equal(MigrationOptions.PurchMasterProgram, options!.Program);
    }
}

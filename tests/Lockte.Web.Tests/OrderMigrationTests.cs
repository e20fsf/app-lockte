using System.Runtime.Versioning;
using System.Security.Cryptography;
using Lockte.Hr;
using Lockte.Migration;
using Lockte.Org;
using Lockte.Prod;
using Lockte.Sales;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// เครื่องมือย้ายข้อมูล: ข้อมูลหลักของ ผลผลิตและORDER (#107) — a made-up <c>ผลผลิตและORDER.accdb</c> in, the registers of
/// modules ขายและส่งของ / ผลิต / องค์กร out, read back through their public services, plus the DQ report, the row counts and the
/// code map decisions that clear the exceptions on the next run.
/// </summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class OrderMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-order-migration-test-");

    public void Dispose() => work.Delete(recursive: true);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private Task<MigrationReport> RunAsync(string source, string? codeMap = null, string output = "out")
        => OrderMigrationTool.RunAsync(db.Services, source, codeMap, Path.Combine(work.FullName, output));

    private string WriteMap(string name, params string[] lines)
    {
        var path = Path.Combine(work.FullName, name);
        File.WriteAllLines(path, ["kind,legacy,code,name,prefix", .. lines]);
        return path;
    }

    private async Task<int> SavedAsync(Task<Lockte.SharedKernel.ServiceResult> save)
    {
        var result = await save;
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Id!.Value;
    }

    /// <summary>
    /// What the employee program's migration leaves: two production แผนก, two employees, and a customer it loaded (the tests of
    /// this collection share a database, so what an earlier test loaded into the sales registers is cleared first).
    /// </summary>
    private async Task SetUpEmployeeProgramDataAsync()
    {
        await AsAsync(async s =>
        {
            await s.Get<SalesDataReset>().DeleteAllAsync();
            var territories = s.Get<SalesTerritoryRegistry>();
            foreach (var t in await s.Get<SalesTerritoryDirectory>().GetAllAsync()) await territories.DeleteAsync(t.Id, t.RowVersion);

            var org = s.Get<OrgRegistry>();
            var directory = s.Get<OrgDirectory>();
            var section = (await directory.GetSectionsAsync()).FirstOrDefault(x => x.Name == "ผลิต ORD")?.Id
                ?? await SavedAsync(org.SaveSectionAsync(new SectionInput(null, "ผลิต ORD", 1, null)));
            if (await directory.FindDepartmentByCodeAsync("QD") is null)
                await SavedAsync(org.SaveDepartmentAsync(new DepartmentInput(null, "QD", "ไดคาสท์ ORD", "q", 1, section, null, [], null)));
            if (await directory.FindDepartmentByCodeAsync("QB") is null)
                await SavedAsync(org.SaveDepartmentAsync(new DepartmentInput(null, "QB", "อัดผ้าเบรก ORD", "q", 2, section, null, [], null)));
            var employees = s.Get<EmployeeRegistry>();
            var employeeDirectory = s.Get<EmployeeDirectory>();
            if (await employeeDirectory.FindByCodeAsync("Q101") is null)
                await SavedAsync(employees.SaveAsync(new EmployeeInput { Code = "Q101", Name = "นาย ขาย สมมุติหนึ่ง" }));
            if (await employeeDirectory.FindByCodeAsync("Q102") is null)
                await SavedAsync(employees.SaveAsync(new EmployeeInput { Code = "Q102", Name = "นางสาว ขาย สมมุติสอง" }));
            var customers = s.Get<CustomerRegistry>();
            await SavedAsync(customers.SaveAsync(new CustomerInput(null, "40-01-001", "ลูกค้ารับจ้างสมมุติ", null)));
            await SavedAsync(customers.SaveAsync(new CustomerInput(null, "40-99-999", "ลูกค้าที่ไม่อยู่ในไฟล์", null)));
            return 0;
        });
    }

    private static FakeLegacyFile Standard() => FakeOrderFile.Empty()
        .Row("เซล", ("ID", "00"), ("NAME", "สำนักงานสมมุติ"))
        .Row("เซล", ("ID", "08"), ("NAME", "นายขาย สมมุติหนึ่ง"))
        .Row("เซล", ("ID", "09"), ("NAME", "นางสาวขาย ไม่มีในทะเบียน"))
        .Row("เซล", ("ID", "11"), ("NAME", "เซลออนไลน์สมมุติ"))
        .Row("P_CUS", ("CODE", "08-01-001"), ("NAME", "ร้านสมมุติหนึ่ง"), ("CREDIT", "60 วัน"), ("ADDRESS", "1 หมู่ 2"), ("ADDRESS1", "เมืองสมมุติ"),
            ("PROVINCES", "จังหวัดสมมุติ"), ("P_CODE", "10000"), ("ADD1", "ขนส่งสมมุติ"), ("ADDDE", "ถนนสมมุติ"), ("ADDDE1", "02-000-0000"),
            ("TEL_1", "081-000-0001"), ("TEL_2", "081-000-0002"), ("SALE_ID", "08"), ("REMARK", "ร้านสมมุติหนึ่ง 1 หมู่ 2"))
        .Row("P_CUS", ("CODE", "08-01-002"), ("NAME", "ร้านสมมุติสอง"), ("CREDIT", "เงินสด"), ("SALE_ID", "08"))
        .Row("P_CUS", ("CODE", "08-01-003"), ("NAME", "ร้านสมมุติสาม"), ("CREDIT", "90 วันหลังวางบิล"), ("SALE_ID", "99"))
        .Row("P_CUS", ("CODE", "09-01-001"), ("NAME", "ร้านซ้ำเหมือนกัน"), ("CREDIT", "30"), ("SALE_ID", "09"))
        .Row("P_CUS", ("CODE", "09-01-001"), ("NAME", "ร้านซ้ำเหมือนกัน"), ("CREDIT", "30"), ("SALE_ID", "09"))
        .Row("P_CUS", ("CODE", "09-01-002"), ("NAME", "ร้านซ้ำ ก"), ("SALE_ID", "09"))
        .Row("P_CUS", ("CODE", "09-01-002"), ("NAME", "ร้านซ้ำ ข"), ("SALE_ID", "09"))
        .Row("P_CUS", ("CODE", "40-01-001"), ("NAME", "ลูกค้ารับจ้างสมมุติ (ชื่อใหม่)"), ("CREDIT", "เงินสด"), ("SALE_ID", "00"))
        .Row("P_CUS", ("NAME", "ไม่มีรหัสลูกค้า"))
        .Row("ประเภท", ("MEM", "ก้ามเบรกสมมุติ")).Row("ประเภท", ("MEM", "ผ้าเบรกสมมุติ"))
        .Row("TYPE", ("NANE", "ดรัมเบรก"), ("TYPE", "2")).Row("TYPE", ("NANE", "ดิสเบรก"), ("TYPE", "4"))
        .Row("MODEL", ("MODEL", "C100 A")).Row("MODEL", ("MODEL", "C100 B"))
        .Row("รหัสสินค้าย่อ", ("C", "G1*"), ("N", "ก้ามเบรก")).Row("รหัสสินค้าย่อ", ("C", "*"), ("N", "ทั้งหมด"))
        .Row("แผนก", ("DEP", "ไดคาสท์ ORD"), ("DEPA", "Q*")).Row("แผนก", ("DEP", "อัดผ้าเบรค ORD"), ("DEPA", "Q*"))
        .Row("MC_N", ("CODE", "D01"), ("NAME", "เครื่องฉีดสมมุติ"), ("DEP", "ไดคาสท์ ORD"))
        .Row("MC_N", ("CODE", "A01"), ("NAME", "เครื่องอัดสมมุติ"), ("DEP", "อัดผ้าเบรค ORD"))
        .Row("MC_N", ("CODE", "X99"), ("NAME", "เครื่องไม่มีแผนก"))
        .Row("รหัสพิมพ์", ("ID", "BF-001-1/1"), ("NAME", "แม่พิมพ์สมมุติหนึ่ง"), ("QTYS", 2), ("OWNER", "บจก.ล็อคเต้"), ("SHOT", 50000), ("DEP", "ไดคาสท์ ORD"),
            ("MARKER", "บจก.ล็อคเต้"), ("DATE", new DateTime(2020, 1, 1)), ("OUT_DATE", new DateTime(1899, 12, 30)), ("COST", 100000), ("AL WEIGHT", 12.5f))
        .Row("รหัสพิมพ์", ("ID", "BL-002-1/2"), ("NAME", "แม่พิมพ์สมมุติสอง"), ("DEP", "อัดผ้าเบรค ORD"), ("DATE", new DateTime(1899, 12, 30)), ("COST", 0))
        .Row("P_NAME", ("PD_C", "G1001"), ("PD_N", "ก้ามเบรกสมมุติ"), ("U_NAME", "คู่"), ("U_PACK", "กล่อง"), ("WEIGHT", 0.125), ("WEIGHT1", 0.2),
            ("U_PRICE", 99.5), ("FML", 40.25), ("MEM", "ก้ามเบรกสมมุติ"), ("MOULD", "BF-001-1/1"), ("CUS_ID", "40-01-001"), ("YEAR", new DateTime(2025, 6, 1)),
            ("MODEL", "C100 A"), ("BARCODE", "88593144001"), ("มอก", "มอก.0000"), ("TYPE", "2"), ("SIZE", "10x20x5"), ("FORMULA", "A"), ("MODEL1", "C100"))
        .Row("P_NAME", ("PD_C", "G1002"), ("PD_N", "สินค้าค่านอกรายการ"), ("MEM", "ประเภทที่ไม่มีในรายการ"), ("MODEL", "รุ่นที่ไม่มีในรายการ"),
            ("MOULD", "ZZ-NOPE"), ("CUS_ID", "99-99-999"), ("TYPE", "9"), ("YEAR", new DateTime(1899, 12, 30)))
        .Row("P_NAME", ("PD_C", "G1004"), ("PD_N", "สินค้าซ้ำ ก"), ("U_NAME", "คู่")).Row("P_NAME", ("PD_C", "G1004"), ("PD_N", "สินค้าซ้ำ ข"), ("U_NAME", "ชิ้น"))
        .Row("P_NAME", ("PD_N", "สินค้าไม่มีรหัส"))
        .Row("EMPO", ("ID", "Q101"), ("NAME", "ผลิตสมมุติหนึ่ง"))
        .Row("EMPO", ("ID", "T201"), ("NAME", "ผลิตสมมุติสอง"))
        .Row("EMPO", ("ID", "B179"), ("NAME", "ซ้ำ ก")).Row("EMPO", ("ID", "B179"), ("NAME", "ซ้ำ ข"));

    private string Save(FakeLegacyFile file, string name = "ผลผลิตและORDER.accdb") => file.Save(Path.Combine(work.FullName, name));

    private static TableCount Table(MigrationReport report, string legacyTable) => report.Tables.Single(t => t.LegacyTable == legacyTable);

    private static int Exceptions(MigrationReport report, string table, string? rule = null)
        => report.Exceptions.Count(e => e.Table == table && (rule is null || e.Rule == rule));

    [Fact]
    public async Task Loads_the_master_data_of_ORDER_from_a_copy_and_reports_what_it_cannot()
    {
        await SetUpEmployeeProgramDataAsync();
        var source = Save(Standard());
        var before = (Hash: SHA256.HashData(File.ReadAllBytes(source)), Written: File.GetLastWriteTimeUtc(source));

        var report = await RunAsync(source);

        // the source file is read through a copy and left as it was
        Assert.Equal(before.Hash, SHA256.HashData(File.ReadAllBytes(source)));
        Assert.Equal(before.Written, File.GetLastWriteTimeUtc(source));
        Assert.Equal("ผลผลิตและORDER.accdb", report.Source!.FileName);
        Assert.Equal(before.Written, report.Source.LastWriteUtc);
        Assert.True(report.Tables.All(t => t.Balanced), string.Join("; ", report.Tables.Where(t => !t.Balanced)));

        // เขตการขาย: 08 linked to the HR employee of the same name; the others loaded without one, and reported
        var territories = await AsAsync(s => s.Get<SalesTerritoryDirectory>().GetAllAsync());
        Assert.Equivalent(new[] { "00", "08", "09", "11" }, territories.Where(t => new[] { "00", "08", "09", "11" }.Contains(t.Code)).Select(t => t.Code));
        Assert.Equal(("Q101", "นาย ขาย สมมุติหนึ่ง"), (territories.Single(t => t.Code == "08").EmployeeCode, territories.Single(t => t.Code == "08").EmployeeName));
        Assert.Null(territories.Single(t => t.Code == "09").EmployeeId);
        Assert.Equal((4, 0, 3), (Table(report, "เซล").Loaded, Table(report, "เซล").Rejected, Exceptions(report, "เซล")));

        // ลูกค้า: credit as days; a customer the employee program loaded is matched by code and updated, not created twice
        var customers = await AsAsync(s => s.Get<CustomerDirectory>().GetAllAsync());
        Assert.Equal(1, customers.Count(c => c.Code == "40-01-001"));
        var matched = customers.Single(c => c.Code == "40-01-001");
        Assert.Equal(("ลูกค้ารับจ้างสมมุติ (ชื่อใหม่)", 0, "00"), (matched.Name, matched.CreditDays, matched.SalesTerritoryCode));
        var one = customers.Single(c => c.Code == "08-01-001");
        Assert.Equal((60, "1 หมู่ 2", "เมืองสมมุติ", "จังหวัดสมมุติ", "10000", "ขนส่งสมมุติ", "ถนนสมมุติ", "02-000-0000", "081-000-0001", "081-000-0002", "08"),
            (one.CreditDays, one.Address, one.District, one.Province, one.PostalCode, one.TransportName, one.TransportAddress, one.TransportPhone, one.Phone1, one.Phone2, one.SalesTerritoryCode));
        Assert.Equal(0, customers.Single(c => c.Code == "08-01-002").CreditDays);
        var three = customers.Single(c => c.Code == "08-01-003");
        Assert.Equal((null, null), (three.CreditDays, three.SalesTerritoryId)); // loaded blank, reported
        Assert.Equal(30, customers.Single(c => c.Code == "09-01-001").CreditDays); // identical copies: one
        Assert.DoesNotContain(customers, c => c.Code == "09-01-002"); // copies that differ: none, until decided
        var customerCount = Table(report, "P_CUS");
        Assert.Equal((9, 5, 5), (customerCount.SourceRows, customerCount.Loaded, customerCount.InDatabase));
        Assert.Equal(1 + 2 + 1, customerCount.Rejected); // blank code, 2 copies that differ, 1 identical copy
        Assert.Contains(report.Exceptions, e => e.Table == "P_CUS" && e.Key == "40-99-999" && e.Action == "คงไว้ในฐานข้อมูลเหมือนเดิม");
        Assert.Contains(report.Exceptions, e => e.Table == "P_CUS" && e.Key == "08-01-003" && e.Problem.Contains("90 วันหลังวางบิล"));
        Assert.Contains(report.Notes, n => n.Contains("1 รหัสมีในทะเบียนอยู่แล้ว"));

        // รายการเลือก: the file's lists, and the values the products use that it lacks, added and reported
        Assert.Equal(new[] { "ก้ามเบรกสมมุติ", "ประเภทที่ไม่มีในรายการ", "ผ้าเบรกสมมุติ" }.Order(), (await AsAsync(s => s.Get<ProductDirectory>().GetProductTypesAsync())).Select(t => t.Name).Order());
        Assert.Equal(new[] { ("2", "ดรัมเบรก"), ("4", "ดิสเบรก") }, (await AsAsync(s => s.Get<ProductDirectory>().GetBrakeTypesAsync())).Select(t => (t.Code, t.Name)));
        Assert.Equal(new[] { "*", "G1*" }, (await AsAsync(s => s.Get<ProductDirectory>().GetGroupsAsync())).Select(g => g.Pattern));
        Assert.Equal((2, 3), (Table(report, "ประเภท").Loaded, (await AsAsync(s => s.Get<ProductDirectory>().GetProductTypesAsync())).Count));
        Assert.Equivalent(new[] { ("ประเภท", "ประเภทที่ไม่มีในรายการ"), ("รุ่น", "รุ่นที่ไม่มีในรายการ") }, report.ListAdditions);
        Assert.Contains(report.Notes, n => n.Contains("รายการเลือกรุ่น: เพิ่ม 1 ค่า"));
        Assert.True(File.Exists(Path.Combine(work.FullName, "out", MigrationReport.ListAdditionsFile)));

        // แม่พิมพ์: the department by name (or reported), the Access zero date is no date
        var molds = await AsAsync(s => s.Get<MoldDirectory>().GetAllAsync());
        var bf = molds.Single(m => m.Code == "BF-001-1/1");
        Assert.Equal((2, 50000, new DateOnly(2020, 1, 1), (DateOnly?)null, 100000m, 12.5m, "บจก.ล็อคเต้"), (bf.PiecesPerShot, bf.MaintenanceShots, bf.StartDate, bf.EndDate, bf.Cost, bf.AluminumWeight, bf.Owner));
        Assert.NotNull(bf.DepartmentId);
        var bl = molds.Single(m => m.Code == "BL-002-1/2");
        Assert.Equal((null, null), (bl.DepartmentId, bl.StartDate)); // 'อัดผ้าเบรค ORD' is spelled differently: blank until mapped
        Assert.Equal((2, 0, 2), (Table(report, "รหัสพิมพ์").Loaded, Table(report, "รหัสพิมพ์").Rejected, Table(report, "รหัสพิมพ์").InDatabase));
        Assert.Contains(report.Exceptions, e => e.Table == "รหัสพิมพ์" && e.Key == "BL-002-1/2" && e.Rule == "DQ-5");

        // เครื่องจักร: needs a department of the register — the two without one wait for the code map
        var machines = await AsAsync(s => s.Get<OrgDirectory>().GetMachinesAsync());
        Assert.Equal(new[] { "D01" }, machines.Where(m => new[] { "D01", "A01", "X99" }.Contains(m.Code)).Select(m => m.Code));
        Assert.Equal((1, 2), (Table(report, "MC_N").Loaded, Table(report, "MC_N").Rejected));
        Assert.Equal(1, Exceptions(report, "แผนก"));

        // สินค้า: every column, the lists/mold/customer by their codes; what is not found is blank and reported
        var g1001 = (await AsAsync(s => s.Get<ProductDirectory>().FindByCodeAsync("G1001")))!;
        Assert.Equal(("ก้ามเบรกสมมุติ", "คู่", 0.125m, 40.25m, 99.5m, "BF-001-1/1", "40-01-001", new DateOnly(2025, 6, 1), "C100 A", "ดรัมเบรก", "มอก.0000"),
            (g1001.Name, g1001.Unit, g1001.AluminumWeight, g1001.UnitCost, g1001.SellingPrice, g1001.MoldCode, g1001.CustomerCode, g1001.ProducedOn, g1001.ModelName, g1001.BrakeTypeName, g1001.StandardNumber));
        var g1002 = (await AsAsync(s => s.Get<ProductDirectory>().FindByCodeAsync("G1002")))!;
        Assert.Equal((null, null, null, null), (g1002.MoldId, g1002.CustomerId, g1002.BrakeTypeId, g1002.ProducedOn));
        Assert.Equal("ประเภทที่ไม่มีในรายการ", g1002.ProductTypeName);
        Assert.Null(await AsAsync(s => s.Get<ProductDirectory>().FindByCodeAsync("G1004")));
        var productCount = Table(report, "P_NAME");
        Assert.Equal((5, 2, 3, 2), (productCount.SourceRows, productCount.Loaded, productCount.Rejected, productCount.InDatabase));

        // พนักงานผลิต: no register of their own — matched to HR by code, the rest reported, the codes only in the match file
        var empo = Table(report, "EMPO");
        Assert.Equal((4, 1, 3, 0), (empo.SourceRows, empo.Excluded, empo.Rejected, empo.Loaded));
        Assert.Contains(report.EmployeeMatches, m => m is { Kind: "production", LegacyCode: "Q101", HrCode: "Q101" });
        var matchFile = await File.ReadAllTextAsync(Path.Combine(work.FullName, "out", MigrationReport.EmployeeMatchFile));
        Assert.DoesNotContain("ผลิตสมมุติ", matchFile);
        Assert.True(File.Exists(Path.Combine(work.FullName, "out", MigrationReport.SourceFile)));

        // nothing the run loaded is in the change history (#39)
        await AsAsync(async s =>
        {
            var history = s.Get<HistoryService>();
            Assert.Empty(await history.SearchAsync(new(Table: "สินค้า", RowKey: "G1001")));
            Assert.Empty(await history.SearchAsync(new(Table: "แม่พิมพ์", RowKey: "BF-001-1/1")));
            Assert.Empty(await history.SearchAsync(new(Table: "ลูกค้า", RowKey: "08-01-001")));
            return 0;
        });
    }

    [Fact]
    public async Task Decisions_in_the_code_map_clear_the_exceptions_and_a_second_run_gives_the_same_data()
    {
        await SetUpEmployeeProgramDataAsync();
        var source = Save(Standard());
        var first = await RunAsync(source, output: "first");
        var map = WriteMap("decisions.csv",
            "department,อัดผ้าเบรค ORD,QB,,",
            "customer,09-01-002,,#2,",
            "sales-representative,00,-,,", "sales-representative,11,-,,", "sales-representative,09,Q102,,",
            "production-employee,T201,Q102,,", "production-employee,B179,Q101,ซ้ำ ข,");

        var second = await RunAsync(source, map, "second");
        var third = await RunAsync(source, map, "third");

        Assert.Equal(0, Exceptions(second, "เซล"));
        Assert.Equal(0, Exceptions(second, "แผนก"));
        Assert.Equal((3, 1, 1), (Table(second, "EMPO").Excluded, Table(second, "EMPO").Rejected, Exceptions(second, "EMPO"))); // the copy of B179 not chosen
        Assert.Equal((2, 1, 1), (Table(second, "MC_N").Loaded, Table(second, "MC_N").Rejected, Exceptions(second, "MC_N"))); // X99 has no department
        Assert.Equal(0, Exceptions(second, "รหัสพิมพ์", "DQ-5"));
        Assert.Equal((6, 3, 6), (Table(second, "P_CUS").Loaded, Table(second, "P_CUS").Rejected, Table(second, "P_CUS").InDatabase));
        Assert.True(second.Tables.All(t => t.Balanced));

        var customers = await AsAsync(s => s.Get<CustomerDirectory>().GetAllAsync());
        Assert.Equal("ร้านซ้ำ ข", customers.Single(c => c.Code == "09-01-002").Name);
        Assert.Equal(1, customers.Count(c => c.Code == "40-01-001"));
        var territories = await AsAsync(s => s.Get<SalesTerritoryDirectory>().GetAllAsync());
        Assert.Equal("Q102", territories.Single(t => t.Code == "09").EmployeeCode);
        Assert.Equal(new[] { "A01", "D01" }, (await AsAsync(s => s.Get<OrgDirectory>().GetMachinesAsync())).Where(m => new[] { "A01", "D01" }.Contains(m.Code)).Select(m => m.Code).Order());

        // the same file and map again: the same counts, nothing doubled
        Assert.Equal(second.Tables.Select(t => (t.LegacyTable, t.Loaded, t.Rejected, t.Excluded, t.InDatabase)),
            third.Tables.Select(t => (t.LegacyTable, t.Loaded, t.Rejected, t.Excluded, t.InDatabase)));
        Assert.Equal(second.Exceptions.Count, third.Exceptions.Count);
        Assert.Equal(2, (await AsAsync(s => s.Get<OrgDirectory>().GetMachinesAsync())).Count(m => new[] { "A01", "D01" }.Contains(m.Code)));
        Assert.Equal(2, (await AsAsync(s => s.Get<ProductDirectory>().SearchAsync(null, null, 1000))).Count);
    }

    [Fact]
    public void Command_line_takes_the_program_and_refuses_an_unknown_one()
    {
        string[] common = ["--source", "a.accdb", "--connection", "Server=x;Database=d", "--confirm", "d", "--out", "o"];
        Assert.Equal(MigrationOptions.EmployeeProgram, MigrationOptions.Parse(common).Options!.Program);
        Assert.Equal(MigrationOptions.OrderProgram, MigrationOptions.Parse([.. common, "--program", "order"]).Options!.Program);
        Assert.Contains("--program", MigrationOptions.Parse([.. common, "--program", "x"]).Error);
    }
}

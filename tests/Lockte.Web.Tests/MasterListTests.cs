using Lockte.Org;
using Lockte.Prod;
using Lockte.Sales;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;
using Microsoft.AspNetCore.Components;

namespace Lockte.Web.Tests;

/// <summary>
/// รายชื่อข้อมูลหลักของผลผลิตและORDER (RPT-21, #137): the rows and their order come from the module services, the pages
/// only lay them out (แบบกลาง B). Fictional data only.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class MasterListTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("report1");
        return await action(scope);
    }

    private async Task<int> SavedAsync(Task<Lockte.SharedKernel.ServiceResult> save)
    {
        var result = await save;
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Id!.Value;
    }

    /// <summary>The body with its character references decoded (Razor writes data text such as Thai as &amp;#xE27;…).</summary>
    private static async Task<string> HtmlAsync<TView>(object report, bool? inUseOnly = null) where TView : IComponent
    {
        var parameters = new Dictionary<string, object?> { ["Report"] = report };
        if (inUseOnly is { } flag) parameters["InUseOnly"] = flag;
        return System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<TView>(parameters));
    }

    [Fact]
    public async Task Customers_are_listed_by_code_with_address_credit_and_territory()
    {
        var prefix = Unique("94-");
        var territory = await AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, "L" + Guid.NewGuid().ToString("N")[..3], "เขตรายการ", null, null))));
        await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, prefix + "B", "ลูกค้า ข", null, CreditDays: 0))));
        await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, prefix + "A", "ลูกค้า ก", null, CreditDays: 60,
            Address: "1 หมู่ 2", District: "เมือง", Province: "จังหวัดทดสอบ", PostalCode: "10000", Phone1: "081", Phone2: "082", SalesTerritoryId: territory))));

        var list = await AsAsync(s => MasterListReports.CustomersAsync(s.Get<CustomerDirectory>()));

        var mine = list.Lines.Where(c => c.Code.StartsWith(prefix)).ToList();
        Assert.Equal([prefix + "A", prefix + "B"], mine.Select(c => c.Code));
        var html = await HtmlAsync<CustomerListView>(new MasterList<CustomerDto>(null, mine));
        Assert.Contains("1 หมู่ 2 เมือง 10000", html);
        Assert.Contains("60 วัน", html);
        Assert.Contains("เงินสด", html);
        Assert.Contains("081, 082", html);
        Assert.True(html.IndexOf("ลูกค้า ก", StringComparison.Ordinal) < html.IndexOf("ลูกค้า ข", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Products_are_listed_by_code_and_narrowed_by_code_pattern_or_customer_with_the_filter_in_the_head()
    {
        var customerA = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, Unique("40-"), "ลูกค้าสินค้า ก", null))));
        var customerB = await AsAsync(s => SavedAsync(s.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, Unique("40-"), "ลูกค้าสินค้า ข", null))));
        var tag = Unique("ZL");
        await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, tag + "2", "สินค้า สอง", null, Unit: "ชิ้น", CustomerId: customerB))));
        await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, tag + "1", "สินค้า หนึ่ง", null, Unit: "คู่", CustomerId: customerA))));
        await AsAsync(s => SavedAsync(s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, tag + "3", "สินค้า สาม ไม่มีลูกค้า", null))));

        var all = await AsAsync(s => MasterListReports.ProductsAsync(s.Get<ProductDirectory>(), s.Get<CustomerDirectory>(), tag + "*", null));
        var ofA = await AsAsync(s => MasterListReports.ProductsAsync(s.Get<ProductDirectory>(), s.Get<CustomerDirectory>(), null, customerA));

        Assert.Equal([tag + "1", tag + "2", tag + "3"], all.Lines.Select(p => p.Code));
        Assert.Equal($"รหัสสินค้า {tag}*", all.Filter);
        Assert.Equal([tag + "1"], ofA.Lines.Select(p => p.Code));
        Assert.StartsWith("ลูกค้า 40-", ofA.Filter);
        Assert.Contains("ลูกค้าสินค้า ก", ofA.Filter);
        var unfiltered = await AsAsync(s => MasterListReports.ProductsAsync(s.Get<ProductDirectory>(), s.Get<CustomerDirectory>(), null, null));
        Assert.Null(unfiltered.Filter);
        Assert.Contains(unfiltered.Lines, p => p.Code == tag + "3");
        var html = await HtmlAsync<ProductListView>(all);
        Assert.Contains($"รหัสสินค้า {tag}*", html);
        Assert.Equal(new[] { tag + "1", tag + "2", tag + "3" }.Select(c => html.IndexOf(c, StringComparison.Ordinal)).Order(), new[] { tag + "1", tag + "2", tag + "3" }.Select(c => html.IndexOf(c, StringComparison.Ordinal)));
        Assert.DoesNotContain("ราคา", html);
    }

    [Fact]
    public async Task The_mold_register_lists_every_mold_and_the_active_list_only_those_without_an_end_date()
    {
        var tag = Unique("LM-");
        var department = await AsAsync(async s =>
        {
            var org = s.Get<OrgRegistry>();
            var section = await SavedAsync(org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่ายรายการ"), 1, null)));
            return await SavedAsync(org.SaveDepartmentAsync(new DepartmentInput(null, Unique("L"), Unique("แผนกรายการ"), "l", 1, section, null, [], null)));
        });
        await AsAsync(s => SavedAsync(s.Get<MoldRegistry>().SaveAsync(new MoldInput(null, tag + "B", "แม่พิมพ์เลิกใช้", null, PiecesPerShot: 2,
            Maker: "ร้านสมมุติ", StartDate: new DateOnly(2020, 1, 1), EndDate: new DateOnly(2024, 12, 31), Owner: "บจก.ล็อคเต้"))));
        await AsAsync(s => SavedAsync(s.Get<MoldRegistry>().SaveAsync(new MoldInput(null, tag + "A", "แม่พิมพ์ใช้อยู่", null, PiecesPerShot: 4,
            MaintenanceShots: 50000, DepartmentId: department, StartDate: new DateOnly(2025, 3, 1), Owner: "ลูกค้าสมมุติ"))));
        await AsAsync(s => SavedAsync(s.Get<MoldRegistry>().SaveAsync(new MoldInput(null, tag + "C", "ใช้อยู่ไม่มีเกณฑ์", null))));

        var register = await AsAsync(s => MasterListReports.MoldsAsync(s.Get<MoldDirectory>(), inUseOnly: false));
        var active = await AsAsync(s => MasterListReports.MoldsAsync(s.Get<MoldDirectory>(), inUseOnly: true));

        Assert.Equal([tag + "A", tag + "B", tag + "C"], register.Lines.Where(m => m.Code.StartsWith(tag)).Select(m => m.Code));
        Assert.Equal([tag + "A", tag + "C"], active.Lines.Where(m => m.Code.StartsWith(tag)).Select(m => m.Code));
        Assert.StartsWith("แผนกรายการ", register.Lines.Single(m => m.Code == tag + "A").DepartmentName);
        var registerHtml = await HtmlAsync<MoldRegisterView>(new MasterList<MoldRegisterLine>(null, register.Lines.Where(m => m.Code.StartsWith(tag)).ToList()), inUseOnly: false);
        Assert.Contains("วันที่เลิกใช้งาน", registerHtml);
        Assert.Contains("31/12/2567", registerHtml);
        var activeHtml = await HtmlAsync<MoldRegisterView>(new MasterList<MoldRegisterLine>(null, active.Lines.Where(m => m.Code.StartsWith(tag)).ToList()), inUseOnly: true);
        Assert.Contains("การบำรุงรักษา", activeHtml);
        Assert.Contains("ทุก 50,000 SHOT", activeHtml);
        Assert.DoesNotContain("ทุก  SHOT", activeHtml); // the legacy printed a SHOT without a number when none was set (Q-23)
        Assert.DoesNotContain("วันที่เลิกใช้งาน", activeHtml);

        // the register's เลขเอกสารควบคุม is master data, edition and spacing as the sample prints it
        Assert.Equal("PC-DM001-1/2547 ฉบับที่1", await AsAsync(s => s.Get<DocumentNumberService>().PrintedTextAsync(DocumentKeys.MoldRegister)));
    }

    [Fact]
    public async Task Machines_and_territories_are_listed_by_code_with_their_department_and_sales_representative()
    {
        var code = "LL" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var department = await AsAsync(async s =>
        {
            var org = s.Get<OrgRegistry>();
            var section = await SavedAsync(org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่ายเครื่อง"), 1, null)));
            return await SavedAsync(org.SaveDepartmentAsync(new DepartmentInput(null, Unique("M"), Unique("แผนกเครื่อง"), "m", 1, section, null, [], null)));
        });
        await AsAsync(s => SavedAsync(s.Get<OrgRegistry>().SaveMachineAsync(new MachineInput(null, code + "2", "เครื่องสอง", department, null))));
        await AsAsync(s => SavedAsync(s.Get<OrgRegistry>().SaveMachineAsync(new MachineInput(null, code + "1", "เครื่องหนึ่ง", department, null))));
        var territoryCode = "T" + Guid.NewGuid().ToString("N")[..3];
        await AsAsync(s => SavedAsync(s.Get<SalesTerritoryRegistry>().SaveAsync(new SalesTerritoryInput(null, territoryCode, "เขตรายชื่อ", null, null))));

        var machines = await AsAsync(s => MasterListReports.MachinesAsync(s.Get<OrgDirectory>()));
        var territories = await AsAsync(s => MasterListReports.TerritoriesAsync(s.Get<SalesTerritoryDirectory>()));

        Assert.Equal([code + "1", code + "2"], machines.Lines.Where(m => m.Code.StartsWith(code)).Select(m => m.Code));
        Assert.Contains(territories.Lines, t => t.Code == territoryCode);
        var html = await HtmlAsync<MachineListView>(new MasterList<MachineDto>(null, machines.Lines.Where(m => m.Code.StartsWith(code)).ToList()));
        Assert.Contains("เครื่องหนึ่ง", html);
        Assert.Contains("แผนกเครื่อง", html);
    }

    [Fact] // D-6: a list without rows says so instead of printing an empty row
    public async Task Every_list_without_rows_says_there_is_no_data()
    {
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<CustomerListView>(new MasterList<CustomerDto>(null, [])));
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<ProductListView>(new MasterList<ProductListLine>("รหัสสินค้า ZZ*", [])));
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<MoldRegisterView>(new MasterList<MoldRegisterLine>(null, [])));
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<MachineListView>(new MasterList<MachineDto>(null, [])));
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<TerritoryListView>(new MasterList<SalesTerritoryDto>(null, [])));
        Assert.DoesNotContain("<tbody>", await HtmlAsync<CustomerListView>(new MasterList<CustomerDto>(null, [])));
    }
}

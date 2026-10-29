using Lockte.Org;
using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>RPT-01 ใบขออนุมัติสั่งซื้อ (#220): the data handed to the report, the number printed top right, and the page body.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class PurchRequestReportTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsBuyerAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    private async Task<(int Id, PurchaseRequestDto Request)> NewRequestAsync(int items)
    {
        int department;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
            department = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนกทดสอบ"), "d", 3, section, null, [], null))).Id!.Value;
        }
        var lines = new List<PurchaseRequestLineInput>();
        for (var i = 1; i <= items; i++)
        {
            var material = (await AsBuyerAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("rp").ToUpperInvariant(), $"ชื่อทะเบียน {i}", null, "ตัว", Cost: 10)))).Id!.Value;
            lines.Add(new PurchaseRequestLineInput(material, i, MaterialName: $"ชื่อในใบ {i}"));
        }
        var no = Unique("R") + "69-0001";
        var saved = await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(
            new PurchaseRequestInput(no, new DateOnly(2026, 10, 29), department, new DateOnly(2026, 11, 15), "F", "เหตุผลทดสอบ", "หมายเหตุทดสอบ", lines)));
        return (saved.Id!.Value, (await AsBuyerAsync(s => s.Get<PurchaseRequestDirectory>().GetAsync(saved.Id!.Value)))!);
    }

    private Task<PurchRequestPrint?> BuildAsync(int id, IReadOnlyCollection<int>? lineIds = null)
        => AsBuyerAsync(s => PurchRequestReports.BuildAsync(s.Get<PurchaseRequestDirectory>(), s.Get<OrgDirectory>(), s.Get<RequestSetupDirectory>(), id, lineIds));

    [Fact]
    public async Task The_print_has_the_head_and_every_item_with_the_names_typed_on_the_request()
    {
        var (id, request) = await NewRequestAsync(3);
        var print = (await BuildAsync(id))!;
        Assert.Equal((request.RequestNo, "โรงงาน", "เหตุผลทดสอบ", new DateOnly(2026, 11, 15)), (print.RequestNo, print.PurchasingLocation, print.Reason, print.DesiredDate));
        Assert.StartsWith("แผนกทดสอบ", print.DepartmentName);
        Assert.Equal(["ชื่อในใบ 1", "ชื่อในใบ 2", "ชื่อในใบ 3"], print.Lines.Select(l => l.Name)); // the document's text, not the register's (DQ-04)
        Assert.Equal([1m, 2m, 3m], print.Lines.Select(l => l.Quantity));
    }

    [Fact]
    public async Task Printing_again_only_the_picked_items_and_an_unknown_request_has_no_data()
    {
        var (id, request) = await NewRequestAsync(4);
        var picked = new[] { request.Lines[1].Id, request.Lines[3].Id };
        var print = (await BuildAsync(id, picked))!;
        Assert.Equal([2, 4], print.Lines.Select(l => l.LineNo));
        Assert.Empty((await BuildAsync(id, [int.MaxValue]))!.Lines); // nothing picked that exists → "ไม่มีรายการ" at the route
        Assert.Null(await BuildAsync(int.MaxValue));
    }

    [Fact]
    public async Task The_document_number_is_the_legacy_one_and_the_body_lists_the_items_and_the_count()
    {
        await using (var scope = db.Scope())
            Assert.Equal("PC-PU002-1/2547 ฉบับที่ 1", await scope.Get<DocumentNumberService>().PrintedTextAsync(DocumentKeys.PurchRequest));

        var (id, _) = await NewRequestAsync(15); // more rows than the legacy's fixed 12: one layout grows
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<PurchRequestView>(new() { [nameof(PurchRequestView.Report)] = (await BuildAsync(id))! }));
        Assert.Contains("รวมรายการ 15 รายการ", html);
        Assert.Contains("เหตุผลทดสอบ", html);
        Assert.Contains("ชื่อในใบ 15", html);
        Assert.Contains("ผู้อนุมัติ (ผู้จัดการโรงงาน)", html);
    }
}

/// <summary>RPT-01 body without a database.</summary>
public sealed class PurchRequestViewTests
{
    private static PurchRequestPrint Print(params PurchRequestPrintLine[] lines)
        => new("O69-0040", new DateOnly(2026, 10, 29), "แผนกทดสอบ", "โรงงาน", new DateOnly(2026, 11, 15), "เหตุผลทดสอบ", "หมายเหตุทดสอบ", lines);

    [Fact]
    public async Task A_print_without_items_says_no_data()
    {
        var html = await ReportBody.RenderAsync<PurchRequestView>(new() { [nameof(PurchRequestView.Report)] = Print() });
        Assert.Contains("ไม่มีข้อมูล", html);
    }

    [Fact]
    public async Task One_layout_grows_with_the_items_and_counts_them()
    {
        var lines = Enumerable.Range(1, 25).Select(i => new PurchRequestPrintLine(i, $"E{i:0000}", $"รายการ {i}", i, "ตัว")).ToArray();
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<PurchRequestView>(new() { [nameof(PurchRequestView.Report)] = Print(lines) }));
        Assert.Contains("รวมรายการ 25 รายการ", html);
        Assert.Contains("รายการ 25", html);
        Assert.Contains("O69-0040", html);
        Assert.Contains("เหตุผลทดสอบ", html);
        Assert.Contains("ผู้อนุมัติ (ผู้จัดการโรงงาน)", html);
    }
}

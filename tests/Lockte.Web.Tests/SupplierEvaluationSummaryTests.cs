using Lockte.Org;
using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>RPT-08 / RPT-09 (#255) without a database: the pages the views print.</summary>
public sealed class SupplierEvaluationSummaryViewTests
{
    private static readonly PurchSettingsDto Settings = new(0.07m, 5000m, null, ["sp"], 80m, 70m, 60m, [], null);

    private static SupplierDto Supplier(string code, bool certificate) => new(code.GetHashCode(), code, "ผู้ขาย " + code, "ที่อยู่", "เขต", "02", null, null, null, 30, true, "ชิ้นส่วน", "ผลิต", certificate, true, []);

    private static SupplierEvaluationDto Evaluation(SupplierDto s, SupplierCounts counts)
        => new(1, s.Id, s.Code, s.Name, new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30), 2, counts, 5, 5, SupplierScore.Calculate(counts, s.Certificate, 5, 5, Settings), []);

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters) where T : Microsoft.AspNetCore.Components.IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<T>(parameters));

    [Fact]
    public async Task The_summary_shows_scores_for_the_evaluated_and_no_order_for_the_rest_AC_44()
    {
        var a = Supplier("DC-31", true);
        var b = Supplier("AS-40", true);
        var rows = new List<EvaluationSummaryRow> { new(a, Evaluation(a, new SupplierCounts(4, 0, 3, 1, 4, 0))), new(b, null) };
        var html = await RenderAsync<SupplierEvaluationSummaryView>(new() { ["Report"] = new EvaluationSummary(new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30), 2, rows) });
        Assert.Contains("ไตรมาสที่ 2", html);
        Assert.Contains("DC-31", html);
        Assert.Contains("ไม่มีการสั่งซื้อ", html);
        Assert.Contains("รวมทั้งหมด 2 ราย", html);
        Assert.Equal(4, rows[0].OrderCount); // มีใบรับรอง + ไม่มี
        Assert.Null(rows[1].OrderCount);
    }

    [Fact]
    public async Task The_annual_form_has_a_page_per_supplier_with_its_details_and_the_questions_left_to_fill_in_BR_051_1()
    {
        var html = await RenderAsync<SupplierAnnualEvaluationView>(new() { ["Suppliers"] = new List<SupplierDto> { Supplier("AS-40", true), Supplier("DC-31", false) }, ["Year"] = 2026, ["Date"] = new DateOnly(2026, 10, 29) });
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "รหัสผู้จำหน่ายปัจจัยการผลิต").Count);
        Assert.Contains("ผู้ขาย AS-40", html);
        Assert.Contains("ประจำปี 2569", html);
        Assert.Contains("21. คุณภาพในการส่งสินค้าเป็นที่น่าพอใจหรือไม่", html);
        Assert.Contains("(D) คะแนนรวม (A)+(B)+(C)", html);
    }

    [Fact]
    public async Task No_supplier_prints_no_data()
        => Assert.Contains("ไม่มีข้อมูล", await RenderAsync<SupplierAnnualEvaluationView>(new() { ["Suppliers"] = new List<SupplierDto>(), ["Year"] = 2026, ["Date"] = new DateOnly(2026, 10, 29) }));
}

/// <summary>RPT-08 / RPT-09 (#255): which suppliers they list.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class SupplierEvaluationSummaryDataTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    private async Task<int> SupplierAsync(bool production, bool certificate = true)
        => (await AsAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, Unique("sm").ToUpperInvariant(), "ผู้ขายสรุป", null, Certificate: certificate, UsedInProduction: production)))).Id!.Value;

    private async Task ReceiveAsync(int supplier, DateOnly date, string? lot)
    {
        var material = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("sx").ToUpperInvariant(), "วัตถุดิบสรุป", null, "กก.")))).Id!.Value;
        int department;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
            department = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
        }
        var request = await AsAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput(Unique("S") + "69-0001", date, department, null, "F", null, null, [new PurchaseRequestLineInput(material, 1)])));
        var requestLine = (await AsAsync(s => s.Get<PurchaseRequestDirectory>().GetAsync(request.Id!.Value)))!.Lines.Single();
        var order = await AsAsync(s => s.Get<PurchaseOrderRegistry>().IssueAsync(new PurchaseOrderInput("69" + Random.Shared.Next(1000, 9999) + Guid.NewGuid().ToString("N")[..3], date, supplier, "ผู้ขายสรุป", null, null, null, null, 30, true, "F", null, null,
            [new PurchaseOrderLineInput(requestLine.Id, 1m, 1m)])));
        var line = (await AsAsync(s => s.Get<PurchaseOrderDirectory>().GetAsync(order.Id!.Value)))!.Lines.Single();
        await AsAsync(s => s.Get<GoodsReceiptRegistry>().ReceiveAsync([new GoodsReceiptInput(line.Id, date, 1m)]));
        if (lot is not null)
        {
            var receipt = (await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetReceiptsAsync(line.Id))).Single();
            await AsAsync(s => s.Get<GoodsReceiptRegistry>().SaveQaAsync(new GoodsReceiptQaUpdate(receipt.Id, receipt.RowVersion, lot, false, true, null)));
        }
    }

    [Fact] // BR-051 #1 #2, AC-43
    public async Task The_annual_form_lists_only_production_suppliers_that_received_in_the_period()
    {
        var produce = await SupplierAsync(true);
        var other = await SupplierAsync(false);
        var idle = await SupplierAsync(true);
        var from = new DateOnly(2028, 1, 1);
        var to = new DateOnly(2028, 12, 31);
        await ReceiveAsync(produce, new DateOnly(2028, 5, 5), null);
        await ReceiveAsync(other, new DateOnly(2028, 5, 5), null);
        await ReceiveAsync(idle, new DateOnly(2027, 5, 5), null); // outside

        var listed = await AsAsync(s => s.Get<SupplierDirectory>().GetProductionSuppliersWithReceiptsAsync(from, to));
        Assert.Contains(listed, s => s.Id == produce);
        Assert.DoesNotContain(listed, s => s.Id == other);
        Assert.DoesNotContain(listed, s => s.Id == idle);
    }

    [Fact] // BR-052 #1, AC-44: chosen by quarter and year, every production supplier listed, certificate holders first
    public async Task The_summary_lists_every_production_supplier_with_the_evaluated_ones_scored()
    {
        var evaluated = await SupplierAsync(true, certificate: true);
        var idle = await SupplierAsync(true, certificate: false);
        var notProduction = await SupplierAsync(false);
        await ReceiveAsync(evaluated, new DateOnly(2028, 5, 5), "L-1");
        await AsAsync(s => s.Get<SupplierEvaluationRegistry>().EvaluateAsync(new SupplierEvaluationInput(evaluated, new DateOnly(2028, 4, 1), new DateOnly(2028, 6, 30), 2, 5, 5)));

        var summary = await AsAsync(s => SupplierEvaluationSummaryReports.BuildSummaryAsync(s.Get<SupplierDirectory>(), s.Get<SupplierEvaluationDirectory>(), 2028, 2));
        Assert.Equal((new DateOnly(2028, 4, 1), new DateOnly(2028, 6, 30)), (summary.From, summary.To));
        Assert.Contains(summary.Rows, r => r.Supplier.Id == evaluated && r.Evaluation is { Score.Grade: "A" });
        Assert.Contains(summary.Rows, r => r.Supplier.Id == idle && r.Evaluation is null);
        Assert.DoesNotContain(summary.Rows, r => r.Supplier.Id == notProduction);
        Assert.True(summary.Rows.ToList().FindIndex(r => r.Supplier.Id == evaluated) < summary.Rows.ToList().FindIndex(r => r.Supplier.Id == idle)); // certificate first
        var other = await AsAsync(s => SupplierEvaluationSummaryReports.BuildSummaryAsync(s.Get<SupplierDirectory>(), s.Get<SupplierEvaluationDirectory>(), 2028, 3));
        Assert.All(other.Rows, r => Assert.Null(r.Evaluation)); // another quarter: nobody evaluated
    }
}

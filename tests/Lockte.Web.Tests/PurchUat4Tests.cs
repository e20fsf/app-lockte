using Lockte.Purch;
using Lockte.Web.Components.Pages.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;
using IncomingReport = Lockte.Web.Reports.Purch.IncomingReport;

namespace Lockte.Web.Tests;

/// <summary>UAT วัตถุดิบ ครั้งที่ 2 ข้อ 4 (#449), without a database: the receipt reports' lines, sign boxes, headings and the missing footer date.</summary>
public sealed class PurchUat4Tests
{
    private static readonly DateOnly From = new(2026, 11, 1), To = new(2026, 11, 30);

    private static IncomingRow Row(string supplier, string lot)
        => new(new DateOnly(2026, 11, 12), "690993", "CA130", "กล่อง NO.5", 500m, "ใบ", 1, supplier, new DateOnly(2026, 11, 18), lot, false, true, true, null);

    private static ReceivedLine Line(int supplierId, string supplierCode, string supplierName, int materialId, string materialCode)
        => new(materialId, new DateOnly(2026, 11, 12), "690993", "REQ1", 1, materialId, materialCode, "กล่อง " + materialCode, "ใบ", true, 500m, 23.55m, 11775m, supplierId, supplierCode, supplierName, false);

    private static async Task<string> RenderAsync<T>(object report) where T : Microsoft.AspNetCore.Components.IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<T>(new() { ["Report"] = report }));

    [Fact]
    public async Task The_record_and_the_inspection_draw_a_line_round_every_cell_and_the_record_signs_at_the_bottom()
    {
        var report = new IncomingReport(From, To, null, [Row("ผู้ขาย ก", "L1")]);
        var record = await RenderAsync<IncomingRecordView>(report);
        Assert.Contains("<table class=\"grid\"", record);
        Assert.Contains("Prepared by", record);
        Assert.Contains("class=\"bottom\"", record); // the sign boxes are pushed to the foot of the sheet
        Assert.True(record.IndexOf("รวมทั้งหมด", StringComparison.Ordinal) < record.IndexOf("Prepared by", StringComparison.Ordinal));
        Assert.Contains("<table class=\"grid\"", await RenderAsync<IncomingInspectionView>(report));
        Assert.Contains("table.grid td", ReportHtml.Compose(new ReportDocument("x", Paper.A4Portrait), new PrintStamp(DateTimeOffset.Now, "u"), "").Replace("th,", ""));
    }

    [Fact]
    public void The_incoming_documents_print_no_date_at_the_foot()
    {
        Assert.False(IncomingReports.PrintsFooter);
        var html = ReportHtml.Compose(new ReportDocument("x", Paper.A4Portrait, Footer: IncomingReports.PrintsFooter), new PrintStamp(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero), "u"), "<p>x</p>");
        Assert.DoesNotContain("@bottom-left", html);
        Assert.DoesNotContain("@bottom-right", html);
        Assert.DoesNotContain("12 พฤศจิกายน", html);
    }

    [Fact]
    public async Task The_material_report_names_the_material_once_under_the_title()
    {
        var html = await RenderAsync<MaterialReceiptView>(ReceiptReports.Material([Line(1, "AS-32", "ผู้ขาย", 10, "CA130")], 10, "กล่อง CA130", From, To));
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(html, "กล่อง CA130").Count);
        Assert.Equal("รายงานการรับวัตถุดิบ", ReceiptReports.MaterialTitle);
    }

    [Fact]
    public async Task The_supplier_report_names_the_supplier_only_in_its_heading()
    {
        var lines = new[] { Line(1, "AS-32", "ไทยแปซิฟิค", 10, "CA130"), Line(1, "AS-32", "ไทยแปซิฟิค", 11, "CA074") };
        var html = await RenderAsync<SupplierReceiptView>(ReceiptReports.Supplier(lines, 1, From, To));
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(html, "ไทยแปซิฟิค").Count);
        Assert.Contains("รหัสผู้ขาย AS-32", html);
        Assert.DoesNotContain("รวม ไทยแปซิฟิค", html);

        var many = await RenderAsync<SupplierReceiptView>(ReceiptReports.Supplier([.. lines, Line(2, "S0391", "พงษ์พันธ์", 10, "CA130")], null, From, To));
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(many, "พงษ์พันธ์").Count);
        Assert.Contains("รวมทั้งหมด", many);
    }

    [Fact]
    public void The_arrow_of_a_picker_with_an_item_chosen_lists_every_item_not_just_the_one_chosen()
    {
        var material = new MaterialDto(54, "CA130", "กล่อง NO.5  26X63X27", "ใบ", null, 0m, null, null, null, true, 0m, null, []);
        Assert.Null(MaterialPicker.Term("CA130  กล่อง NO.5  26X63X27", material));
        Assert.Equal("กล่อง", MaterialPicker.Term("กล่อง", material)); // what the user types is still searched
        Assert.Equal("CA130  x", MaterialPicker.Term("CA130  x", null));
    }
}

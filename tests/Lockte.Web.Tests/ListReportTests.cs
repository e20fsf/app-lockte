using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>รายชื่อวัตถุดิบ RPT-28/29 และรายชื่อผู้ขาย RPT-30/31 (#263) without a database.</summary>
public sealed class ListReportTests
{
    private static MaterialDto Material(string code, string name, decimal cost = 1m) => new(code.GetHashCode(), code, name, "กก.", null, cost, null, null, null, false, 0m, null, []);

    private static SupplierDto Supplier(string code, string name, int? credit = 30)
        => new(code.GetHashCode(), code, name, "ที่อยู่ 1", "ที่อยู่ 2", "02-000", null, "02-111", "ผู้ติดต่อ", credit, true, "ลวด", null, false, true, []);

    [Fact]
    public void Materials_by_code_filter_the_group_ignoring_case_and_by_name_use_the_thai_order()
    {
        var all = new[] { Material("B02", "เหล็ก"), Material("B01", "ลวด"), Material("C01", "กล่อง"), Material("b03", "ขวด") };
        var byCode = ListReports.Materials(all, "B*", false);
        Assert.Equal(["B01", "B02", "b03"], byCode.Rows.Select(r => r.Code));
        Assert.Equal(["C01", "b03", "B01", "B02"], ListReports.Materials(all, null, true).Rows.Select(r => r.Code));
    }

    [Fact]
    public void Leading_vowels_do_not_count_in_the_thai_order_of_names()
    {
        // เก… sorts with ก, before ข — a plain code-point sort would put เก after ค
        var names = ListReports.Materials([Material("1", "ค"), Material("2", "เก"), Material("3", "ข")], null, true).Rows.Select(r => r.Name);
        Assert.Equal(["เก", "ข", "ค"], names);
    }

    [Fact]
    public void Suppliers_are_listed_by_code_or_by_name()
    {
        var all = new[] { Supplier("S2", "บริษัท ข"), Supplier("S1", "บริษัท ค"), Supplier("S3", "บริษัท ก") };
        Assert.Equal(["S1", "S2", "S3"], ListReports.Suppliers(all, false).Rows.Select(r => r.Code));
        Assert.Equal(["S3", "S2", "S1"], ListReports.Suppliers(all, true).Rows.Select(r => r.Code));
    }

    [Fact]
    public async Task The_views_print_the_rows_the_credit_text_and_no_data_when_empty()
    {
        var materials = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<MaterialListView>(new() { ["Report"] = ListReports.Materials([Material("B01", "ลวด", 12.5m)], "B", false) }));
        Assert.Contains("กลุ่มรหัส B*", materials);
        Assert.Contains("12.50", materials);
        var suppliers = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<SupplierListView>(new() { ["Report"] = ListReports.Suppliers([Supplier("S1", "ผู้ขาย", credit: 0)], false) }));
        Assert.Contains("เงินสด", suppliers);
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<MaterialListView>(new() { ["Report"] = ListReports.Materials([], null, false) }));
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<SupplierListView>(new() { ["Report"] = ListReports.Suppliers([], true) }));
    }
}

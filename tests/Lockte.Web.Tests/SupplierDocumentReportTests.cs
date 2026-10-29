using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>ทะเบียนผู้ขาย RPT-32 และหนังสือแสดงความมุ่งมั่น RPT-33 (#264) without a database.</summary>
public sealed class SupplierDocumentReportTests
{
    private static SupplierDto Supplier(string code, string name, bool production)
        => new(code.GetHashCode(), code, name, "ที่อยู่ 1", "ที่อยู่ 2", "02-000", null, "02-111", "ผู้ติดต่อ", 30, true, "ลวด", null, false, production, []);

    [Fact]
    public void Only_the_suppliers_used_in_production_are_in_the_register_by_code()
    {
        var report = SupplierDocumentReports.Approved([Supplier("S2", "ผู้ขาย ข", true), Supplier("S1", "ผู้ขาย ก", false), Supplier("S0", "ผู้ขาย ค", true)]);
        Assert.Equal(["S0", "S2"], report.Rows.Select(r => r.Code));
    }

    [Fact]
    public async Task The_register_and_the_letter_print_what_the_form_says()
    {
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<ApprovedSupplierView>(new() { ["Report"] = SupplierDocumentReports.Approved([Supplier("S1", "ผู้ขายทดสอบ", true)]) }));
        Assert.Contains("ผู้ขายทดสอบ", html);
        Assert.Contains("02-111", html);
        Assert.Contains("ไม่มีข้อมูล", await ReportBody.RenderAsync<ApprovedSupplierView>(new() { ["Report"] = SupplierDocumentReports.Approved([]) }));

        var letter = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<SupplierCommitmentView>(new() { ["Report"] = new SupplierCommitmentLetter("ผู้ขายทดสอบ", new DateOnly(2026, 10, 19)) }));
        Assert.Contains("วันที่ 19 ตุลาคม 2569", letter);
        Assert.Equal(3, letter.Split("ผู้ขายทดสอบ").Length - 1); // under the title, in the line of the contract party and under the place to sign
        Assert.Contains("มรท. 8001", letter);
        Assert.Contains("ซึ่งเป็นบริษัทคู่สัญญากับ", letter);
        Assert.Contains("ออกให้แก่ บริษัท ล็อคเต้ จำกัด", letter);
    }
}

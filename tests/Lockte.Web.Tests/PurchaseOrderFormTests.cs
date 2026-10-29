using System.Text;
using System.Text.RegularExpressions;
using Lockte.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>
/// RPT-02 ใบสั่งซื้อ (#224, ระดับ A): the data of the sample orders and the sheets of the form. The look of the page is checked by overlaying the PDF on the legacy
/// sample (set env <c>LOCKTE_PDF_OUT</c> to a folder to keep the PDFs the tests print).
/// </summary>
[Collection(ChromiumCollection.Name)]
public sealed class PurchaseOrderFormTests(ChromiumFixture chromium)
{
    private static readonly PurchLetterheadOptions Letterhead = new() { Line1 = "ที่อยู่สำนักงาน (ข้อมูลทดสอบ) โทร. 02-000-0000", Line2 = "Test Office Address, Tel. 02-000-0000" };

    private static PurchaseOrderPrint Order(params (string Name, decimal Quantity, decimal UnitPrice)[] lines)
    {
        var printed = lines.Select((l, i) => new PurchaseOrderPrintLine(i + 1, "K69-0025", l.Name, l.Quantity, "ชิ้น", l.UnitPrice, PurchaseOrderMath.LineNet(l.UnitPrice, l.Quantity))).ToList();
        var totals = PurchaseOrderMath.Calculate(printed.Select(l => l.Amount).ToList(), true, 0.07m);
        return new PurchaseOrderPrint("690988", new DateOnly(2026, 10, 29), "บริษัท ผู้ขายทดสอบ จำกัด", "99 ถนนทดสอบ", "เขตทดสอบ กรุงเทพฯ 10000", "02-111-1111", "02-222-2222",
            "อัดผ้าเบรก", new DateOnly(2026, 11, 15), 30, printed, totals.Subtotal, totals.Tax, totals.Total, totals.TotalInWords, "เหตุผลทดสอบ", "หมายเหตุทดสอบ");
    }

    private static string Text(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    private static int Pages(byte[] pdf) => Regex.Matches(Text(pdf), @"/Type /Page\b").Count;

    private static void Keep(string name, byte[] pdf)
    {
        if (Environment.GetEnvironmentVariable("LOCKTE_PDF_OUT") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".pdf"), pdf);
        }
    }

    [Fact]
    public void The_sample_order_has_the_totals_of_AC_06_and_the_words_of_BR_064()
    {
        var order = Order(("รายการหนึ่ง", 1, 950m), ("รายการสอง", 1, 5430m));
        Assert.Equal((6380.00m, 446.60m, 6826.60m), (order.Subtotal, order.Tax, order.Total));
        Assert.Equal("หกพันแปดร้อยยี่สิบหกบาทหกสิบสตางค์", order.TotalInWords);
        var noVat = PurchaseOrderMath.Calculate([288000m], false, 0.07m); // AC-08
        Assert.Equal((0m, 288000.00m), (noVat.Tax, noVat.Total));
    }

    [Fact]
    public void One_sheet_holds_ten_lines_and_more_go_on_further_sheets_with_the_total_on_the_last()
    {
        Assert.Single(PurchaseOrderForm.Layout(Order(Enumerable.Range(1, 10).Select(i => ($"รายการ {i}", 1m, 10m)).ToArray()), "PC-PU002-2/2547 ฉบับที่ 1", Letterhead));
        var pages = PurchaseOrderForm.Layout(Order(Enumerable.Range(1, 23).Select(i => ($"รายการ {i}", 1m, 10m)).ToArray()), "PC-PU002-2/2547 ฉบับที่ 1", Letterhead);
        Assert.Equal(3, pages.Count);
        Assert.DoesNotContain("Grand Total", pages[0].ToSvg());
        Assert.Contains("Grand Total", pages[2].ToSvg());
        Assert.Contains("รวมรายการ", pages[2].ToSvg());
        Assert.Contains(">23<", pages[2].ToSvg());
    }

    [Fact]
    public async Task The_form_prints_as_an_A4_pdf_one_sheet_for_few_lines_and_the_empty_form_too()
    {
        var one = PurchaseOrderForm.Layout(Order(("อลูมิเนียมก้อน", 3000m, 96m), ("ลวดเชื่อม", 12.5m, 20m)), "PC-PU002-2/2547 ฉบับที่ 1", Letterhead);
        var pdf = await chromium.Renderer.RenderAsync(FormPageHtml.Compose(PurchaseOrderForm.Title, one), one[0].Paper);
        Assert.Equal(1, Pages(pdf));
        Keep("purchase-order-2-lines", pdf);

        var many = PurchaseOrderForm.Layout(Order(Enumerable.Range(1, 13).Select(i => ($"รายการที่ {i} ชื่อยาวหน่อยเพื่อทดสอบการบีบข้อความให้พอดีช่อง", 1m + i, 10m * i)).ToArray()), "PC-PU002-2/2547 ฉบับที่ 1", Letterhead);
        var manyPdf = await chromium.Renderer.RenderAsync(FormPageHtml.Compose(PurchaseOrderForm.Title, many), many[0].Paper);
        Assert.Equal(2, Pages(manyPdf));
        Keep("purchase-order-13-lines", manyPdf);

        var blank = PurchaseOrderForm.Blank("PC-PU002-2/2547 ฉบับที่ 1", Letterhead);
        var blankPdf = await chromium.Renderer.RenderAsync(FormPageHtml.Compose(PurchaseOrderForm.Title, [blank]), blank.Paper);
        Assert.Equal(1, Pages(blankPdf));
        Keep("purchase-order-blank", blankPdf);
    }
}

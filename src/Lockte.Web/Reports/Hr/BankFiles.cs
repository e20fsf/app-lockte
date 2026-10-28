using Lockte.Hr;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Reports.Hr;

/// <summary>
/// ไฟล์ส่งธนาคาร (INT-01 ttb, INT-02 กสิกร, SCR-42, #54): the rows of the legacy reports <c>เงินเดือนส่งแบ็งค์ทหารไทย</c> and
/// <c>เงินOTส่งแบ็งค์กสิกร</c> as a <see cref="SummaryTable"/> — the same table is the page of RPT-05 (แบบกลาง B) and the Excel the user pastes
/// into the bank's template (header in row 1, items, a total row last: "ไม่เอาบรรทัดแรกและบรรทัดสุดท้าย").
/// ⚠ Not yet compared with the real template or an export from Access (#25): the columns follow the printed reports of lot 4 —
/// ttb: ลำดับ · ชื่อ-นามสกุล · จำนวนเงิน · วันที่ (blank) · เลขบัญชี; กสิกร: ลำดับ · เลขบัญชี · คำนำหน้า · ชื่อ (อังกฤษ) · จำนวนเงิน, by เลขบัญชี ↑.
/// A change of the template is a change of the two column lists here.
/// </summary>
public static class BankFiles
{
    public const string TtbTitle = "เงินเดือนส่งแบ็งค์ทหารไทย";
    public const string KbankTitle = "เงินOTส่งแบ็งค์กสิกร";

    /// <summary>The ttb list (PAY_W, or ค่าแรงงวด + โบนัส for RPT-40) as the report <paramref name="title"/> prints it.</summary>
    public static SummaryTable Ttb(IEnumerable<BankTransferRow> rows, string title = TtbTitle)
    {
        var list = rows.Where(r => r.Bank == BankTransfers.Ttb).ToList();
        return new SummaryTable(title, null,
            [new("ลำดับ", SummaryFormat.Integer), new("ชื่อ-นามสกุล"), new("จำนวนเงิน", SummaryFormat.Money), new("วันที่"), new("เลขบัญชี")],
            list.Select((r, i) => new object?[] { i + 1, r.Name, r.Amount, null, r.Account }).ToList(),
            [null, $"รวม {list.Count} รายการ", list.Sum(r => r.Amount), null, null]);
    }

    /// <summary>The กสิกร list (PAY_OT, or OT งวด + โบนัส for RPT-41).</summary>
    public static SummaryTable Kbank(IEnumerable<BankTransferRow> rows, string title = KbankTitle)
    {
        var list = rows.Where(r => r.Bank == BankTransfers.Kbank).ToList();
        return new SummaryTable(title, null,
            [new("ลำดับ", SummaryFormat.Integer), new("เลขบัญชี"), new("คำนำหน้า"), new("ชื่อ"), new("จำนวนเงิน", SummaryFormat.Money)],
            list.Select((r, i) => new object?[] { i + 1, r.Account, r.EnglishTitle, r.EnglishName, r.Amount }).ToList(),
            [null, $"รวม {list.Count} รายการ", null, null, list.Sum(r => r.Amount)]);
    }

    /// <summary>The table of a list name from the address (<c>ttb</c> / <c>kbank</c>); null for any other.</summary>
    public static SummaryTable? Of(string bank, IEnumerable<BankTransferRow> rows, string? ttbTitle = null, string? kbankTitle = null)
        => bank.ToUpperInvariant() switch
        {
            BankTransfers.Ttb => Ttb(rows, ttbTitle ?? TtbTitle),
            BankTransfers.Kbank => Kbank(rows, kbankTitle ?? KbankTitle),
            _ => null,
        };

    /// <summary>The file as downloaded: no title row, so the header is row 1.</summary>
    public static byte[] Excel(SummaryTable table) => XlsxWriter.Write(table with { Title = "" });
}

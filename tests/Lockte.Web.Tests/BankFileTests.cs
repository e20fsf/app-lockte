using System.IO.Compression;
using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Web.Reports.Hr;

namespace Lockte.Web.Tests;

/// <summary>#54 ไฟล์ส่งธนาคาร (INT-01/02, RPT-05) without a database: the lists, the table and the Excel the user pastes into the template.</summary>
public sealed class BankFileTests
{
    private static readonly BankPayInput[] Pay =
    [
        new("A002", "2712073721", null, 2232, 0, 0, "นางสาวสมหญิง ทดสอบ"),
        new("A001", "0498514049", "0498514049", 6996, 140, 0, "นายสมชาย ทดสอบ", "Mr.", "SOMCHAI TEST"),
        new("A003", null, "2383297004", 5000, 461, 0, "นายสมศักดิ์ ทดสอบ", "Mr.", "SOMSAK TEST"),
    ];

    [Fact] // the ttb list: ลำดับ · ชื่อ-นามสกุล · จำนวนเงิน · วันที่ (blank) · เลขบัญชี, by เลขบัญชี ↑, the total row last
    public void The_ttb_table_is_the_legacy_report()
    {
        var table = BankFiles.Ttb(BankTransfers.Build(Pay));

        Assert.Equal(["ลำดับ", "ชื่อ-นามสกุล", "จำนวนเงิน", "วันที่", "เลขบัญชี"], table.Columns.Select(c => c.Header));
        Assert.Equal([[1, "นายสมชาย ทดสอบ", 6996m, null, "0498514049"], [2, "นางสาวสมหญิง ทดสอบ", 2232m, null, "2712073721"]], table.Rows.Select(r => r));
        Assert.Equal([null, "รวม 2 รายการ", 9228m, null, null], table.TotalRow);
    }

    [Fact] // the กสิกร list: ลำดับ · เลขบัญชี · คำนำหน้า · ชื่อ (English) · จำนวนเงิน — PAY_OT > 0 and ACC2 only
    public void The_kbank_table_is_the_legacy_report()
    {
        var table = BankFiles.Kbank(BankTransfers.Build(Pay));

        Assert.Equal(["ลำดับ", "เลขบัญชี", "คำนำหน้า", "ชื่อ", "จำนวนเงิน"], table.Columns.Select(c => c.Header));
        Assert.Equal([[1, "0498514049", "Mr.", "SOMCHAI TEST", 140m], [2, "2383297004", "Mr.", "SOMSAK TEST", 461m]], table.Rows.Select(r => r));
        Assert.Equal([null, "รวม 2 รายการ", null, null, 601m], table.TotalRow);
    }

    [Fact] // the list of the address is ttb or kbank in either case; anything else is not a list
    public void The_list_comes_from_the_address()
    {
        Assert.Equal(BankFiles.TtbTitle, BankFiles.Of("TTB", BankTransfers.Build(Pay))!.Title);
        Assert.Equal(BankFiles.KbankTitle, BankFiles.Of("kbank", BankTransfers.Build(Pay))!.Title);
        Assert.Null(BankFiles.Of("letter", BankTransfers.Build(Pay)));
    }

    private static string Sheet(byte[] xlsx)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsx));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        return reader.ReadToEnd();
    }

    [Fact] // header in row 1 (no title row), the items from row 2, the total last; accounts are text so a leading 0 stays; amounts are numbers
    public void The_excel_starts_with_the_header_and_ends_with_the_total()
    {
        var sheet = Sheet(BankFiles.Excel(BankFiles.Kbank(BankTransfers.Build(Pay))));

        Assert.Equal(["1", "2", "3", "4"], Regex.Matches(sheet, "<row r=\"(\\d+)\"").Select(m => m.Groups[1].Value));
        Assert.Contains("<c r=\"A1\" t=\"inlineStr\" s=\"1\"><is><t>ลำดับ</t></is></c>", sheet);
        Assert.Contains("<c r=\"B2\" t=\"inlineStr\" s=\"0\"><is><t xml:space=\"preserve\">0498514049</t></is></c>", sheet);
        Assert.Contains("<c r=\"E2\" s=\"3\"><v>140</v></c>", sheet);
        Assert.Contains("รวม 2 รายการ", sheet);
        Assert.Contains("<c r=\"E4\" s=\"3\"><v>601</v></c>", sheet);
    }

    [Fact] // the lists with the bonus (RPT-40/41) have the same shape under their own titles
    public void The_bonus_lists_use_the_same_table()
    {
        var rows = Bonus.Payment([new BonusPayInput("A001", "111", "901", 7500, 875, 0, 7500, "นายสมชาย ทดสอบ", "Mr.", "SOMCHAI TEST")]);

        Assert.Equal((15875m, "นายสมชาย ทดสอบ"), (rows.Single(r => r.Bank == BankTransfers.Ttb).Amount, rows.Single(r => r.Bank == BankTransfers.Ttb).Name));
        Assert.Equal("SOMCHAI TEST", rows.Single(r => r.Bank == BankTransfers.Kbank).EnglishName);
        Assert.Equal("เงินOTรวมโบนัสส่งแบ็งค์กสิกร", BankFiles.Of("kbank", rows, kbankTitle: "เงินOTรวมโบนัสส่งแบ็งค์กสิกร")!.Title);
    }
}

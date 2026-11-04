using System.IO.Compression;
using Lockte.Hr;
using Lockte.Web.Reports.Hr;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Tests;

/// <summary>ยอดเงินส่งกองทุนสงเคราะห์ลูกจ้าง (UAT-23): the columns, header and rows of the Excel, from made-up employees.</summary>
public sealed class WelfareFundTableTests
{
    private static readonly DateOnly From = new(2026, 8, 27);
    private static readonly DateOnly To = new(2026, 9, 26);

    [Theory]
    [InlineData("นายสมมุติ ทดสอบ", "นาย", "สมมุติ", "ทดสอบ")]
    [InlineData("นางสาวสมหญิง ตัวอย่าง ใหญ่", "นางสาว", "สมหญิง", "ตัวอย่าง ใหญ่")]
    [InlineData("นาง ก ข", "นาง", "ก", "ข")]
    [InlineData("Mr. Test Sample", "Mr.", "Test", "Sample")]
    [InlineData("สมมุติ ทดสอบ", "", "สมมุติ", "ทดสอบ")]
    [InlineData("", "", "", "")]
    public void The_name_field_is_cut_into_title_first_and_last(string full, string title, string first, string last)
        => Assert.Equal((title, first, last), WelfareFundTable.SplitName(full));

    [Fact]
    public void The_sheet_has_the_funds_header_and_one_row_per_employee_in_the_funds_column_order()
    {
        var report = new WelfareFundReport(From, To, null,
        [
            new WelfareFundLine("U001", "นางสาวสมมุติ ทดสอบ", 30, "ไทย", "1234567890123", EmployeeTypes.Daily, 9858m, new DateOnly(2020, 3, 1), null),
            new WelfareFundLine("U002", "นายตัวอย่าง จำลอง", null, "พม่า", "P000000", EmployeeTypes.Monthly, 12340m, new DateOnly(2021, 1, 4), new DateOnly(2026, 9, 10)),
        ]);

        var table = WelfareFundTable.Build(report, "บริษัท ทดสอบ จำกัด", new DateOnly(2026, 10, 5));

        Assert.Equal(["คำนำหน้า", "ชื่อ", "สกุล", "อายุ", "สัญชาติ", "เลขบัตรประชาชน/เลขหนังสือเดินทาง", "ประเภทการจ้าง (รายวัน/รายเดือน)",
            "ค่าจ้าง (บาท)", "วันที่เริ่มงาน", "วันที่สิ้นสุดงาน", "สาเหตุการออกจากงาน/หมายเหตุอื่นๆ"], table.Columns.Select(c => c.Header));
        Assert.Contains("ชื่อสถานประกอบกิจการ บริษัท ทดสอบ จำกัด", table.Filter);
        Assert.Contains("รอบการส่งเงิน ปี พ.ศ. 2569 เดือน กันยายน", table.Filter);
        Assert.Contains("วันที่ดำเนินการนำส่ง 5/10/2569", table.Filter);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(new object?[] { "นางสาว", "สมมุติ", "ทดสอบ", 30, "ไทย", "1234567890123", "รายวัน", 9858m, "1/3/2563", null, null }, table.Rows[0]);
        Assert.Equal("รายเดือน", table.Rows[1][6]);
        Assert.Equal("10/9/2569", table.Rows[1][9]);
        Assert.Equal("ลาออก", table.Rows[1][10]);
        Assert.Null(table.Rows[1][3]); // unknown age stays blank, not 0
    }

    [Fact]
    public void The_header_lines_become_rows_above_the_table_in_the_workbook()
    {
        var table = WelfareFundTable.Build(new WelfareFundReport(From, To, null, []), "บริษัท ทดสอบ จำกัด", To);

        using var zip = new ZipArchive(new MemoryStream(XlsxWriter.Write(table)));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var sheet = reader.ReadToEnd();

        Assert.Contains("บริษัท ทดสอบ จำกัด", sheet);
        Assert.Contains("รอบการส่งเงิน", sheet);
        Assert.Contains("วันที่ดำเนินการนำส่ง", sheet);
        Assert.Contains("<row r=\"6\">", sheet); // title, 3 header lines, a blank row, then the column headings
    }
}

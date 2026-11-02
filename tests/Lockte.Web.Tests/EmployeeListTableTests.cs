using System.IO.Compression;
using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Tests;

/// <summary>#344 รายชื่อพนักงาน (ยังไม่บรรจุ / แผนก / ทั้งหมด) without a database: names and dates stay on one line, the Excel is the report's own table.</summary>
public sealed class EmployeeListTableTests
{
    private const string LongName = "นางสาวเมตตาลักษณ์ อยู่สมบูรณ์พูนสุขเจริญ";

    private static string Sheet(SummaryTable table)
    {
        using var zip = new ZipArchive(new MemoryStream(XlsxWriter.Write(table)));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        return reader.ReadToEnd();
    }

    [Fact] // ชื่อ-นามสกุลและวันที่ไม่ตกบรรทัด: nowrap on those cells in all three reports
    public async Task Names_and_dates_are_kept_on_one_line()
    {
        var employee = new EmployeeDto
        {
            Id = 1, Code = "S060", Name = LongName, Nationality = "ไทย", StartDate = new(2026, 8, 13), EmployeeType = EmployeeTypes.Daily, ShiftCode = "D", RowVersion = [],
        };
        var probation = await ReportBody.RenderAsync<OnProbationReportView>(new() { [nameof(OnProbationReportView.Employees)] = new List<EmployeeDto> { employee } });
        var roster = await ReportBody.RenderAsync<DepartmentRosterView>(new()
        {
            [nameof(DepartmentRosterView.Roster)] = new DepartmentRoster("รายชื่อพนักงานแผนกตกแต่ง", [new("T214", LongName, new(2002, 12, 10), "หัวหน้า", 23, 10, 47, 17020.26, 567.34)], 0, 1),
        });
        var working = await ReportBody.RenderAsync<WorkingEmployeesView>(new()
        {
            [nameof(WorkingEmployeesView.List)] = new WorkingEmployeeList([new("T214", LongName, "ตกแต่ง", "หัวหน้า", new(1979, 1, 2), new(2002, 12, 10), 1, 47.5, "รพ.")], 0, 1),
        });

        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(probation, "<td class=\"nw\">").Count); // ชื่อ, เริ่ม, ถึงกำหนด
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(roster, "<td class=\"nw\">").Count); // ชื่อ, เริ่มทำงาน
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(working, "<td class=\"nw\">").Count); // ชื่อ, เกิด, เริ่มทำงาน
    }

    [Fact] // the heading is printed once: the page header; no second copy of the title above the table
    public async Task The_roster_does_not_repeat_its_title_above_the_table()
    {
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<DepartmentRosterView>(new()
        {
            [nameof(DepartmentRosterView.Roster)] = new DepartmentRoster("รายชื่อพนักงานแผนกตกแต่ง", [new("T058", "นางสาวทดสอบ", null, null, null, null, null, 0, 587.32)], 0, 1),
        }));

        Assert.DoesNotContain("รายชื่อพนักงานแผนกตกแต่ง", html);
        Assert.DoesNotContain("class=\"sub\"", html);
    }

    [Fact] // ข้อมูลตรงกับรายงาน: same order, พ.ศ. dates, footer
    public void The_probation_excel_is_the_report_table()
    {
        EmployeeDto E(string code, DateOnly start) => new()
        {
            Id = 1, Code = code, Name = "ชื่อ " + code, Nationality = "ไทย", StartDate = start, EmployeeType = EmployeeTypes.Daily, ShiftCode = "D", DepartmentName = "สโตร์", RowVersion = [],
        };
        var table = EmployeeListTables.OnProbation([E("S060", new(2026, 8, 13)), E("A100", new(2026, 9, 1))]);
        var sheet = Sheet(table);

        Assert.True(sheet.IndexOf("A100", StringComparison.Ordinal) < sheet.IndexOf("S060", StringComparison.Ordinal));
        Assert.Contains("<t xml:space=\"preserve\">11/11/2569</t>", sheet); // 13/8/2569 + 90
        Assert.Contains("รวมทั้งหมด 2 คน", sheet);
    }

    [Fact] // roster: pay as numbers (2 decimals), อายุเฉลี่ย in the footer, the title as the Excel title row
    public void The_roster_excel_has_numbers_and_the_footer()
    {
        var roster = new DepartmentRoster("รายชื่อพนักงานแผนกตกแต่ง",
            [new("T058", "นางสาวหนึ่ง", new(1994, 6, 21), "พนักงาน", 32, 4, 54, 0, 587.32), new("T214", "นางสองหนึ่ง", null, null, null, null, 45, 12928, 0)], 0, 2);
        var table = EmployeeListTables.Roster(roster);
        var sheet = Sheet(table);

        Assert.Equal("รายชื่อพนักงานแผนกตกแต่ง", table.Title);
        Assert.Equal(10, table.Columns.Count);
        Assert.Contains("<v>587</v>", sheet); // ค่าแรง/วัน whole (#354); เงินเดือน keeps its satang
        Assert.Contains("<v>12928</v>", sheet);
        Assert.Contains("รวม 2 คน", sheet);
        Assert.Contains("<v>50</v>", sheet); // (54 + 45) / 2 = 49.5 → 50
    }

    [Fact]
    public void The_working_excel_lists_every_line_with_its_footer()
    {
        var table = EmployeeListTables.Working(new WorkingEmployeeList([new("T214", "นางสองหนึ่ง", "ตกแต่ง", "หัวหน้า", new(1979, 1, 2), new(2002, 12, 10), 1, 47.5, "รพ.")], 1, 0));
        var sheet = Sheet(table);

        Assert.Equal(1, table.Rows.Count);
        Assert.Contains("<t xml:space=\"preserve\">2/1/2522</t>", sheet);
        Assert.Contains("ชาย 1 คน", sheet);
        Assert.Contains("<v>48</v>", sheet); // อายุ 47.5 → 48
    }
}

/// <summary>#345 ทะเบียนประวัติพนักงาน without a database.</summary>
public sealed class EmployeeRegisterTableTests
{
    private static readonly EmployeeRegisterLine Line = new("T214", "นางสาวเมตตาลักษณ์ อยู่สมบูรณ์พูนสุขเจริญ", new(1979, 1, 2), "หญิง", "ไทย", "124 หมู่ 8", "101/10 หมู่ 3",
        "ป.6", new(2002, 12, 10), "หัวหน้า", "ตกแต่ง", 17020.26, new(2026, 1, 1));

    [Fact] // ชื่อ + วันเกิด + วันเริ่มทำงาน + วันสิ้นสุดสัญญา ไม่ตกบรรทัด
    public async Task Name_and_the_three_dates_are_kept_on_one_line()
    {
        var html = await ReportBody.RenderAsync<EmployeeRegisterView>(new() { [nameof(EmployeeRegisterView.Lines)] = new List<EmployeeRegisterLine> { Line } });

        Assert.Equal(4, System.Text.RegularExpressions.Regex.Matches(html, "<td class=\"nw\">").Count);
    }

    [Fact]
    public void The_excel_is_the_register_with_the_given_title()
    {
        var table = EmployeeListTables.Register([Line], EmployeeRegisterView.CurrentTitle);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(XlsxWriter.Write(table)));
        var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();

        Assert.Equal(14, table.Columns.Count);
        Assert.Contains(EmployeeRegisterView.CurrentTitle, sheet);
        Assert.Contains("<v>17020.26</v>", sheet);
        Assert.Contains("<t xml:space=\"preserve\">1/1/2569</t>", sheet);
    }
}

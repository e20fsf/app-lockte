using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;
using Microsoft.AspNetCore.Components;

namespace Lockte.Web.Tests;

/// <summary>
/// The HTML of the #59 reports before it becomes a PDF (แบบกลาง B, #40): no rows → "ไม่มีข้อมูล" (D-6), the month from the
/// dates (D-5), the legacy formats (1 / 2 decimals, H:mm, ได้/ไม่ได้), long text kept whole for wrapping (D-1).
/// </summary>
public sealed class AttendanceReportLayoutTests
{
    private static readonly DateOnly From = new(2026, 9, 1);
    private static readonly DateOnly To = new(2026, 9, 30);

    /// <summary>The body with its character references decoded (Razor writes data text such as Thai as &amp;#xE27;…).</summary>
    private static async Task<string> HtmlAsync<TView>(object report) where TView : IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<TView>(new() { ["Report"] = report }));

    public static TheoryData<string, object> EmptyReports => new()
    {
        { nameof(WarningReportView), new WarningReport(From, To, []) },
        { nameof(DepartmentLeaveReportView), new DepartmentLeaveReport(From, To, null, 0, null, []) },
        { nameof(LeaveListReportView), new LeaveListReport(From, To, null, []) },
        { nameof(EmployeeLeaveReportView), new EmployeeLeaveReport(From, To, "A001", "นาย ก", null, 400, []) },
        { nameof(LeaveSummaryReportView), new LeaveSummaryReport(From, To, null, [], new Dictionary<string, double>()) },
        { nameof(AbsentLeaveReportView), new AbsentLeaveReport(From, To, null, []) },
        { nameof(LateReportView), new LateReport(From, To, null, []) },
        { nameof(WorkHoursReportView), new WorkHoursReport(From, To, "ประกอบ", 0, null, []) },
        { nameof(OvertimeReportView), new OvertimeReport(From, []) },
        { nameof(AttendanceReportView), new AttendanceReport(From, To, null, []) },
        { nameof(AbsenceReportView), new AbsenceReport(From, [], 100) },
    };

    [Theory, MemberData(nameof(EmptyReports))] // D-6: ไม่มีข้อมูล → "ไม่มีข้อมูล" ไม่พิมพ์แถวว่างเลข 1
    public async Task Every_report_without_rows_says_there_is_no_data(string view, object report)
    {
        var html = view switch
        {
            nameof(WarningReportView) => await HtmlAsync<WarningReportView>(report),
            nameof(DepartmentLeaveReportView) => await HtmlAsync<DepartmentLeaveReportView>(report),
            nameof(LeaveListReportView) => await HtmlAsync<LeaveListReportView>(report),
            nameof(EmployeeLeaveReportView) => await HtmlAsync<EmployeeLeaveReportView>(report),
            nameof(LeaveSummaryReportView) => await HtmlAsync<LeaveSummaryReportView>(report),
            nameof(AbsentLeaveReportView) => await HtmlAsync<AbsentLeaveReportView>(report),
            nameof(LateReportView) => await HtmlAsync<LateReportView>(report),
            nameof(WorkHoursReportView) => await HtmlAsync<WorkHoursReportView>(report),
            nameof(OvertimeReportView) => await HtmlAsync<OvertimeReportView>(report),
            nameof(AttendanceReportView) => await HtmlAsync<AttendanceReportView>(report),
            _ => await HtmlAsync<AbsenceReportView>(report),
        };

        Assert.Contains("ไม่มีข้อมูล", html);
        Assert.DoesNotContain("<tbody>", html);
    }

    [Fact] // RPT-30: เรื่อง ยาวพิมพ์ครบ (ตัดบรรทัด D-1), "รวมทั้งหมด n รายการ"
    public async Task The_warning_report_keeps_the_whole_subject_and_counts_the_rows()
    {
        var subject = string.Concat(Enumerable.Repeat("มาสายและออกก่อนเวลาโดยไม่แจ้งหัวหน้า ", 6)).Trim();
        var html = await HtmlAsync<WarningReportView>(new WarningReport(From, To,
            [new WarningReportLine(new(2026, 9, 3), "A001", "นาย ก", "ประกอบ", "ใบเตือนด้วยวาจา", subject)]));

        Assert.Contains(subject, html);
        Assert.Contains("ตั้งแต่วันที่ 1/9/2569 ถึงวันที่ 30/9/2569", html);
        Assert.Contains("รวมทั้งหมด 1 รายการ", html);
    }

    [Fact] // RPT-31: ทศนิยม 1 (2.875 → 2.9), ไม่ลา = 0.0, 16 ประเภท + รวม SR 1–15, ไม่มีวันทำงาน → % ว่าง, หัว "แผนก" + ทั้งหมด
    public async Task The_department_leave_report_prints_one_decimal_sixteen_types_and_a_blank_percent_without_work_days()
    {
        var report = new DepartmentLeaveReport(From, To, null, 5, null,
            [new DepartmentLeaveLine("A001", "นาย ก", new(2010, 1, 4), new Dictionary<string, double> { ["1"] = 2.875, ["16"] = 1 })]);

        var html = await HtmlAsync<DepartmentLeaveReportView>(report);

        Assert.Equal("รายงานการลางานแผนกทั้งหมด", DepartmentLeaveReportView.TitleOf(report));
        Assert.Contains(">2.9<", html);
        Assert.Contains(">0.0<", html);
        Assert.Contains("ลาเป็นโรคระบาด", html);
        Assert.Equal(4 + 16 + 1, System.Text.RegularExpressions.Regex.Matches(html, "<th>").Count); // ที่ รหัส ชื่อ เริ่มทำงาน + 16 + รวม
        Assert.Contains("<b>รวมทั้งหมด</b> 5 <b>คน</b>", html);
        Assert.Contains("ผู้อนุมัติ", html);
    }

    [Fact] // RPT-32: "0.50 วัน", ได้/ไม่ได้, H:mm, ชื่อวัน
    public async Task The_employee_leave_report_prints_days_paid_and_times_as_the_legacy()
    {
        var html = await HtmlAsync<EmployeeLeaveReportView>(new EmployeeLeaveReport(From, To, "A001", "นาย ก", new(2010, 1, 4), 400,
            [new EmployeeLeaveLine(new(2026, 9, 7), new(2026, 9, 7), new(8, 0), new(12, 0), 0.5, "ลากิจ", true, 200, null)]));

        Assert.Contains("0.50 วัน", html);
        Assert.Contains(">ได้<", html);
        Assert.Contains(">8:00<", html);
        Assert.Contains(">วันจันทร์<", html);
        Assert.Contains("ค่าแรง/วัน 400.00", html);
    }

    [Fact] // RPT-33: เดือนจากช่วงวันที่ (D-5), 15 ประเภท, %การลา ทศนิยม 2
    public async Task The_leave_summary_takes_the_month_from_the_dates_and_prints_fifteen_types()
    {
        var html = await HtmlAsync<LeaveSummaryReportView>(new LeaveSummaryReport(From, To, 26,
            [new LeaveSummaryLine("ตกแต่ง", 21, new Dictionary<string, double> { ["1"] = 10, ["2"] = 34.5, ["3"] = 19.125, ["6"] = 3, ["8"] = 2, ["14"] = 1 }, 20604)],
            new Dictionary<string, double>()));

        Assert.Contains("สรุปการลางานของพนักงานเดือน กันยายน 2569", html);
        Assert.Contains(">19.13<", html);
        Assert.Contains(">69.63<", html);
        Assert.Contains(">12.75<", html); // 69.625 / 21 / 26 × 100 as the legacy sample
        Assert.Contains(">20,604.00<", html);
        Assert.DoesNotContain("ลาป่วยเป็นโรคระบาด", html);
    }
}

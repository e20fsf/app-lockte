using System.Globalization;
using Lockte.Hr;
using Lockte.Web.Accounts;
using Lockte.Web.Reports.Hr;

namespace Lockte.Web.Reports;

/// <summary>
/// The PDFs of ใบเตือน, การลา and การมาทำงาน (#59): RPT-30, RPT-31 (and พิมพ์ลางานตามวันที่), RPT-32..34, RPT-12..15 and
/// RPT-17, all ระดับ B. Dates are ISO (<c>?from=2026-09-01&amp;to=2026-09-30</c>); a missing or reversed range is a 400.
/// Paper as the legacy (#40): A5 นอน for RPT-30/32/34, Letter → A4.
/// </summary>
public static class HrAttendanceReports
{
    public static RouteGroupBuilder MapHrAttendanceReports(this RouteGroupBuilder reports)
    {
        // RPT-30 รายงานพนักงานที่ได้รับใบเตือน (เมนู 4)
        reports.MapGet("/hr/warnings", async (DateOnly? from, DateOnly? to, HttpContext http, WarningDirectory warnings, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await warnings.GetReportAsync(from!.Value, to!.Value);
            return await PrintAsync<WarningReportView>(http, printer, WarningReportView.Title, Paper.A5Landscape, report,
                "ใบเตือน " + Iso(report.From), cancellationToken);
        });

        // RPT-31 รายงานการลางานแผนก (เมนู 17): ?department= one แผนก (none = ทุกแผนก), ?employees= จำนวนคน, ?workDays= จำนวนวันทำงาน
        reports.MapGet("/hr/leave/department", async (DateOnly? from, DateOnly? to, int? department, int? employees, int? workDays,
            HttpContext http, LeaveReportDirectory leave, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await leave.GetDepartmentReportAsync(from!.Value, to!.Value, department, employees, workDays);
            return await PrintAsync<DepartmentLeaveReportView>(http, printer, DepartmentLeaveReportView.TitleOf(report), Paper.A4Landscape, report,
                "ลางานแผนก " + (report.DepartmentName ?? "ทั้งหมด") + " " + Iso(report.From), cancellationToken);
        });

        // RPT-31 ปุ่ม "พิมพ์ลางานตามวันที่" (รายงาน พิมพ์ลางานทั้งแผนก100)
        reports.MapGet("/hr/leave/list", async (DateOnly? from, DateOnly? to, int? department, HttpContext http, LeaveReportDirectory leave,
            ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await leave.GetListReportAsync(from!.Value, to!.Value, department);
            return await PrintAsync<LeaveListReportView>(http, printer, LeaveListReportView.Title, Paper.A4Portrait, report,
                "ลางานตามวันที่ " + Iso(report.From), cancellationToken);
        });

        // RPT-32 การลางานของพนักงาน (รายคน)
        reports.MapGet("/hr/leave/employee/{id:int}", async (int id, DateOnly? from, DateOnly? to, HttpContext http, LeaveReportDirectory leave,
            ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            if (await leave.GetEmployeeReportAsync(id, from!.Value, to!.Value) is not { } report) return Results.NotFound("ไม่พบพนักงาน");
            return await PrintAsync<EmployeeLeaveReportView>(http, printer, EmployeeLeaveReportView.Title, Paper.A5Landscape, report,
                "ลางาน " + report.EmployeeCode + " " + Iso(report.From), cancellationToken);
        });

        // RPT-33 สรุปการลางานของพนักงานเดือน…: ?workDays= จำนวนวันทำงาน
        reports.MapGet("/hr/leave/summary", async (DateOnly? from, DateOnly? to, int? workDays, HttpContext http, LeaveReportDirectory leave,
            ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await leave.GetSummaryReportAsync(from!.Value, to!.Value, workDays);
            return await PrintAsync<LeaveSummaryReportView>(http, printer, LeaveSummaryReportView.Title, Paper.A4Landscape, report,
                "สรุปการลางาน " + Iso(report.From), cancellationToken);
        });

        // RPT-34 รายงานพนักงานที่ขาดงาน: ?employee= one employee (none = ทุกคน)
        reports.MapGet("/hr/leave/absent", async (DateOnly? from, DateOnly? to, int? employee, HttpContext http, LeaveReportDirectory leave,
            ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await leave.GetAbsentReportAsync(from!.Value, to!.Value, employee);
            return await PrintAsync<AbsentLeaveReportView>(http, printer, AbsentLeaveReportView.Title, Paper.A5Landscape, report,
                "ขาดงาน " + Iso(report.From), cancellationToken);
        });

        // RPT-12 รายงานพนักงานที่มาทำงานสาย (เมนู 3): ?employee=
        reports.MapGet("/hr/time/late", async (DateOnly? from, DateOnly? to, int? employee, HttpContext http, AttendanceReportDirectory attendance,
            ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await attendance.GetLateReportAsync(from!.Value, to!.Value, employee);
            return await PrintAsync<LateReportView>(http, printer, LateReportView.Title, Paper.A4Portrait, report,
                "มาสาย " + Iso(report.From), cancellationToken);
        });

        // RPT-13 สรุปชั่วโมงการทำงาน (เมนู 3): ?department= (required), ?employees= จำนวนพนักงาน, ?workDays= จำนวนวันทำงานทั้งหมด
        reports.MapGet("/hr/time/hours", async (DateOnly? from, DateOnly? to, int? department, int? employees, int? workDays, HttpContext http,
            AttendanceReportDirectory attendance, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            if (department is not { } departmentId) return Results.BadRequest("กรุณาเลือกแผนก");
            if (await attendance.GetWorkHoursReportAsync(from!.Value, to!.Value, departmentId, employees, workDays) is not { } report)
                return Results.NotFound("ไม่พบแผนก");
            return await PrintAsync<WorkHoursReportView>(http, printer, WorkHoursReportView.Title, Paper.A4Portrait, report,
                "สรุปชั่วโมงทำงาน " + report.DepartmentName + " " + Iso(report.From), cancellationToken);
        });

        // RPT-14 รายงานพนักงานทำงานล่วงเวลาจริง (เมนู 7): ?date=
        reports.MapGet("/hr/time/overtime", async (DateOnly? date, HttpContext http, AttendanceReportDirectory attendance, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (date is not { } day) return Results.BadRequest("กรุณากรอกวันที่");
            var report = await attendance.GetOvertimeReportAsync(day);
            return await PrintAsync<OvertimeReportView>(http, printer, OvertimeReportView.Title, Paper.A4Portrait, report,
                "ทำล่วงเวลาจริง " + Iso(day), cancellationToken);
        });

        // RPT-15 รายงานการมาทำงาน (เมนู 7): ?employee=
        reports.MapGet("/hr/time/attendance", async (DateOnly? from, DateOnly? to, int? employee, HttpContext http,
            AttendanceReportDirectory attendance, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var report = await attendance.GetAttendanceReportAsync(from!.Value, to!.Value, employee);
            return await PrintAsync<AttendanceReportView>(http, printer, AttendanceReportView.Title, Paper.A4Portrait, report,
                "การมาทำงาน " + Iso(report.From), cancellationToken);
        });

        // RPT-17 รายงานพนักงานที่ไม่มาทำงาน (SCR-10): ?date=
        reports.MapGet("/hr/time/absences", async (DateOnly? date, HttpContext http, AbsenceDirectory absences, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (date is not { } day) return Results.BadRequest("กรุณากรอกวันที่");
            var report = await absences.GetDailyReportAsync(day);
            return await PrintAsync<AbsenceReportView>(http, printer, AbsenceReportView.Title, Paper.A4Portrait, report,
                "ไม่มาทำงาน " + Iso(day), cancellationToken);
        });

        return reports;
    }

    /// <summary>Both dates given, in order.</summary>
    private static IResult? Range(DateOnly? from, DateOnly? to)
        => from is null || to is null ? Results.BadRequest("กรุณากรอกตั้งแต่วันที่และถึงวันที่")
            : from > to ? Results.BadRequest("ตั้งแต่วันที่ต้องไม่หลังถึงวันที่") : null;

    private static async Task<IResult> PrintAsync<TView>(HttpContext http, ReportPrinter printer, string title, Paper paper, object report,
        string fileName, CancellationToken cancellationToken) where TView : Microsoft.AspNetCore.Components.IComponent
    {
        var pdf = await printer.PrintAsync<TView>(title, paper, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User),
            cancellationToken: cancellationToken);
        return ReportsRegistration.Pdf(http, pdf, fileName);
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

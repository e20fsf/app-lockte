using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel;

namespace Lockte.Web.Tests;

/// <summary>
/// The rows of the leave and attendance reports (#59: RPT-31..34 BR-064, RPT-12..15 BR-066/062, RPT-17 BR-061): the legacy
/// queries' filters, joins and sums, ⚠ quirks included. Each test works in departments and a year of its own, so the rows of
/// the other tests (which share the database) do not reach its numbers.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class AttendanceReportTests(LocalDbFixture db)
{
    private static int lastCode;
    private static int lastYear = 2100;
    private static int lastPeriodYear = 1700;

    /// <summary>"J" keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)).</summary>
    private static string NewCode() => $"M{Interlocked.Increment(ref lastCode):D3}";

    /// <summary>A year no other test uses: the reports read every row of their dates.</summary>
    private static int NewYear() => Interlocked.Increment(ref lastYear);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static void Ok(ServiceResult result) => Assert.True(result.Succeeded, string.Join(", ", result.Errors));

    private async Task<DepartmentDto> NewDepartmentAsync(string name = "แผนก")
    {
        var sectionId = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null)))).Id!.Value;
        var id = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveDepartmentAsync(new DepartmentInput(
            null, Unique("V"), Unique(name), "V", 9, sectionId, null, [], null)))).Id!.Value;
        return (await AsHrAsync(s => s.Get<OrgDirectory>().GetDepartmentAsync(id)))!;
    }

    private async Task<EmployeeDto> NewEmployeeAsync(DepartmentDto? department, DateOnly? resign = null, string? code = null, double dailyWage = 400)
    {
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = code ?? NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], DepartmentId = department?.Id,
            SectionId = department?.SectionId, StartDate = D(2015, 3, 1), ResignDate = resign, DailyWage = dailyWage,
            EmployeeType = EmployeeTypes.Daily,
        }));
        Ok(result);
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private async Task LeaveAsync(EmployeeDto employee, DateOnly from, double days, string type, bool? paid = false, DateOnly? to = null,
        string? remark = null)
    {
        var result = await AsHrAsync(s => s.Get<LeaveRegistry>().SaveAsync(new LeaveInput(null, employee.Id, from, from, to ?? from,
            null, null, days, type, paid, remark, null, null)));
        Ok(result.Save);
    }

    private async Task WorkAsync(EmployeeDto employee, DateOnly date, string type = "W", string workIn = "08:00", string workOut = "17:00",
        int rest = 60, string? late = null)
        => Ok(await AsHrAsync(s => s.Get<WorkTimeRegistry>().SaveAsync(new WorkTimeInput(null, date, employee.Id, type,
            TimeOnly.Parse(workIn), TimeOnly.Parse(workOut), rest, late is null ? null : TimeOnly.Parse(late), null, null))));

    private Task<T> LeaveReportAsync<T>(Func<LeaveReportDirectory, Task<T>> query) => AsHrAsync(s => query(s.Get<LeaveReportDirectory>()));

    private Task<T> AttendanceAsync<T>(Func<AttendanceReportDirectory, Task<T>> query) => AsHrAsync(s => query(s.Get<AttendanceReportDirectory>()));

    // ---------- RPT-31 ลางานทั้งแผนก ----------

    [Fact] // BR-064: ทุกคนที่ยังทำงานในแผนก (ไม่ลา = 0), วันลาแยก SR 1–16, รวม = SR 1–15, ลาคร่อมวันที่ไม่นับ, เรียงตามรหัส
    public async Task The_department_report_has_every_working_employee_with_days_by_type_and_a_total_of_types_1_to_15()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var other = await NewDepartmentAsync();
        var second = await NewEmployeeAsync(department, code: $"Y{year % 100:D2}2");
        var first = await NewEmployeeAsync(department, code: $"Y{year % 100:D2}1");
        var resigned = await NewEmployeeAsync(department, resign: D(year, 1, 1));
        var elsewhere = await NewEmployeeAsync(other);
        await LeaveAsync(first, D(year, 3, 2), 1.25, "1");
        await LeaveAsync(first, D(year, 3, 9), 1.625, "1");
        await LeaveAsync(first, D(year, 4, 1), 2, "6");
        await LeaveAsync(first, D(year, 4, 2), 1, "16");
        await LeaveAsync(first, D(year, 6, 30), 2, "3", to: D(year, 7, 1)); // TO_D after the dates: not counted
        await LeaveAsync(resigned, D(year, 3, 3), 1, "1");
        await LeaveAsync(elsewhere, D(year, 3, 3), 1, "1");

        var report = await LeaveReportAsync(r => r.GetDepartmentReportAsync(D(year, 1, 1), D(year, 6, 30), department.Id, null, 20));

        Assert.Equal(department.Name, report.DepartmentName);
        Assert.Equal(2, report.Employees);
        Assert.Equal([first.Code, second.Code], report.Lines.Select(l => l.EmployeeCode));
        var line = report.Lines[0];
        Assert.Equal((2.875, 2.0, 1.0, 0.0), (line.DaysOf("1"), line.DaysOf("6"), line.DaysOf("16"), line.DaysOf("3")));
        Assert.Equal(4.875, line.Total);
        Assert.Equal(0, report.Lines[1].Total);
        Assert.Equal(4.875 / 2 / 20 * 100, report.Percent(report.Total));
    }

    [Fact] // EMP_QTY typed on the form wins; without จำนวนวันทำงาน the % row is blank (as the legacy sample)
    public async Task The_department_report_takes_the_typed_head_count_and_leaves_the_percent_blank_without_work_days()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        await NewEmployeeAsync(department);

        var report = await LeaveReportAsync(r => r.GetDepartmentReportAsync(D(year, 1, 1), D(year, 12, 31), department.Id, 7, null));

        Assert.Equal(7, report.Employees);
        Assert.Null(report.Percent(report.Total));
    }

    [Fact] // DEP "*": everyone with a แผนก — a blank DEPARTMENT is not LIKE "*"
    public async Task Every_department_leaves_out_employees_without_one()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var inDepartment = await NewEmployeeAsync(department);
        var withoutDepartment = await NewEmployeeAsync(null);
        await LeaveAsync(withoutDepartment, D(year, 2, 2), 1, "1");

        var report = await LeaveReportAsync(r => r.GetDepartmentReportAsync(D(year, 1, 1), D(year, 12, 31), null, null, null));

        Assert.Null(report.DepartmentName);
        Assert.Contains(report.Lines, l => l.EmployeeCode == inDepartment.Code);
        Assert.DoesNotContain(report.Lines, l => l.EmployeeCode == withoutDepartment.Code);
    }

    [Fact] // ปุ่ม "พิมพ์ลางานตามวันที่" (คิวรี พิมพ์ลางานทั้งแผนก100): the rows themselves, by รหัส then วันที่
    public async Task The_list_by_date_prints_the_leave_rows_by_code_then_date()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var second = await NewEmployeeAsync(department, code: $"Y{year % 100:D2}4");
        var first = await NewEmployeeAsync(department, code: $"Y{year % 100:D2}3");
        await LeaveAsync(second, D(year, 5, 1), 1, "2");
        await LeaveAsync(first, D(year, 5, 9), 0.5, "1");
        await LeaveAsync(first, D(year, 5, 2), 1, "8");

        var report = await LeaveReportAsync(r => r.GetListReportAsync(D(year, 5, 1), D(year, 5, 31), department.Id));

        Assert.Equal(
            [
                new LeaveListLine(first.Code, first.Name, 1, "8", department.Name),
                new LeaveListLine(first.Code, first.Name, 0.5, "1", department.Name),
                new LeaveListLine(second.Code, second.Name, 1, "2", department.Name),
            ],
            report.Lines);
    }

    // ---------- RPT-32 ลางานรายคน ----------

    [Fact] // คิวรี ลางานรายคน: FROM_D within the dates, by FROM_D; ⚠ a leave with ได้ค่าแรง blank is not printed (inner join YES,NO)
    public async Task The_employee_report_lists_the_leaves_by_date_without_the_ones_with_paid_blank()
    {
        var year = NewYear();
        var employee = await NewEmployeeAsync(await NewDepartmentAsync(), dailyWage: 380);
        await LeaveAsync(employee, D(year, 6, 1), 0.5, "2", paid: true, remark: "ไข้");
        await LeaveAsync(employee, D(year, 2, 3), 1, "1", paid: false);
        await LeaveAsync(employee, D(year, 3, 4), 1, "1", paid: null);
        await LeaveAsync(employee, D(year + 1, 1, 2), 1, "1", paid: false);

        var report = (await LeaveReportAsync(r => r.GetEmployeeReportAsync(employee.Id, D(year, 1, 1), D(year, 12, 31))))!;

        Assert.Equal((employee.Code, 380.0), (report.EmployeeCode, report.DailyWage));
        Assert.Equal([D(year, 2, 3), D(year, 6, 1)], report.Lines.Select(l => l.FromDate));
        var sick = report.Lines[1];
        Assert.Equal(("ลาป่วยมีใบแพทย์", "ได้", 190.0, "ไข้"), (sick.TypeName, sick.PaidName, sick.Pay, sick.Remark));
        Assert.Equal("ไม่ได้", report.Lines[0].PaidName);
        Assert.Equal((1.5, 190.0), (report.TotalDays, report.TotalPay));
    }

    [Theory]
    [InlineData(2026, 10, 19, "วันจันทร์")]
    [InlineData(2026, 10, 24, "วันเสาร์")]
    [InlineData(2026, 10, 25, "วันอาทิตย์")]
    public void The_employee_report_names_the_day_of_the_week(int year, int month, int day, string name)
        => Assert.Equal(name, new EmployeeLeaveLine(D(year, month, day), D(year, month, day), default, default, 1, "", false, 0, null).DayName);

    // ---------- RPT-33 สรุปการลางาน ----------

    [Fact] // คิวรี พิมพ์สรุปการลางาน*: per แผนก, distinct rows per SR, รวมจ่ายค่าแรง of every row, คนลาออกยังนับวันลา, เรียง รวม มาก→น้อย
    public async Task The_summary_sums_distinct_leaves_by_department_and_type_and_orders_by_the_total()
    {
        var year = NewYear();
        var big = await NewDepartmentAsync("แผนกมาก");
        var small = await NewDepartmentAsync("แผนกน้อย");
        var gone = await NewDepartmentAsync("แผนกไม่มีคน");
        var a = await NewEmployeeAsync(big, dailyWage: 400);
        await NewEmployeeAsync(big);
        var resigned = await NewEmployeeAsync(big, resign: D(year, 1, 31), dailyWage: 300);
        var c = await NewEmployeeAsync(small, dailyWage: 500);
        var nobodyLeft = await NewEmployeeAsync(gone, resign: D(year, 1, 31));
        await LeaveAsync(a, D(year, 3, 2), 1, "2", paid: true);
        await LeaveAsync(a, D(year, 3, 2), 1, "2", paid: true); // the same row again: its days count once
        await LeaveAsync(a, D(year, 3, 10), 2, "8", paid: false);
        await LeaveAsync(a, D(year, 3, 11), 1, "16", paid: true); // SR 16: not a column, but in รวมจ่ายค่าแรง of the แผนก
        await LeaveAsync(resigned, D(year, 3, 5), 0.5, "1", paid: true);
        await LeaveAsync(c, D(year, 3, 6), 1, "3", paid: false);
        await LeaveAsync(nobodyLeft, D(year, 3, 6), 4, "3", paid: false);
        await LeaveAsync(c, D(year, 3, 31), 1, "3", paid: false, to: D(year, 4, 1)); // across the end: left out

        var report = await LeaveReportAsync(r => r.GetSummaryReportAsync(D(year, 3, 1), D(year, 3, 31), 26));

        var mine = report.Lines.Where(l => l.DepartmentName == big.Name || l.DepartmentName == small.Name).ToList();
        Assert.Equal([big.Name, small.Name], mine.Select(l => l.DepartmentName));
        Assert.DoesNotContain(report.Lines, l => l.DepartmentName == gone.Name); // no working employee
        var line = mine[0];
        Assert.Equal(2, line.Employees);
        Assert.Equal((1.0, 2.0, 0.5, 0.0), (line.DaysOf("2"), line.DaysOf("8"), line.DaysOf("1"), line.DaysOf("16")));
        Assert.Equal(3.5, line.Total);
        Assert.Equal(400 + 400 + 400 + 150, line.Pay);
        Assert.Equal(3.5 / 2 / 26 * 100, report.Percent(line.Total, line.Employees));
        Assert.Equal((400.0, 150.0), (report.PayOf("2"), report.PayOf("1")));
        Assert.Equal((1, 1.0, 0.0), (mine[1].Employees, mine[1].DaysOf("3"), mine[1].Pay));
        Assert.All(report.Lines.Except(mine), l => Assert.Equal(0, l.Total)); // other tests' แผนก: nothing in this year
    }

    // ---------- RPT-34 ขาดงาน ----------

    [Fact] // คิวรี ขาดงาน2: SR 8 with FROM_D within the dates (resigned too), one employee or all, by FROM_D then รหัส
    public async Task The_absent_report_lists_type_8_by_date_then_code()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var b = await NewEmployeeAsync(department, code: $"Y{year % 100:D2}6");
        var a = await NewEmployeeAsync(department, resign: D(year, 12, 1), code: $"Y{year % 100:D2}5");
        await LeaveAsync(b, D(year, 4, 2), 1, "8");
        await LeaveAsync(a, D(year, 4, 2), 0.5, "8");
        await LeaveAsync(a, D(year, 4, 1), 2, "8", to: D(year, 4, 2));
        await LeaveAsync(a, D(year, 4, 3), 1, "1");
        await LeaveAsync(a, D(year, 5, 1), 1, "8");

        var all = await LeaveReportAsync(r => r.GetAbsentReportAsync(D(year, 4, 1), D(year, 4, 30), null));
        var one = await LeaveReportAsync(r => r.GetAbsentReportAsync(D(year, 4, 1), D(year, 4, 30), b.Id));

        Assert.Equal(
            [
                new AbsentLeaveLine(D(year, 4, 1), D(year, 4, 2), 2, a.Code, a.Name),
                new AbsentLeaveLine(D(year, 4, 2), D(year, 4, 2), 0.5, a.Code, a.Name),
                new AbsentLeaveLine(D(year, 4, 2), D(year, 4, 2), 1, b.Code, b.Name),
            ],
            all.Lines);
        Assert.Equal((b.Code, 1), (one.EmployeeCode, one.Lines.Count));
    }

    // ---------- RPT-12 มาสาย / RPT-14 OT จริง / RPT-15 การมาทำงาน ----------

    [Fact] // คิวรี มาสาย: LATE set, by วันที่ then รหัส; นาที = ⚠ Minute(LATE)
    public async Task The_late_report_lists_late_rows_with_the_minute_part_of_the_late_time()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var b = await NewEmployeeAsync(department, code: $"Y{year % 100:D2}8");
        var a = await NewEmployeeAsync(department, code: $"Y{year % 100:D2}7");
        await WorkAsync(b, D(year, 7, 1), late: "08:15");
        await WorkAsync(a, D(year, 7, 1), late: "09:05");
        await WorkAsync(a, D(year, 6, 30), late: "08:40");
        await WorkAsync(a, D(year, 7, 2));

        var report = await AttendanceAsync(r => r.GetLateReportAsync(D(year, 6, 1), D(year, 7, 31), null));

        Assert.Equal([(D(year, 6, 30), a.Code, 40), (D(year, 7, 1), a.Code, 5), (D(year, 7, 1), b.Code, 15)],
            report.Lines.Select(l => (l.Date, l.EmployeeCode, l.Minutes)));
        Assert.Equal(60, report.TotalMinutes);
        Assert.Single((await AttendanceAsync(r => r.GetLateReportAsync(D(year, 6, 1), D(year, 7, 31), b.Id))).Lines);
    }

    [Fact] // คิวรี ทำล่วงเวลาจริง: the day's rows that are not W, by รหัส
    public async Task The_overtime_report_lists_the_days_rows_that_are_not_W()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var b = await NewEmployeeAsync(department, code: $"Y{year % 100:D2}D");
        var a = await NewEmployeeAsync(department, code: $"Y{year % 100:D2}C");
        var day = D(year, 8, 2);
        await WorkAsync(b, day, "S", "08:00", "17:00");
        await WorkAsync(a, day, "O1", "17:30", "20:30", 0);
        await WorkAsync(a, day);
        await WorkAsync(a, day.AddDays(1), "O1", "17:30", "19:30", 0);

        var report = await AttendanceAsync(r => r.GetOvertimeReportAsync(day));

        Assert.Equal(
            [
                new OvertimeReportLine(day, a.Code, a.Name, department.Name, new TimeOnly(17, 30), new TimeOnly(20, 30)),
                new OvertimeReportLine(day, b.Code, b.Name, department.Name, new TimeOnly(8, 0), new TimeOnly(17, 0)),
            ],
            report.Lines);
    }

    [Fact] // คิวรี รายงานการมาทำงาน: W rows by รหัส then วันที่, จำนวนวัน = WORK_T (BR-001), รวมทั้งหมด Σ วัน
    public async Task The_attendance_report_lists_W_rows_by_code_then_date_with_their_days()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var b = await NewEmployeeAsync(department, code: $"Y{year % 100:D2}B");
        var a = await NewEmployeeAsync(department, code: $"Y{year % 100:D2}A");
        await WorkAsync(b, D(year, 9, 1));
        await WorkAsync(a, D(year, 9, 2), workIn: "08:00", workOut: "12:00", rest: 0);
        await WorkAsync(a, D(year, 9, 1), workIn: "08:00", workOut: "16:45");
        await WorkAsync(a, D(year, 9, 1), "O1", "17:00", "19:00", 0);

        var report = await AttendanceAsync(r => r.GetAttendanceReportAsync(D(year, 9, 1), D(year, 9, 30), null));

        Assert.Equal([(a.Code, D(year, 9, 1), 1.0), (a.Code, D(year, 9, 2), 0.5), (b.Code, D(year, 9, 1), 1.0)],
            report.Lines.Select(l => (l.EmployeeCode, l.Date, l.Days!.Value)));
        Assert.Equal(2.5, report.TotalDays);
        Assert.Equal(department.Name, report.Lines[0].DepartmentName);
    }

    // ---------- RPT-13 สรุปชั่วโมงการทำงาน ----------

    [Fact] // W and OT as the legacy history kept them (Long: half to even), TT_OT = RND-A(S+O2+H+O3); periods within the dates only
    public async Task The_work_hours_report_sums_the_periods_rounded_as_the_legacy_history()
    {
        var year = Interlocked.Increment(ref lastPeriodYear);
        var department = await NewDepartmentAsync();
        var other = await NewDepartmentAsync();
        var employee = await NewEmployeeAsync(department);
        var resigned = await NewEmployeeAsync(department, resign: D(year, 12, 31));
        var elsewhere = await NewEmployeeAsync(other);

        // งวด 1: W 12.5 → 12, O1 2.5 → 2, S 3.5 → RND-A 4
        var first = await NewPeriodAsync(D(year, 1, 1), D(year, 1, 15));
        for (var day = 1; day <= 12; day++) await WorkAsync(employee, D(year, 1, day));
        await WorkAsync(employee, D(year, 1, 13), workIn: "08:00", workOut: "12:00", rest: 0);
        await WorkAsync(employee, D(year, 1, 2), "O1", "17:00", "19:30", 0);
        await WorkAsync(employee, D(year, 1, 14), "S", "08:00", "11:30", 0);
        await WorkAsync(resigned, D(year, 1, 2));
        await WorkAsync(elsewhere, D(year, 1, 2));
        // งวด 2: W 13.5 → 14, O1 1.5 → 2
        var second = await NewPeriodAsync(D(year, 1, 16), D(year, 1, 31));
        for (var day = 16; day <= 28; day++) await WorkAsync(employee, D(year, 1, day));
        await WorkAsync(employee, D(year, 1, 29), workIn: "08:00", workOut: "12:00", rest: 0);
        await WorkAsync(employee, D(year, 1, 16), "O1", "17:00", "18:30", 0);
        // งวด 3 ends after the dates: left out
        var third = await NewPeriodAsync(D(year, 2, 1), D(year, 2, 15));
        await WorkAsync(employee, D(year, 2, 2));
        foreach (var period in new[] { first, second, third })
            Ok(await AsHrAsync(s => s.Get<PayPeriodRegistry>().CalculateTimeAsync(period)));

        var report = (await AttendanceAsync(r => r.GetWorkHoursReportAsync(D(year, 1, 1), D(year, 2, 10), department.Id, null, 26)))!;

        var line = Assert.Single(report.Lines);
        Assert.Equal((employee.Code, 26.0, 208.0, 8.0), (line.EmployeeCode, line.Days, line.Hours, line.OvertimeHours));
        Assert.Equal(8.0 / 26 / 8 * 100, line.OvertimePercent);
        Assert.Equal((department.Name, 1, 26), (report.DepartmentName, report.Employees, report.WorkDays!.Value));
    }

    private async Task<int> NewPeriodAsync(DateOnly from, DateOnly to)
    {
        var result = await AsHrAsync(s => s.Get<PayPeriodRegistry>().SaveAsync(new PayPeriodInput(null, from, to, to.AddDays(5), null)));
        Ok(result);
        return result.Id!.Value;
    }

    // ---------- RPT-17 ไม่มาทำงาน ----------

    [Fact] // BR-061: the PDF reads the same rows as SCR-10's screen report
    public async Task The_absence_report_counts_the_absent_against_everyone_working()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var employee = await NewEmployeeAsync(department);
        Ok(await AsHrAsync(s => s.Get<AbsenceRegistry>().SaveAsync(new AbsenceInput(null, D(year, 3, 3), employee.Id, null, "ป่วย", null))));

        var report = await AsHrAsync(s => s.Get<AbsenceDirectory>().GetDailyReportAsync(D(year, 3, 3)));

        var line = Assert.Single(report.Lines);
        Assert.Equal((employee.Code, department.Name, "ป่วย"), (line.EmployeeCode, line.DepartmentName, line.Remark));
        Assert.Equal(report.WorkingEmployees - 1, report.Present);
    }
}

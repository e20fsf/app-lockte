using System.Net;
using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;
using Microsoft.AspNetCore.Components;

namespace Lockte.Web.Tests;

/// <summary>
/// #67: BR-032 ปรับค่าแรง (SCR-52, RPT-37) and BR-055 / BR-067 การประเมินผล (SCR-53, SCR-54, RPT-43). The reports read every
/// working employee, so each test looks only at its own employees, and their leaves, late days and ใบเตือน are in years
/// (1500+) no other test uses.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class PayAdjustmentEvaluationTests(LocalDbFixture db)
{
    private static int lastCode;
    private static int lastYear = 1500; // after VacationPayTests' (1300+), before AttendanceReportTests' (1700+)

    /// <summary>"Y" keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)).</summary>
    private static string NewCode() => $"Y{Interlocked.Increment(ref lastCode):D3}";

    private static int NewYear() => Interlocked.Increment(ref lastYear);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static readonly double[] Steps = [2.83, 2.83, 4, 5, 6];

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static void Ok(ServiceResult result) => Assert.True(result.Succeeded, string.Join(", ", result.Errors));

    private async Task<DepartmentDto> NewDepartmentAsync()
    {
        var sectionId = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null)))).Id!.Value;
        var id = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveDepartmentAsync(new DepartmentInput(
            null, Unique("Y"), Unique("แผนก"), "Y", 9, sectionId, null, [], null)))).Id!.Value;
        return (await AsHrAsync(s => s.Get<OrgDirectory>().GetDepartmentAsync(id)))!;
    }

    private async Task<EmployeeDto> NewEmployeeAsync(DepartmentDto? department, int type = EmployeeTypes.Daily, double salary = 0,
        double dailyWage = 400, DateOnly? start = null, DateOnly? resign = null, string? code = null, string? position = null)
    {
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = code ?? NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], DepartmentId = department?.Id,
            SectionId = department?.SectionId, StartDate = start, ResignDate = resign, Salary = salary, DailyWage = dailyWage,
            EmployeeType = type, Position = position,
        }));
        Ok(result);
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private async Task LeaveAsync(EmployeeDto employee, DateOnly from, double days, string type = "1", DateOnly? to = null)
        => Ok((await AsHrAsync(s => s.Get<LeaveRegistry>().SaveAsync(new LeaveInput(null, employee.Id, from, from, to ?? from,
            null, null, days, type, false, null, null, null)))).Save);

    private Task WarnAsync(EmployeeDto employee, DateOnly date, string level)
        => AsHrAsync(async s => { Ok(await s.Get<WarningRegistry>().SaveAsync(new WarningInput(null, date, employee.Id, level, null, null))); return 0; });

    private Task LateAsync(EmployeeDto employee, DateOnly date, string type = "W")
        => AsHrAsync(async s =>
        {
            Ok(await s.Get<WorkTimeRegistry>().SaveAsync(new WorkTimeInput(null, date, employee.Id, type, new TimeOnly(8, 0), new TimeOnly(17, 0),
                60, new TimeOnly(8, 20), null, null)));
            return 0;
        });

    private async Task<(IReadOnlyList<PayAdjustmentLine> Monthly, IReadOnlyList<PayAdjustmentLine> Daily)> AdjustAsync(int year,
        DepartmentDto? department, params EmployeeDto[] employees)
    {
        var report = await AsHrAsync(s => s.Get<PayAdjustmentDirectory>().GetReportAsync(
            new PayAdjustmentInput(D(year, 1, 1), D(year, 12, 31), department?.Id, 25, Steps)));
        var codes = employees.Select(e => e.Code).ToHashSet();
        return (report.Monthly.Where(l => codes.Contains(l.Code)).ToList(), report.Daily.Where(l => codes.Contains(l.Code)).ToList());
    }

    [Fact] // BR-032 ตัวอย่าง 1–2: รายเดือน 15,000 อายุงาน 3 ปี ลา 10 → +600 = 15,600; ลา 25 พอดี → +0
    public async Task Monthly_gets_the_step_of_its_service_unless_it_reached_the_leave_cap()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var three = await NewEmployeeAsync(department, EmployeeTypes.Monthly, 15000, 500, start: D(year - 3, 12, 31).AddDays(-30));
        var capped = await NewEmployeeAsync(department, EmployeeTypes.Monthly, 15000, 500, start: D(year - 3, 6, 1));
        await LeaveAsync(three, D(year, 3, 2), 10);
        await LeaveAsync(capped, D(year, 3, 2), 20, "2");
        await LeaveAsync(capped, D(year, 5, 2), 5, "8");

        var (monthly, daily) = await AdjustAsync(year, department, three, capped);

        Assert.Empty(daily);
        Assert.Equal([three.Code, capped.Code], monthly.Select(l => l.Code));
        Assert.Equal((15000.0, 10.0, 4.0, 600.0, 15600.0), (monthly[0].Wage, monthly[0].LeaveDays, monthly[0].Rate, monthly[0].Increase, monthly[0].NewWage));
        Assert.Equal((25.0, 4.0, 0.0, 15000.0), (monthly[1].LeaveDays, monthly[1].Rate, monthly[1].Increase, monthly[1].NewWage));
        Assert.Equal((D(year, 12, 31).DayNumber - three.StartDate!.Value.DayNumber) / 365.0, monthly[0].ServiceYears);
    }

    [Fact] // BR-032 ตัวอย่าง 3–5: รายวัน 400 อายุงาน 5 ปี วาจา 1 → 16; 0.5 ปี → 11.32 → 11; 0.5 ปี หนังสือ 2 → ติดลบ → 0
    public async Task Daily_takes_one_percent_per_verbal_and_two_per_written_warning_never_below_zero()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var five = await NewEmployeeAsync(department, start: D(year - 5, 6, 1));
        var half = await NewEmployeeAsync(department, start: D(year, 7, 1));
        var warned = await NewEmployeeAsync(department, start: D(year, 7, 1));
        await WarnAsync(five, D(year, 4, 1), WarningLevels.Verbal);
        await WarnAsync(warned, D(year, 8, 1), WarningLevels.Written);
        await WarnAsync(warned, D(year, 9, 1), WarningLevels.Written);

        var (_, daily) = await AdjustAsync(year, department, five, half, warned);

        Assert.Equal([(1, 0, 5.0, 16.0, 416.0), (0, 0, 2.83, 11.0, 411.0), (0, 2, 2.83, 0.0, 400.0)],
            daily.Select(l => (l.VerbalWarnings, l.WrittenWarnings, l.Rate, l.Increase, l.NewWage)));
    }

    [Fact] // ⚠ Q-31: ทุกแผนก → ใบเตือนหนังสือไม่ถูกนับ (DEPART = "*"); ใบเตือนวาจานับทุกแผนก; ⚠ คิวรี M1 GROUP BY วันที่: ใบเดียวกันวันเดียวนับครั้งเดียว
    public async Task Written_warnings_count_only_for_one_department_and_same_day_warnings_once()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var employee = await NewEmployeeAsync(department, start: D(year - 10, 1, 1));
        await WarnAsync(employee, D(year, 2, 1), WarningLevels.Verbal);
        await WarnAsync(employee, D(year, 2, 1), WarningLevels.Verbal);
        await WarnAsync(employee, D(year, 3, 1), WarningLevels.Verbal);
        await WarnAsync(employee, D(year, 4, 1), WarningLevels.Written);
        await WarnAsync(employee, D(year + 1, 1, 1), WarningLevels.Written); // after the range

        var all = Assert.Single((await AdjustAsync(year, null, employee)).Daily);
        var one = Assert.Single((await AdjustAsync(year, department, employee)).Daily);

        Assert.Equal((2, 0, 16.0), (all.VerbalWarnings, all.WrittenWarnings, all.Increase)); // 400 × (6 − 2) / 100
        Assert.Equal((2, 1, 8.0), (one.VerbalWarnings, one.WrittenWarnings, one.Increase)); // 400 × (6 − 2 − 2) / 100
    }

    [Fact] // วันลา: SR 1–15 ยกเว้น 5 (มาโครไม่รันคิวรี 8) · ใบที่คร่อมช่วงไม่นับ · Round 2 ตำแหน่ง
    public async Task Counts_every_leave_type_but_work_accidents_within_the_dates()
    {
        var year = NewYear();
        var employee = await NewEmployeeAsync(null, start: D(year - 1, 6, 1));
        await LeaveAsync(employee, D(year, 2, 2), 1.125, "1");
        await LeaveAsync(employee, D(year, 2, 3), 2, "15");
        await LeaveAsync(employee, D(year, 2, 4), 3, "5");
        await LeaveAsync(employee, D(year, 2, 5), 4, "16");
        await LeaveAsync(employee, D(year, 12, 31), 2, to: D(year + 1, 1, 1));

        var line = Assert.Single((await AdjustAsync(year, null, employee)).Daily);

        Assert.Equal(3.12, line.LeaveDays); // Round(3.125, 2), banker's
    }

    [Fact] // ไม่รวม ลาออก / รหัส LT / แผนกอื่นแม้มีใบเตือนวาจา (คิวรี M1 ดึงมา — ผู้ใช้ตัดสิน 2026-10-21 ไม่ดึง) · ไม่มีวันเริ่มงาน → STEP5 (IIf Null) · ไม่แก้ข้อมูลพนักงาน (Q-32)
    public async Task Leaves_out_resigned_LT_and_other_departments_and_changes_no_employee()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var other = await NewDepartmentAsync();
        var noStart = await NewEmployeeAsync(department, dailyWage: 412.5);
        var resigned = await NewEmployeeAsync(department, resign: D(year, 6, 30));
        var lt = await NewEmployeeAsync(department, code: "LTY1");
        var elsewhere = await NewEmployeeAsync(other);
        await WarnAsync(elsewhere, D(year, 3, 1), WarningLevels.Verbal);

        var (monthly, daily) = await AdjustAsync(year, department, noStart, resigned, lt, elsewhere);
        var everyone = (await AdjustAsync(year, null, elsewhere)).Daily;

        Assert.Empty(monthly);
        Assert.Equal([elsewhere.Code], everyone.Select(l => l.Code)); // on the report of all แผนก only (decided 2026-10-21)
        var line = Assert.Single(daily);
        Assert.Equal((noStart.Code, (double?)null, 6.0, 25.0, 412.0, 438.0), (line.Code, line.ServiceYears, line.Rate, line.Increase, line.Wage, line.NewWage));
        Assert.Equal(412.5, (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(noStart.Id)))!.DailyWage);
    }

    private static async Task<string> RenderAsync<TView>(Dictionary<string, object?> parameters) where TView : IComponent
        => WebUtility.HtmlDecode(await ReportBody.RenderAsync<TView>(parameters));

    [Fact] // RPT-37: คอลัมน์ ท้ายรายงาน และเกณฑ์ที่พิมพ์ (STEP2–5 จากพารามิเตอร์) · ปีของหัว = วันนี้ + 30 วัน
    public async Task The_pay_adjustment_report_prints_its_columns_totals_and_criteria()
    {
        var input = new PayAdjustmentInput(D(2026, 1, 1), D(2026, 9, 3), null, 25, [1, 2.5, 4, 5, 6]);
        var report = new PayAdjustmentReport(input,
            [new PayAdjustmentLine("Y900", "นาย ก", D(2020, 1, 1), 6.67, 15000, 10, 0, 0, 5, 750, 15750)],
            [new PayAdjustmentLine("Y901", "นาย ข", D(2025, 1, 1), 1.67, 400, 2.5, 1, 0, 2.5, 6, 406),
             new PayAdjustmentLine("Y902", "นาย ค", null, null, 500, 0, 0, 0, 6, 30, 530)]);

        var monthly = await RenderAsync<PayAdjustmentReportView>(new() { ["Report"] = report, ["Daily"] = false });
        var daily = await RenderAsync<PayAdjustmentReportView>(new() { ["Report"] = report, ["Daily"] = true });

        Assert.Contains("เงินเดือนใหม่", monthly);
        Assert.DoesNotContain("ด้วยวาจา</th>", monthly);
        Assert.Contains("15,750", monthly);
        Assert.Contains("รวมทั้งหมด 1 คน", monthly);
        Assert.Contains("5.00 %", monthly); // Avg(750 / 15000 × 100)
        Assert.Contains("3. อายุงานตั้งแต่ 1 ถึงน้อยกว่า 2 ปี ปรับ 2.50 %", monthly);
        Assert.Contains("2. อายุงานน้อยกว่า 1 ปี ไม่ปรับค่าแรง", monthly); // fixed text, as the legacy (Q-48)
        Assert.DoesNotContain("หัวหน้าแผนก", monthly);
        Assert.Contains("ด้วยวาจา</th>", daily);
        Assert.Contains("รวมทั้งหมด 2 คน", daily);
        Assert.Contains("เฉลี่ยอายุงาน 1.67 ปี", daily); // the one with a start date
        Assert.Contains("4.25 %", daily); // Avg(2.5, 6)
        Assert.Contains("936", daily);
        Assert.Contains("หัวหน้าแผนกซ่อมบำรุง", daily);
        Assert.Equal("รายละเอียดการปรับค่าแรงพนักงานรายวันปี 2570", PayAdjustmentReportView.TitleOf(true, D(2026, 12, 5)));
    }

    [Fact] // SCR-53 (BR-055): บันทึก / ล้าง ลงข้อมูลพนักงาน · RPT-43 รายงานการประเมินผลพนักงาน: DATE_1 มีค่าและยังไม่ลาออก
    public async Task Evaluations_are_saved_on_the_employee_and_listed_when_the_first_has_a_date()
    {
        var department = await NewDepartmentAsync();
        var evaluated = await NewEmployeeAsync(department, start: D(2026, 1, 5));
        var resigned = await NewEmployeeAsync(department, resign: D(2026, 6, 1));
        var notYet = await NewEmployeeAsync(department);
        var input = new EvaluationInput(D(2026, 2, 1), 72, " B ", D(2026, 4, 1), 80, "A", D(2026, 4, 5), " ผ่าน ");
        Ok(await AsHrAsync(s => s.Get<EvaluationRegistry>().SaveAsync(evaluated.Id, input, evaluated.RowVersion)));
        Ok(await AsHrAsync(s => s.Get<EvaluationRegistry>().SaveAsync(resigned.Id, input, resigned.RowVersion)));
        Ok(await AsHrAsync(s => s.Get<EvaluationRegistry>().SaveAsync(notYet.Id, input with { Evaluation1Date = null }, notYet.RowVersion)));

        var saved = (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(evaluated.Id)))!;
        var list = (await AsHrAsync(s => s.Get<EvaluationDirectory>().GetEvaluatedAsync()))
            .Where(l => l.Code == evaluated.Code || l.Code == resigned.Code || l.Code == notYet.Code).ToList();
        var tooLong = await AsHrAsync(s => s.Get<EvaluationRegistry>().SaveAsync(evaluated.Id, input with { Evaluation2Grade = new string('A', 51) },
            saved.RowVersion));
        Ok(await AsHrAsync(s => s.Get<EvaluationRegistry>().SaveAsync(evaluated.Id, new EvaluationInput(null, null, null, null, null, null, null, null),
            saved.RowVersion)));
        var cleared = (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(evaluated.Id)))!;

        Assert.Equal((72, "B", 80, "A", D(2026, 4, 5), "ผ่าน"), (saved.Evaluation1Score, saved.Evaluation1Grade, saved.Evaluation2Score,
            saved.Evaluation2Grade, saved.ProbationPassedDate, saved.Memo));
        var line = Assert.Single(list);
        Assert.Equal((evaluated.Code, department.Name, D(2026, 2, 1), "B"), (line.Code, line.DepartmentName, line.Evaluation1Date, line.Evaluation1Grade));
        Assert.False(tooLong.Succeeded);
        Assert.Equal((null, null, null), (cleared.Evaluation1Date, cleared.Evaluation1Grade, cleared.Memo));
        var html = await RenderAsync<EvaluationListView>(new() { ["Lines"] = list });
        Assert.Contains("การประเมินครั้งที่1", html);
        Assert.Contains("Approved by", html);
    }

    private Task<YearlyEvaluationReport> YearlyAsync(YearlyEvaluationKind kind, int year, DepartmentDto? department)
        => AsHrAsync(s => s.Get<EvaluationDirectory>().GetYearlyAsync(kind, D(year, 1, 1), D(year, 12, 31), department?.Id));

    [Fact] // BR-067: พนักงาน* → ใบพนักงาน (ใบเตือนนับทุกใบ ไม่ใช่ 0/1 ของคิวรี 202/204 — ผู้ใช้ตัดสิน 2026-10-21), ตำแหน่งอื่น → ใบหัวหน้า, ไม่มีตำแหน่งไม่อยู่ทั้งสอง · ลากิจ = SR 1 · มาสายนับแถว · ใบเตือนเฉพาะเมื่อเลือกแผนก
    public async Task Yearly_sheets_split_by_position_and_count_leaves_late_days_and_warnings()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var staff = await NewEmployeeAsync(department, position: "พนักงานฝ่ายผลิต");
        var head = await NewEmployeeAsync(department, position: "หัวหน้าแผนก");
        var none = await NewEmployeeAsync(department);
        await LeaveAsync(staff, D(year, 2, 2), 1.5, "1");
        await LeaveAsync(staff, D(year, 2, 3), 2, "9"); // ลากิจฉุกเฉิน: not on the sheet (the box is SumOf1)
        await LeaveAsync(staff, D(year, 2, 4), 1, "2");
        await LeaveAsync(staff, D(year, 2, 5), 0.5, "3");
        await LeaveAsync(staff, D(year, 2, 6), 1, "8");
        await LateAsync(staff, D(year, 3, 1));
        await LateAsync(staff, D(year, 3, 1), WorkTypes.Overtime); // the same day's other kind of work counts again
        await WarnAsync(staff, D(year, 4, 1), WarningLevels.Verbal);
        await WarnAsync(staff, D(year, 5, 1), WarningLevels.Verbal);
        await WarnAsync(staff, D(year, 5, 1), WarningLevels.Verbal); // the same day again: every ใบ counts (decided 2026-10-21, as RPT-22)
        await WarnAsync(staff, D(year, 6, 1), WarningLevels.Written);

        var one = await YearlyAsync(YearlyEvaluationKind.Staff, year, department);
        var all = (await YearlyAsync(YearlyEvaluationKind.Staff, year, null)).Sheets.Single(s => s.Code == staff.Code);
        var heads = await YearlyAsync(YearlyEvaluationKind.Supervisor, year, department);

        var sheet = Assert.Single(one.Sheets);
        Assert.Equal((department.Name, department.SectionName), (one.DepartmentName, sheet.SectionName));
        Assert.Equal((1.5, 1.0, 0.5, 2, 1.0, 3, 1), (sheet.PersonalLeave, sheet.SickLeaveWithCertificate, sheet.SickLeaveWithoutCertificate,
            sheet.LateCount, sheet.Absent, sheet.VerbalWarnings, sheet.WrittenWarnings));
        Assert.Equal(-1.5 - 1 - 0.5 - 0.1 - 3 - 15 - 10, sheet.TotalDeduction, 9);
        Assert.Equal((0, 0), (all.VerbalWarnings, all.WrittenWarnings)); // ทั้งหมด (Q-31)
        Assert.Equal([head.Code], heads.Sheets.Select(s => s.Code));
        Assert.DoesNotContain(none.Code, (await YearlyAsync(YearlyEvaluationKind.Supervisor, year, null)).Sheets.Select(s => s.Code));
    }

    private static List<(double X, double Y, string Text)> Texts(FormPage page)
        => Regex.Matches(page.ToSvg(), "<text x=\"([\\d.]+)\" y=\"([\\d.]+)\"[^>]*>([^<]*)</text>")
            .Select(m => (double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), WebUtility.HtmlDecode(m.Groups[3].Value)))
            .ToList();

    [Fact] // RPT-43 ระดับ A, A5 ตั้ง: หัว ปี (TO) เลขเอกสาร · สถิติ × ตัวคูณ · คะแนนหักรวม · หัวข้อ 5–6 ของใบหัวหน้า
    public void The_yearly_sheet_prints_the_statistics_where_the_legacy_did()
    {
        var sheet = new YearlyEvaluationSheet("Y999", "นาย ทดสอบ", "ฝ่ายทดสอบ", 3, 1, 1, 2, 0, 1, 0);
        var staff = new YearlyEvaluationReport(YearlyEvaluationKind.Staff, D(2026, 1, 1), D(2026, 12, 31), null, [sheet]);
        var head = staff with { Kind = YearlyEvaluationKind.Supervisor, DepartmentName = "แผนกทดสอบ" };

        var page = EvaluationForms.Sheet(staff, sheet, D(2026, 10, 15), "WI-TEST-3/2548 ฉบับที่2");
        var texts = Texts(page).Select(t => t.Text).ToList();
        var headTexts = Texts(EvaluationForms.Sheet(head, sheet, D(2026, 10, 15), null)).Select(t => t.Text).ToList();

        Assert.Equal(Paper.A5Portrait, page.Paper);
        Assert.Contains("ใบรายงานการประเมินผลงานพนักงานประจำปี", texts);
        Assert.Contains("2569", texts);
        Assert.Contains("15/10/2569", texts);
        Assert.Contains("ทั้งหมด", texts);
        Assert.Contains("WI-TEST-3/2548 ฉบับที่2", texts);
        Assert.Contains("ซื่อสัตย์สุจริตในการทำงาน", texts);
        Assert.Contains((75.1, 116.0, "3.00"), Texts(page));
        Assert.Contains((139.0, 116.0, "-3.00"), Texts(page));
        Assert.Contains((139.0, 134.0, "-0.10"), Texts(page)); // มาสาย 2 × −0.05
        Assert.Contains((139.0, 140.0, "0.00"), Texts(page)); // ขาดงาน 0, never "-0.00"
        Assert.Contains((139.1, 158.0, "-10.10"), Texts(page));
        Assert.Contains("ใบรายงานการประเมินผลงานหัวหน้าหรือรองหัวหน้าประจำปี", headTexts);
        Assert.Contains("การควบคุมงาน", headTexts);
        Assert.Contains("แผนกทดสอบ", headTexts);
        Assert.DoesNotContain("ซื่อสัตย์สุจริตในการทำงาน", headTexts);
        Assert.Equal(DocumentKeys.YearlyEvaluationMonthly, EvaluationForms.DocumentKeyOf(YearlyEvaluationKind.Supervisor));
    }
}

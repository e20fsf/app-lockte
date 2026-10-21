using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;
using Microsoft.AspNetCore.Components;

namespace Lockte.Web.Tests;

/// <summary>
/// BR-031 ค่าพักร้อน (SCR-51, #63): who is paid and how much, with the legacy's ⚠ kept (every SR counts, rows equal in
/// FROM_D/TO_D/SUM_D count once, the years-only อายุงาน). The report reads every working employee, so each test looks only at
/// its own employees, and its leaves are in years (1300+) no other test uses.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class VacationPayTests(LocalDbFixture db)
{
    private static int lastCode;
    private static int lastYear = 1300; // after PayHistoryReportTests' (900+) and PayrollTests' (1050+) years, clear of the leave tests

    /// <summary>"H" keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)).</summary>
    private static string NewCode() => $"H{Interlocked.Increment(ref lastCode):D3}";

    private static int NewYear() => Interlocked.Increment(ref lastYear);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static int ThisYear => DateTime.Now.Year;

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
            null, Unique("H"), Unique("แผนก"), "H", 9, sectionId, null, [], null)))).Id!.Value;
        return (await AsHrAsync(s => s.Get<OrgDirectory>().GetDepartmentAsync(id)))!;
    }

    private async Task<EmployeeDto> NewEmployeeAsync(DepartmentDto? department, double dailyWage = 400, DateOnly? start = null,
        DateOnly? resign = null, string? code = null)
    {
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = code ?? NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], DepartmentId = department?.Id,
            SectionId = department?.SectionId, StartDate = start ?? D(2015, 3, 1), ResignDate = resign, DailyWage = dailyWage,
            EmployeeType = EmployeeTypes.Daily,
        }));
        Ok(result);
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private async Task LeaveAsync(EmployeeDto employee, DateOnly from, double days, string type = "3", DateOnly? to = null)
    {
        var result = await AsHrAsync(s => s.Get<LeaveRegistry>().SaveAsync(new LeaveInput(null, employee.Id, from, from, to ?? from,
            null, null, days, type, false, null, null, null)));
        Ok(result.Save);
    }

    private async Task<IReadOnlyList<VacationPayLine>> LinesAsync(int year, params EmployeeDto[] employees)
    {
        var report = await AsHrAsync(s => s.Get<VacationPayDirectory>().GetReportAsync(D(year, 1, 1), D(year, 12, 31)));
        var codes = employees.Select(e => e.Code).ToHashSet();
        return report.Lines.Where(l => codes.Contains(l.Code)).ToList();
    }

    [Fact] // BR-031 ตัวอย่าง 1–3: ไม่ลาเลย 6 × 400; ลารวม 2.5 วัน 3.5 × 400; ลารวม 6 วันพอดีไม่อยู่ในรายงาน
    public async Task Pays_the_days_left_of_six_at_todays_daily_wage()
    {
        var year = NewYear();
        var department = await NewDepartmentAsync();
        var none = await NewEmployeeAsync(department);
        var some = await NewEmployeeAsync(department);
        var six = await NewEmployeeAsync(department);
        await LeaveAsync(some, D(year, 3, 2), 1.5);
        await LeaveAsync(some, D(year, 7, 1), 1, "1");
        await LeaveAsync(six, D(year, 4, 1), 4, "8");
        await LeaveAsync(six, D(year, 5, 1), 2, "6");

        var lines = await LinesAsync(year, none, some, six);

        Assert.Equal([none.Code, some.Code], lines.Select(l => l.Code));
        Assert.Equal((department.Name, 400.0, 0.0, 6.0, 2400.0), (lines[0].DepartmentName, lines[0].DailyWage, lines[0].UsedDays,
            lines[0].RemainingDays, lines[0].Amount));
        Assert.Equal((2.5, 3.5, 1400.0), (lines[1].UsedDays, lines[1].RemainingDays, lines[1].Amount));
    }

    [Fact] // คิวรี 1: FROM_D ≥ FORM และ TO_D ≤ TO — ใบที่คร่อมวันใดวันหนึ่งไม่นับ; ไม่ปัดเงิน
    public async Task Counts_only_the_leaves_wholly_within_the_dates_and_keeps_the_amount_unrounded()
    {
        var year = NewYear();
        var employee = await NewEmployeeAsync(null, dailyWage: 881.87);
        await LeaveAsync(employee, D(year, 2, 2), 5.125);
        await LeaveAsync(employee, D(year, 12, 31), 2, to: D(year + 1, 1, 1)); // across the end
        await LeaveAsync(employee, D(year - 1, 12, 31), 3, to: D(year, 1, 1)); // across the start

        var line = Assert.Single(await LinesAsync(year, employee));

        Assert.Null(line.DepartmentName);
        Assert.Equal(5.125, line.UsedDays);
        Assert.Equal(0.875 * 881.87, line.Amount, 9);
    }

    [Fact] // ⚠ คิวรี 1 GROUP BY SUM_D, FROM_D, TO_D, ID: ใบที่วันที่และจำนวนวันเท่ากันนับครั้งเดียวแม้คนละประเภท
    public async Task Rows_equal_in_dates_and_days_count_once_whatever_their_type()
    {
        var year = NewYear();
        var employee = await NewEmployeeAsync(null);
        await LeaveAsync(employee, D(year, 3, 2), 1, "1");
        await LeaveAsync(employee, D(year, 3, 2), 1, "3");
        await LeaveAsync(employee, D(year, 3, 2), 0.5, "3");

        var line = Assert.Single(await LinesAsync(year, employee));

        Assert.Equal(1.5, line.UsedDays);
    }

    [Fact] // ผู้มีสิทธิ์: ยังไม่ลาออก ไม่ใช่ LT* และ Year(วันนี้) − Year(START) > 1 (นับแค่ปี)
    public async Task Leaves_out_the_resigned_lt_codes_and_those_who_started_last_year()
    {
        var year = NewYear();
        var lastDecember = await NewEmployeeAsync(null, start: D(ThisYear - 2, 12, 31));
        var lastJanuary = await NewEmployeeAsync(null, start: D(ThisYear - 1, 1, 1));
        var resigned = await NewEmployeeAsync(null, resign: D(ThisYear - 1, 6, 30));
        var lt = await NewEmployeeAsync(null, code: "LTH" + (Interlocked.Increment(ref lastCode) % 10));

        var lines = await LinesAsync(year, lastDecember, lastJanuary, resigned, lt);

        Assert.Equal([lastDecember.Code], lines.Select(l => l.Code));
    }

    [Theory] // Format(Now(),'yyyy') − Format(START,'yyyy') > 1
    [InlineData(2024, 12, 31, 2026, true)]
    [InlineData(2025, 1, 1, 2026, false)]
    [InlineData(2026, 1, 1, 2026, false)]
    public void Entitlement_counts_calendar_years_only(int year, int month, int day, int thisYear, bool entitled)
        => Assert.Equal(entitled, VacationPay.IsEntitled(D(year, month, day), thisYear));
}

/// <summary>RPT-36 and RPT-52 before they become PDFs, and RPT-52 as one A4 sheet.</summary>
[Collection(ChromiumCollection.Name)]
public sealed class VacationPayReportLayoutTests(ChromiumFixture chromium)
{
    private static async Task<string> HtmlAsync<TView>(Dictionary<string, object?> parameters) where TView : IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<TView>(parameters));

    private static string Text(string html) => Regex.Replace(html, "<[^>]+>", "\n");

    private static readonly VacationPayReport Report = new(new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 3),
    [
        new VacationPayLine("H001", "นาย ก", "ซ่อมบำรุง", 881.8666666666667, 5.125),
        new VacationPayLine("H002", "นาย ข", "ไดคาสท์", 588.0333333333333, 5.5),
        new VacationPayLine("H003", "นาย ค", null, 372, 0),
    ]);

    [Fact] // หัว "…ประจำปี" + ปี พ.ศ. ของตั้งแต่วันที่; ค่าแรง/วัน และวันคงเหลือทศนิยม 2; เงินจำนวนเต็ม; รวมจากยอดไม่ปัด
    public async Task The_list_prints_the_amounts_to_the_baht_and_their_unrounded_total()
    {
        var html = await HtmlAsync<VacationPayReportView>(new() { ["Report"] = Report });

        Assert.Equal("รายชื่อพนักงานจ่ายคืนเงินพักร้อนประจำปี 2569", VacationPayReportView.TitleOf(Report));
        Assert.Contains(">881.87<", html);
        Assert.Contains(">0.88<", html); // 0.875
        Assert.Contains(">772<", html);  // 771.63
        Assert.Contains(">294<", html);  // 294.02
        Assert.Contains(">2,232<", html); // 6 × 372
        Assert.Contains(">3,298<", html); // 771.63 + 294.02 + 2,232 = 3,297.65
        Assert.Contains("จัดทำโดย", html);
        Assert.Contains("อนุมัติโดย", html);
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<VacationPayReportView>(new() { ["Report"] = Report with { Lines = [] } }));
    }

    [Fact] // RPT-52 ข้อความคงที่ทุกบรรทัด; ชื่อผู้ลงนามจาก settings, ว่าง = วงเล็บว่างให้เขียนเอง
    public async Task The_notice_prints_the_legacy_text_and_the_signer_from_the_settings()
    {
        var signed = Text(await HtmlAsync<VacationNoticeView>(new() { ["FactoryManagerName"] = "นาย ทดสอบ ลงนาม" }));
        var blank = Text(await HtmlAsync<VacationNoticeView>(new()));

        Assert.All(VacationNoticeView.Lines, l => Assert.Contains(l.Text, signed));
        Assert.Equal(28, VacationNoticeView.Lines.Count);
        Assert.Contains("12.   เข้าทำงานเดือนธันวาคม ได้หยุดเพิ่ม 0.5 วัน", signed);
        Assert.Contains("(นาย ทดสอบ ลงนาม)", signed);
        Assert.Contains("ผู้จัดการโรงงาน", signed);
        Assert.Contains("(" + new string(' ', 40) + ")", blank);
    }

    [Fact]
    public async Task The_notice_is_one_a4_portrait_sheet()
    {
        var body = await ReportBody.RenderAsync<VacationNoticeView>(new());

        var pdf = Encoding.Latin1.GetString(await chromium.Renderer.RenderAsync(FormHtml.Compose("ประกาศ", Paper.A4Portrait, body), Paper.A4Portrait));

        Assert.Single(Regex.Matches(pdf, @"/Type /Page\b"));
        var box = Regex.Match(pdf, @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        Assert.Equal(210, double.Parse(box.Groups[1].Value, CultureInfo.InvariantCulture) * 25.4 / 72, 0);
    }
}

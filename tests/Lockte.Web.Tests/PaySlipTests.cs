using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Migration;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;

namespace Lockte.Web.Tests;

/// <summary>
/// RPT-04 สลิปเงินเดือน (#55, BR-034) without a database: what each slip holds (the legacy table SLIP) and where the form
/// prints it. How close the form lies on the legacy print is checked by overlaying the sample (#40), not here.
/// </summary>
public sealed class PaySlipTests
{
    private static DateOnly D(string iso) => DateOnly.Parse(iso, CultureInfo.InvariantCulture);

    /// <summary>BR-002's period 27/08 – 11/09 (D = 16, SUN = 2), paid 16/09.</summary>
    private static readonly PayPeriodDates Dates = new(D("2026-08-27"), D("2026-09-11"), D("2026-09-16"));

    private static readonly PayPeriodFacts Facts = new(Dates, 2, null);

    private static EmployeeTimeTotal Time(double w = 0, double l = 0, double o1 = 0, double s = 0, double o2 = 0, double h = 0, double o3 = 0,
        int holi = 0, int sun = 2)
        => new(1, "A001", "นาย ทดสอบ สลิป", w, l, 0, o1, s, o2, h, o3, holi, sun, 0);

    private static PaySlip Slip(EmployeeTimeTotal time, PayEmployee employee, bool account = true)
        => PaySlips.Build(Dates, time, PayCalculation.Calculate(Facts, time, employee)!, employee.EmployeeType!.Value, account);

    [Fact] // BR-034 ตัวอย่าง 1: รายวัน SA_DAY 400, D 16, SUN 2, W 14, L 1, O1 3.5
    public void Daily_slip_is_the_legacy_SLIP_row()
    {
        var s = Slip(Time(w: 14, l: 1, o1: 3.5), new PayEmployee(EmployeeTypes.Daily, 0, 400, null, false, null));

        Assert.Equal((16, 2, 0, 14), (s.Days, s.WeeklyHolidays, s.PublicHolidays, s.WorkingDays));
        Assert.Equal((5600m, 263m, 0m, 0m, 0m, 0m), (s.PayNormal, s.Parts.Overtime, s.Parts.Sunday, s.Parts.SundayOvertime, s.Parts.Holiday, s.Parts.HolidayOvertime));
        Assert.Equal((5863m, 5863m, 400.0, (decimal?)null), (s.Total, s.Net, s.Rate, s.Atm));
    }

    [Fact] // BR-034 ตัวอย่าง 2: รายเดือน SALARY 15,000 มาครบ, S 8 ชม., O2 2 ชม.
    public void Monthly_slip_splits_the_overtime_into_its_parts()
    {
        var s = Slip(Time(w: 14, s: 8, o2: 2), new PayEmployee(EmployeeTypes.Monthly, 15000, 500, null, false, null));

        Assert.Equal((7500m, 500m, 375m, 8375m, 8375m), (s.PayNormal, s.Parts.Sunday, s.Parts.SundayOvertime, s.Total, s.Net));
        Assert.Equal(15000.0, s.Rate);
    }

    [Fact] // วันหยุดประเพณี: H_D from HOLI, W_D = TT_D − HOLI − SUN, H_W and OT3 on their own
    public void Holiday_hours_and_days_print_in_their_own_fields()
    {
        var s = Slip(Time(w: 13, h: 8, o3: 2, holi: 1), new PayEmployee(EmployeeTypes.Daily, 0, 400, null, false, null));

        Assert.Equal((1, 13), (s.PublicHolidays, s.WorkingDays));
        Assert.Equal((400m, 300m), (s.Parts.Holiday, s.Parts.HolidayOvertime));
        Assert.Equal(s.PayNormal + 700, s.Total);
    }

    [Fact] // SLIP41/51: HOT1 = Y adds HOT to TT; SLIP5/51: no บัญชีทหารไทย → ATM 0; รายเดือน (SLIP3) leaves ATM blank
    public void Hot_and_atm_follow_the_legacy_queries()
    {
        var hot = Slip(Time(w: 14), new PayEmployee(EmployeeTypes.Daily, 0, 400, null, true, null), account: false);
        var monthly = Slip(Time(w: 14), new PayEmployee(EmployeeTypes.Monthly, 15000, 500, null, false, null), account: false);

        Assert.Equal((140m, 5740m, (decimal?)0), (hot.Hot, hot.Total, hot.Atm));
        Assert.Null(monthly.Atm);
    }

    [Theory] // #180: 0.25% of รวมค่าแรง, whole baht, half away from zero (lot 4: 4,692 → 12)
    [InlineData(4692, 12)]
    [InlineData(5863, 15)]
    [InlineData(400, 1)]   // 1.0
    [InlineData(200, 1)]   // 0.5 → 1
    [InlineData(199, 0)]   // 0.4975 → 0
    [InlineData(0, 0)]
    public void Welfare_is_a_quarter_percent_rounded_to_the_baht(decimal total, decimal expected)
        => Assert.Equal(expected, Welfare.Contribution(total));

    [Fact] // #180: the employee's share leaves the net, the company's does not; a slip without welfare (a migrated period) keeps net = total
    public void Employee_welfare_is_deducted_from_the_net()
    {
        var s = Slip(Time(w: 14, l: 1, o1: 3.5), new PayEmployee(EmployeeTypes.Daily, 0, 400, null, false, null)) with
        {
            WelfareEmployee = 12, WelfareCompany = 12, WelfareAccumulated = 23,
        };

        Assert.Equal((5863m, 5851m), (s.Total, s.Net));
        Assert.Equal(s.Total, Slip(Time(w: 14, l: 1, o1: 3.5), new PayEmployee(EmployeeTypes.Daily, 0, 400, null, false, null)).Net);
    }

    private static PaySlipBatch Batch(int count, params SlipSignerDto[] signers)
    {
        var period = new PayPeriodDto(1, Dates.From, Dates.To, Dates.PayDate, PayPeriodStep.PayCalculated, null, null, [], DateTimeOffset.Now, "hr1");
        var slip = Slip(Time(w: 14, l: 1, o1: 3.5), new PayEmployee(EmployeeTypes.Daily, 0, 400, null, false, null))
            with { WelfareEmployee = 15, WelfareCompany = 15, WelfareAccumulated = 40 };
        return new PaySlipBatch(period, Enumerable.Range(1, count).Select(i => slip with { Code = $"A{i:D3}" }).ToList(), signers);
    }

    /// <summary>The texts of an SVG page with their x and baseline.</summary>
    private static List<(double X, double Y, string Text)> Texts(FormPage page)
        => Regex.Matches(page.ToSvg(), "<text x=\"([\\d.]+)\" y=\"([\\d.]+)\"[^>]*>([^<]*)</text>")
            .Select(m => (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                System.Net.WebUtility.HtmlDecode(m.Groups[3].Value)))
            .ToList();

    private static string TextAt(FormPage page, double x, double y)
        => Assert.Single(Texts(page), t => Math.Abs(t.X - x) < 0.01 && Math.Abs(t.Y - y) < 0.01).Text;

    [Fact] // RPT-04: 3 ใบต่อหน้า A4, one every 95 mm
    public void Slips_print_three_to_a_page_95_mm_apart()
    {
        var pages = PaySlipForm.Layout(Batch(4));

        Assert.Equal(2, pages.Count);
        Assert.All(pages, p => Assert.Equal(Paper.A4Portrait, p.Paper));
        Assert.Equal(["A001", "A002", "A003"], Texts(pages[0]).Where(t => Math.Abs(t.X - 49.82) < 0.01).Select(t => t.Text));
        Assert.Equal([35.52, 130.52, 225.52], Texts(pages[0]).Where(t => Math.Abs(t.X - 49.82) < 0.01).Select(t => t.Y));
        Assert.Equal("A004", TextAt(pages[1], 49.82, 35.52));
        Assert.Equal(3, Regex.Matches(pages[0].ToSvg(), "<image ").Count); // the logo of each slip
    }

    [Fact] // the legacy formats: วันที่ d/M/yyyy พ.ศ., ชั่วโมง Fixed 1, เงิน Standard 0, SA_D Standard 2, หักว่าง
    public void Slip_prints_each_field_in_the_legacy_format()
    {
        var page = PaySlipForm.Layout(Batch(1))[0];

        Assert.Equal("16/9/2569", TextAt(page, 120.96, 23.18));
        Assert.Equal(("27/8/2569", "11/9/2569"), (TextAt(page, 46.86, 29.5), TextAt(page, 71.53, 29.5)));
        Assert.Equal(("16", "2", "0", "14"), (TextAt(page, 101.53, 29.5), TextAt(page, 123.46, 29.5), TextAt(page, 143.07, 29.5), TextAt(page, 165.86, 29.5)));
        Assert.Equal("400.00", TextAt(page, 159.95, 35.52));
        Assert.Equal(("14.0", "1.0", "3.5"), (TextAt(page, 34.47, 56.49), TextAt(page, 41.84, 56.49), TextAt(page, 72.54, 56.49)));
        Assert.Equal(("5,600", "263"), (TextAt(page, 113.76, 56.49), TextAt(page, 153.94, 56.49)));
        // #180: เงินสงเคราะห์ลูกจ้าง, บริษัท, รวมเงินสะสม, then โบนัส, ค่าพักร้อน, รวมค่าแรง, เงินได้สุทธิ (= 5,863 − 15)
        Assert.Equal(("15", "15", "40", "0", "0", "5,863", "5,848"),
            (TextAt(page, 62.5, 70.5), TextAt(page, 83.5, 70.5), TextAt(page, 103, 70.5), TextAt(page, 127.75, 70.5), TextAt(page, 140.5, 70.5),
                TextAt(page, 156.25, 70.5), TextAt(page, 176.175, 70.5)));
        Assert.DoesNotContain(Texts(page), t => Math.Abs(t.Y - 70.5) < 0.01 && t.X < 52); // ATM ประกันสังคม: blank
        Assert.DoesNotContain(Texts(page), t => t.Text is "กองทุนเลี้ยงชีพ" or "ค่าแรงค้างงวดก่อน");
        Assert.Contains(Texts(page), t => t.Text == "ลูกจ้าง (0.25%)");
        Assert.Contains(Texts(page), t => t.Text == "บริษัท (0.25%)");
    }

    [Theory]
    [InlineData(13.9375, "13.9")]
    [InlineData(0.25, "0.3")]
    [InlineData(1.85, "1.9")]
    [InlineData(0, "0.0")]
    public void Hours_print_as_Fixed_one_decimal(double hours, string text) => Assert.Equal(text, PaySlipForm.Hours(hours));

    [Fact] // #55: แผนกบุคคล / ผู้จัดการโรงงาน — the picture when there is one, else the name where the legacy label was
    public void Signers_print_their_picture_or_their_name()
    {
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2, 3];
        var page = PaySlipForm.Layout(Batch(1,
            new SlipSignerDto(SlipSignerRole.HrDepartment, "สมหญิง", png, "image/png", null),
            new SlipSignerDto(SlipSignerRole.FactoryManager, "สมชาย", null, null, null)))[0];

        Assert.Equal("สมชาย", TextAt(page, 146.68, 78.59));
        Assert.DoesNotContain(Texts(page), t => t.Text == "สมหญิง");
        Assert.Contains("href=\"data:image/png;base64," + Convert.ToBase64String(png) + "\"", page.ToSvg());
    }

    [Fact] // no signer saved yet: the lines stay empty
    public void A_slip_without_signers_leaves_their_lines_empty()
    {
        var page = PaySlipForm.Layout(Batch(1))[0];

        Assert.DoesNotContain(Texts(page), t => Math.Abs(t.Y - 78.59) < 0.01);
        Assert.Equal(["ผู้รับเงิน", "แผนกบุคคล", "ผู้จัดการโรงงาน"], Texts(page).Where(t => Math.Abs(t.Y - 79.46) < 0.01).Select(t => t.Text));
    }

    [Fact] // AC-07 comparer: a field that differs is listed with both values as printed; Null N/HOT are 0
    public void The_slip_baseline_lists_each_field_that_differs()
    {
        var slip = Slip(Time(w: 14, l: 1, o1: 3.5), new PayEmployee(EmployeeTypes.Daily, 0, 400, null, false, null));
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "﻿ID,NAME,W,L,O1,SALA,OT,TT,NET,SA_D,TT_D,W_D,DATE,ATM,N,HOT\n"
                                + "a001,\"นาย, ทดสอบ\",14,1,3.5,5600,262,5863,5863,400,16,14,2026-09-16 00:00:00,,,\n");

        var mismatches = SlipBaseline.Compare(Dates, SlipBaseline.Read(path), [slip]);

        Assert.Equal([new PayMismatch(Dates.From, "A001", "OT", "262", "263")], mismatches);
        File.Delete(path);
    }
}

/// <summary>Runs only when <see cref="PaySlipGoldenMasterTests.BaselineVariable"/> is set (real data stays on the team's PC).</summary>
public sealed class SlipGoldenMasterFactAttribute : FactAttribute
{
    public SlipGoldenMasterFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(PaySlipGoldenMasterTests.BaselineVariable) is null)
            Skip = $"golden master สลิป: ตั้ง {PaySlipGoldenMasterTests.BaselineVariable} เพื่อรัน";
    }
}

/// <summary>
/// Golden master of RPT-04 (AC-07): the legacy inputs of the period in ตั้งวันที่ตัดWEEK — คิวรี คำนวณเวลาทำงาน13 with
/// EMPLO, and the period's SUNDAY count and first HOLIDAY — through <see cref="PayCalculation"/> and
/// <see cref="PaySlips"/>, against the table SLIP that มาโคร SLIP filled for the same period, every field. The folder
/// holds <c>period.csv</c> (FROM_DA,TO_DA,PAY_D,DAT,SUNDAY), <c>inputs.csv</c> and <c>slip.csv</c>, exported from a copy
/// of <c>source/พนักงาน.accdb</c>; mismatches go to <c>slip-golden-master-mismatches.csv</c> there.
/// </summary>
public sealed class PaySlipGoldenMasterTests
{
    public const string BaselineVariable = "LOCKTE_SLIP_BASELINE";

    [SlipGoldenMasterFact]
    public void Slips_match_the_legacy_SLIP_table()
    {
        var folder = Environment.GetEnvironmentVariable(BaselineVariable)!;
        static string[][] Rows(string path) => File.ReadAllLines(path).Skip(1).Where(l => l.Trim().Length > 0).Select(SlipBaseline.Csv).ToArray();
        static DateOnly Date(string s) => DateOnly.ParseExact(s[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        static double N(string s) => s.Length == 0 ? 0 : double.Parse(s, CultureInfo.InvariantCulture);

        var p = Rows(Path.Combine(folder, "period.csv"))[0];
        var dates = new PayPeriodDates(Date(p[0]), Date(p[1]), Date(p[2]));
        var facts = new PayPeriodFacts(dates, (int)N(p[4]), p[3].Length == 0 ? null : Date(p[3]));
        var slips = new List<PaySlip>();
        var i = 0;
        foreach (var r in Rows(Path.Combine(folder, "inputs.csv")))
        {
            // ID,NAME,CLAS,SALARY,SA_DAY,POINT_1,HOT1,RESIGN,ACC,W,L,LNO,O1,S,O2,H,O3,HOLI,SUN,SUN_DAY
            var type = int.TryParse(r[2], out var clas) ? clas : (int?)null;
            var employee = new PayEmployee(type, N(r[3]), N(r[4]), r[5].Length == 0 ? null : (int)N(r[5]), r[6] == "Y",
                r[7].Length == 0 ? null : Date(r[7]));
            var time = new EmployeeTimeTotal(++i, r[0].ToUpperInvariant(), r[1], N(r[9]), N(r[10]), N(r[11]), N(r[12]), N(r[13]), N(r[14]), N(r[15]),
                N(r[16]), (int)N(r[17]), (int)N(r[18]), (int)N(r[19]));
            if (PayCalculation.Calculate(facts, time, employee) is { } pay)
                slips.Add(PaySlips.Build(dates, time, pay, type!.Value, r[8].Length > 0));
        }

        var mismatches = SlipBaseline.Compare(dates, SlipBaseline.Read(Path.Combine(folder, "slip.csv")), slips);

        var outFile = Path.Combine(folder, "slip-golden-master-mismatches.csv");
        PayBaseline.Write(outFile, mismatches);
        Assert.True(slips.Count > 0, "ไม่มีสลิป");
        Assert.True(mismatches.Count == 0, $"ไม่ตรง {mismatches.Count} ช่อง จาก {slips.Count} ใบ — ดู {outFile}");
    }
}

/// <summary>สลิปผ่านบริการของ module (#55): คำนวณค่าแรง → สลิปของงวด, ผู้ลงนาม, ขั้น "พิมพ์ SLIP".</summary>
[Collection(LocalDbCollection.Name)]
public sealed class PaySlipDataTests(LocalDbFixture db)
{
    private static int lastCode;
    private static int lastYear = 750; // after PayrollTests' years (600+), before PayPeriodTests' (1100+)

    /// <summary>"S" keeps clear of the other test classes' codes.</summary>
    private static string NewCode() => $"S{Interlocked.Increment(ref lastCode):D3}";

    private static int NewYear() => Interlocked.Increment(ref lastYear);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static void Ok(ServiceResult result) => Assert.True(result.Succeeded, string.Join(", ", result.Errors));

    private async Task<EmployeeDto> NewEmployeeAsync(int year, int type = EmployeeTypes.Daily, double salary = 0, double dailyWage = 400,
        string? account = null)
    {
        var shift = await AsHrAsync(async s => (await s.Get<OrgDirectory>().FindShiftByCodeAsync("A"))!);
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], ShiftId = shift.Id, StartDate = D(year - 1, 1, 1),
            EmployeeType = type, Salary = salary, DailyWage = dailyWage, BankAccountTtb = account,
        }));
        Ok(result);
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private async Task WorkAsync(EmployeeDto employee, DateOnly date, string type = "W", string workIn = "08:00", string workOut = "17:00", int rest = 60)
        => Ok(await AsHrAsync(s => s.Get<WorkTimeRegistry>().SaveAsync(new WorkTimeInput(null, date, employee.Id, type,
            TimeOnly.Parse(workIn), TimeOnly.Parse(workOut), rest, null, null, null))));

    /// <summary>27/08 – 11/09 (D = 16), วันอาทิตย์ 30/08 and 06/09, paid 16/09.</summary>
    private async Task<int> PeriodAsync(int year)
    {
        foreach (var day in new[] { D(year, 8, 30), D(year, 9, 6) })
            Ok(await AsHrAsync(s => s.Get<CalendarRegistry>().SaveWeeklyHolidayAsync(new WeeklyHolidayInput(null, day, "วันอาทิตย์", null))));
        var result = await AsHrAsync(s => s.Get<PayPeriodRegistry>().SaveAsync(new PayPeriodInput(null, D(year, 8, 27), D(year, 9, 11), D(year, 9, 16), null)));
        Ok(result);
        return result.Id!.Value;
    }

    private Task<PaySlipBatch?> SlipsAsync(int periodId) => AsHrAsync(s => s.Get<PayPeriodDirectory>().GetSlipsAsync(periodId));

    [Fact] // BR-034 end to end: a daily employee without บัญชี with O1 and Sunday work, a monthly one with บัญชี
    public async Task Slips_come_from_the_calculated_pay_and_time()
    {
        var year = NewYear();
        var period = await PeriodAsync(year);
        var daily = await NewEmployeeAsync(year);
        var monthly = await NewEmployeeAsync(year, EmployeeTypes.Monthly, 15000, 500, account: "1234567890");
        foreach (var day in Enumerable.Range(0, 16).Select(i => D(year, 8, 27).AddDays(i)).Where(d => d != D(year, 8, 30) && d != D(year, 9, 6)))
        {
            await WorkAsync(daily, day);
            await WorkAsync(monthly, day);
        }
        await WorkAsync(daily, D(year, 9, 1), "O1", "17:00", "20:30", 0); // 3.5 ชม.
        await WorkAsync(monthly, D(year, 8, 30), "S"); // 8 ชม. วันอาทิตย์
        Ok(await AsHrAsync(s => s.Get<PayPeriodRegistry>().CalculateTimeAsync(period)));
        Ok(await AsHrAsync(s => s.Get<PayPeriodRegistry>().CalculatePayAsync(period)));

        var batch = (await SlipsAsync(period))!;

        var d = Assert.Single(batch.Slips, s => s.Code == daily.Code);
        Assert.Equal((14.0, 3.5, 5600m, 263m, 5863m, (decimal?)0, 16, 14), (d.Normal, d.Overtime, d.PayNormal, d.Parts.Overtime, d.Total, d.Atm, d.Days, d.WorkingDays));
        Assert.Equal((15m, 15m, 15m, 5848m), (d.WelfareEmployee, d.WelfareCompany, d.WelfareAccumulated, d.Net)); // 5,863 × 0.25% = 14.66
        var m = Assert.Single(batch.Slips, s => s.Code == monthly.Code);
        Assert.Equal((8.0, 7500m, 500m, 8000m, (decimal?)null, 15000.0), (m.Sunday, m.PayNormal, m.Parts.Sunday, m.Total, m.Atm, m.Rate));
        Assert.Equal(batch.Slips.Select(s => s.Code).Order(StringComparer.Ordinal), batch.Slips.Select(s => s.Code));
        Assert.Equal([SlipSignerRole.HrDepartment, SlipSignerRole.FactoryManager], batch.Signers.Select(s => s.Role));
    }

    [Fact] // no slip before ค่าแรง is calculated
    public async Task There_are_no_slips_before_the_pay_is_calculated()
        => Assert.Null(await SlipsAsync(await PeriodAsync(NewYear())));

    [Fact] // #22: printing the slips is step พิมพ์ SLIP — refused while ส่งแบงค์ is not done, done after it
    public async Task Printing_slips_completes_the_step_only_after_the_bank_step()
    {
        var year = NewYear();
        var period = await PeriodAsync(year);
        var employee = await NewEmployeeAsync(year);
        await WorkAsync(employee, D(year, 9, 1));
        var registry = (Func<TestScope, PayPeriodRegistry>)(s => s.Get<PayPeriodRegistry>());
        Ok(await AsHrAsync(s => registry(s).CalculateTimeAsync(period)));
        Ok(await AsHrAsync(s => registry(s).CalculatePayAsync(period)));
        Ok(await AsHrAsync(s => registry(s).CompleteStepAsync(period, PayPeriodStep.SummaryPrinted)));

        Assert.False((await AsHrAsync(s => registry(s).CompleteStepAsync(period, PayPeriodStep.SlipPrinted))).Succeeded);
        Ok(await AsHrAsync(s => registry(s).CompleteStepAsync(period, PayPeriodStep.BankPrinted)));
        Ok(await AsHrAsync(s => registry(s).CompleteStepAsync(period, PayPeriodStep.SlipPrinted)));
        Assert.NotNull(await SlipsAsync(period));
    }

    [Fact] // #55: ผู้ลงนาม — name, picture kept, replaced, removed; a stale rowversion is a conflict
    public async Task Signers_are_saved_with_their_picture()
    {
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10];
        async Task<SlipSignerDto> SignerAsync() => (await AsHrAsync(s => s.Get<PayPeriodDirectory>().ListSlipSignersAsync()))
            .Single(x => x.Role == SlipSignerRole.FactoryManager);
        Task<ServiceResult> SaveAsync(SlipSignerInput input) => AsHrAsync(s => s.Get<SlipSignerRegistry>().SaveAsync(input));

        var before = await SignerAsync();
        Ok(await SaveAsync(new SlipSignerInput(SlipSignerRole.FactoryManager, " สมชาย ทดสอบ ", png, "image/png", false, before.RowVersion)));
        var saved = await SignerAsync();
        Ok(await SaveAsync(new SlipSignerInput(SlipSignerRole.FactoryManager, "สมชาย ใหม่", null, null, false, saved.RowVersion)));
        var renamed = await SignerAsync();

        Assert.Equal("สมชาย ทดสอบ", saved.Name);
        Assert.Equal(png, saved.Signature);
        Assert.Equal(("สมชาย ใหม่", "image/png"), (renamed.Name, renamed.SignatureContentType));
        Assert.Equal(png, renamed.Signature);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            SaveAsync(new SlipSignerInput(SlipSignerRole.FactoryManager, "เก่า", null, null, false, saved.RowVersion)));
        Ok(await SaveAsync(new SlipSignerInput(SlipSignerRole.FactoryManager, "สมชาย ใหม่", null, null, true, renamed.RowVersion)));
        Assert.Null((await SignerAsync()).Signature);
    }

    [Fact] // a picture must be a PNG or JPEG of at most 512 KB
    public async Task A_signature_that_is_not_a_small_picture_is_refused()
    {
        Task<ServiceResult> SaveAsync(byte[] picture, string type) => AsHrAsync(s => s.Get<SlipSignerRegistry>().SaveAsync(
            new SlipSignerInput(SlipSignerRole.HrDepartment, "ทดสอบ", picture, type, false, null)));

        Assert.Equal(["รูปลายเซ็นต้องเป็นไฟล์ PNG หรือ JPEG"], (await SaveAsync("GIF89a"u8.ToArray(), "image/png")).Errors);
        Assert.Equal(["รูปลายเซ็นใหญ่ได้ไม่เกิน 512 KB"], (await SaveAsync(new byte[SlipSignerRegistry.MaxSignatureBytes + 1], "image/jpeg")).Errors);
    }
}

/// <summary>RPT-04 through Chromium (#52 pipeline): A4, one page per three slips, the logo and TH Sarabun New embedded.</summary>
[Collection(ChromiumCollection.Name)]
public sealed class PaySlipPdfTests(ChromiumFixture chromium)
{
    private static int Pages(byte[] pdf) => Regex.Matches(System.Text.Encoding.Latin1.GetString(pdf), @"/Type /Page\b").Count;

    [Fact]
    public async Task Seven_slips_print_on_three_A4_pages()
    {
        var dates = new PayPeriodDates(new(2026, 10, 12), new(2026, 10, 26), new(2026, 11, 1));
        var period = new PayPeriodDto(1, dates.From, dates.To, dates.PayDate, PayPeriodStep.PayCalculated, null, null, [], DateTimeOffset.Now, "hr1");
        var slip = new PaySlip("A005", "นางสาวทดสอบ ระบบ", 15, 2, 1, 525, 3, 1.8, 0, 0, 0, 0, 0, 3019, OvertimeParts.None, 0, null);
        var batch = new PaySlipBatch(period, Enumerable.Repeat(slip, 7).ToList(), []);

        var pdf = await chromium.Renderer.RenderAsync(FormPageHtml.Compose(PaySlipForm.Title, PaySlipForm.Layout(batch)), Paper.A4Portrait);

        Assert.Equal(3, Pages(pdf));
        Assert.Contains("THSarabunNew", System.Text.Encoding.Latin1.GetString(pdf));
    }

    /// <summary>
    /// The overlay check of #40 (tools/form-overlay): with <c>LOCKTE_FORM_OVERLAY_DIR</c> set, prints
    /// <c>slips.json</c> there (a <see cref="PaySlipBatch"/> with the sample's data, kept out of git) to <c>slips.pdf</c>.
    /// </summary>
    [Fact]
    public async Task Prints_the_overlay_check_pdf_when_asked()
    {
        var dir = Environment.GetEnvironmentVariable("LOCKTE_FORM_OVERLAY_DIR");
        if (string.IsNullOrEmpty(dir) || !File.Exists(Path.Combine(dir, "slips.json"))) return;
        var batch = JsonSerializer.Deserialize<PaySlipBatch>(await File.ReadAllTextAsync(Path.Combine(dir, "slips.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var pdf = await chromium.Renderer.RenderAsync(FormPageHtml.Compose(PaySlipForm.Title, PaySlipForm.Layout(batch)), Paper.A4Portrait);
        await File.WriteAllBytesAsync(Path.Combine(dir, "slips.pdf"), pdf);
    }
}

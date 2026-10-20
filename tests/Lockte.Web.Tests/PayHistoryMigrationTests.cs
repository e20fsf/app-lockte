using System.Runtime.Versioning;
using Lockte.Hr;
using Lockte.Migration;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// เครื่องมือย้ายข้อมูล: ผลค่าแรง (#51) — ค่าแรง + สำเนาของ ค่าแรง from 1 ม.ค. 2568 into one table, from a fake
/// <c>พนักงาน.accdb</c>, read back through <see cref="PayPeriodDirectory"/>; plus the row counts and the totals per period.
/// </summary>
[Collection(MigrationDbCollection.Name)]
[SupportedOSPlatform("windows")]
public sealed class PayHistoryMigrationTests(MigrationDbFixture db) : IDisposable
{
    private static readonly DateTime Oct12 = new(2026, 10, 12);
    private static readonly DateTime Sep27 = new(2026, 9, 27);

    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-pay-history-test-");

    public void Dispose() => work.Delete(recursive: true);

    private static (string, object?)[] Period(DateTime from, DateTime to, DateTime payDate)
        => [("FROM_DATE", from), ("TO_DATE", to), ("PAY_D", payDate)];

    private static (string, object?)[] Copy(string? id, DateTime from, DateTime to, DateTime payDate, int w, int payW, int payOt = 0, int ot = 0)
        => [("ID", id), ("W", w), ("PAY_W", payW), ("PAY_OT", payOt), ("OT", ot), ("TT_OT", 0), ("SUNDAY", 2), ("HOLIDAY", 0), ("HOT", 0.0),
            .. Period(from, to, payDate)];

    /// <summary>
    /// ค่าแรง of the open period 12–26 ต.ค. 2569 (D001, O001) and the history: D001's copy of it (W and OT rounded to Long),
    /// O001's copy with another PAY_W, the closed period 27 ก.ย.–11 ต.ค. (D001 twice, F001, an orphan, a blank ID), the
    /// งวด 2 ช่วง at new year 2569 whose first part ends on the second's TO_DATE, a period of 2567 (stays) and a row without
    /// FROM_DATE.
    /// </summary>
    private static FakeLegacyFile WithPay()
    {
        var (oct26, nov1, oct11, oct16) = (new DateTime(2026, 10, 26), new DateTime(2026, 11, 1), new DateTime(2026, 10, 11), new DateTime(2026, 10, 16));
        var (dec27, jan1, jan11, jan16) = (new DateTime(2025, 12, 27), new DateTime(2026, 1, 1), new DateTime(2026, 1, 11), new DateTime(2026, 1, 16));
        const string history = "สำเนาของ ค่าแรง";
        return FakeLegacyFile.Standard()
            .Row("ค่าแรง", [("ID", "D001"), ("SA", 400.0), ("W", 12.5), ("L", 1.0), ("OT", 3.5), ("TT_OT", 8.0), ("PAY_W", 5400), ("PAY_OT", 1263),
                ("HOT", 52.5), ("SUNDAY", 2.0), ("HOLIDAY", 0.0), .. Period(Oct12, oct26, nov1)])
            .Row("ค่าแรง", [("ID", "o001"), ("SA", 15600.0), ("W", 13.0), ("L", 0.0), ("OT", 0.0), ("TT_OT", 0.0), ("PAY_W", 7800), ("PAY_OT", 0),
                ("HOT", 0.0), ("SUNDAY", 2.0), ("HOLIDAY", 0.0), .. Period(Oct12, oct26, nov1)])
            .Row(history, [.. Copy("D001", Oct12, oct26, nov1, w: 12, payW: 5400, payOt: 1263, ot: 4).Where(c => c.Item1 is not ("TT_OT" or "HOT")),
                ("TT_OT", 8), ("HOT", 52.5)])
            .Row(history, Copy("O001", Oct12, oct26, nov1, w: 13, payW: 7700))
            .Row(history, Copy("D001", Sep27, oct11, oct16, w: 11, payW: 4400, payOt: 210, ot: 3))
            .Row(history, Copy("F001", Sep27, oct11, oct16, w: 13, payW: 10500))
            .Row(history, Copy("d001", Sep27, oct11, oct16, w: 1, payW: 400))
            .Row(history, Copy("X998", Sep27, oct11, oct16, w: 13, payW: 5200))
            .Row(history, Copy(null, Sep27, oct11, oct16, w: 1, payW: 1))
            .Row(history, Copy("D001", dec27, jan11, jan16, w: 3, payW: 1200))
            .Row(history, Copy("D001", jan1, jan11, jan16, w: 8, payW: 3200))
            .Row(history, Copy("D001", new DateTime(2024, 12, 27), new DateTime(2024, 12, 31), new DateTime(2025, 1, 16), w: 3, payW: 1185))
            .Row(history, ("ID", "D001"), ("W", 1), ("PAY_W", 400));
    }

    private async Task<MigrationReport> RunAsync()
    {
        var source = Path.Combine(work.FullName, "pay.accdb");
        if (!File.Exists(source)) WithPay().Save(source);
        return await MigrationTool.RunAsync(db.Services, source, null, Path.Combine(work.FullName, "out"));
    }

    private async Task<T> ReadAsync<T>(Func<TestScope, Task<T>> read)
    {
        await using var scope = db.Scope("hr1");
        return await read(scope);
    }

    private Task<int> HistoryCountAsync()
        => ReadAsync(async s => (await s.Get<HistoryService>().SearchAsync(new HistoryFilter(Max: 100_000))).Count);

    private Task<IReadOnlyList<PayPeriodDto>> PeriodsAsync() => ReadAsync(s => s.Get<PayPeriodDirectory>().ListAsync());

    private Task<IReadOnlyList<EmployeePay>> PayOfAsync(PayPeriodDto period) => ReadAsync(s => s.Get<PayPeriodDirectory>().GetPayAsync(period.Id));

    [Fact]
    public async Task Pay_of_every_period_from_2568_is_loaded_into_its_period_and_counted()
    {
        var historyBefore = await HistoryCountAsync();
        var report = await RunAsync();

        var periods = await PeriodsAsync();
        Assert.Equal(
        [
            (new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 26), PayPeriodStep.Set), // the open period of ตั้งวันที่ตัดWEEK keeps its step
            (new DateOnly(2026, 9, 27), new DateOnly(2026, 10, 11), PayPeriodStep.SlipPrinted),
            (new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 11), PayPeriodStep.SlipPrinted),
            (new DateOnly(2025, 12, 27), new DateOnly(2025, 12, 31), PayPeriodStep.SlipPrinted), // ช่วงที่ 1 of BR-008, TO_DATE fixed
        ], periods.Select(p => (p.From, p.To, p.CompletedStep)));

        var open = await PayOfAsync(periods[0]);
        Assert.Equal(["D001", "O001"], open.Select(p => p.Code));
        var d001 = open[0];
        Assert.Equal((400.0, 12.5, 1.0, 3.5, 8, 5400m, 1263m, 52.5m, 2), // from ค่าแรง: SA and L, W and OT not rounded
            (d001.Rate, d001.Normal, d001.PaidLeave, d001.Overtime, d001.HolidayHours, d001.PayNormal, d001.PayOvertime, d001.Hot, d001.WeeklyHolidays));
        Assert.Equal(7800m, open[1].PayNormal); // not the history's copy with another PAY_W

        var closed = await PayOfAsync(periods[1]);
        Assert.Equal(["D001", "F001"], closed.Select(p => p.Code));
        Assert.Equal((0.0, 11.0, 0.0, 3.0, 4400m, 210m), // the history keeps no SA or L
            (closed[0].Rate, closed[0].Normal, closed[0].PaidLeave, closed[0].Overtime, closed[0].PayNormal, closed[0].PayOvertime));
        var report2 = await ReadAsync(s => s.Get<PayPeriodDirectory>().GetPayReportAsync(periods[1].Id, null));
        Assert.Equal("ไดคาสท์", report2!.Lines.Single(l => l.Pay.Code == "D001").DepartmentName); // แผนก of today

        var count = Assert.Single(report.Tables, t => t.NewTable == "hr.PayResults");
        Assert.Equal((13, 2, 5, 6, 6), (count.SourceRows, count.Excluded, count.Rejected, count.Loaded, count.InDatabase));
        Assert.True(report.Balanced, string.Join("\n", report.Tables.Where(t => !t.Balanced).Select(t => t.ToString())
            .Concat(report.MonthlyTotals.Where(m => !m.Balanced).Select(m => m.ToString()))));
        var sep27 = report.MonthlyTotals.Single(m => m.Table == "hr.PayResults" && m.Month == "งวด 2026-09-27" && m.Measure == "ค่าแรง (PAY_W)");
        Assert.Equal((4400.0 + 10500 + 400 + 5200 + 1, 4400.0 + 10500, 4400.0 + 10500), (sep27.Source, sep27.Expected, sep27.InDatabase));
        Assert.Equal(historyBefore, await HistoryCountAsync());

        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-2" && e.Key == "2026-10-12 O001" && e.Problem.Contains("ค่าแรง"));
        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-2" && e.Key == "2026-09-27 D001");
        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-6" && e.Key == "2026-09-27 X998");
        Assert.Contains(report.Exceptions, e => e.Rule == "DQ-1" && e.Table == "สำเนาของ ค่าแรง");
        Assert.Contains(report.Exceptions, e => e.Key == "งวด 2025-12-27" && e.Action.Contains("2025-12-31"));
        // SA/L: one line per period loaded from the history, not one per employee
        Assert.Equal(["งวด 2025-12-27", "งวด 2026-01-01", "งวด 2026-09-27"],
            report.Exceptions.Where(e => e.Action == "โหลดเป็น 0").Select(e => e.Key).Order());
    }

    [Fact] // ลบแล้วโหลดใหม่ รันซ้ำได้
    public async Task A_second_run_loads_the_same_results_again()
    {
        await RunAsync();
        var report = await RunAsync();

        Assert.Equal(4, (await PeriodsAsync()).Count);
        Assert.Equal(6, Assert.Single(report.Tables, t => t.NewTable == "hr.PayResults").InDatabase);
    }
}

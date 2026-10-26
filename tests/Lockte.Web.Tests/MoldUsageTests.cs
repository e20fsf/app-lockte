using Lockte.Prod;
using Lockte.Prod.Data;
using Microsoft.EntityFrameworkCore;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Tests;

/// <summary>
/// ตัดยอดแม่พิมพ์ (SCR-18, BR-008) and the reports of it (RPT-15) through the public services of module ผลิต (#125). Fictional data only; every test works in
/// a year of its own because a cut takes the production of a date range from every mold and the tests of this collection share a database.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class MoldUsageTests(LocalDbFixture db)
{
    private static int year = 2400;
    private static int counter = Random.Shared.Next(10000, 60000);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static DateOnly NewYear() => new(Interlocked.Increment(ref year), 1, 1);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    private async Task<int> MoldAsync(string prefix, int? piecesPerShot = 4, int? shots = 50000, DateOnly? end = null)
    {
        var saved = await AsAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(null, Unique(prefix), "แม่พิมพ์ใช้งาน", null, PiecesPerShot: piecesPerShot, MaintenanceShots: shots,
            Cost: 1, Owner: MoldRegistry.DefaultPerson, EndDate: end)));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return saved.Id!.Value;
    }

    private async Task<int> ProductAsync()
    {
        var saved = await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("ZU"), "สินค้าแม่พิมพ์", null, Unit: "ชิ้น")));
        return saved.Id!.Value;
    }

    private async Task<int> ReportAsync(string department, DateOnly date, int product, int mold, decimal good, decimal? reject = null)
    {
        var saved = await AsAsync(s => s.Get<ProductionReportRegistry>().SaveAsync(new ProductionReportInput(null, null, department, date, department + Interlocked.Increment(ref counter).ToString("00000"), null,
            [new ProductionReportLineInput(null, null, product, null, null, null, 8m, null, null, good, reject, null, mold, null)]), ProductionSaveMode.Save));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return saved.Id!.Value;
    }

    private Task<MoldCutResult> CutAsync(DateOnly from, DateOnly cut) => AsAsync(s => s.Get<MoldUsageRegistry>().CutAsync(from, cut));

    private async Task<int> CumulativeAsync(DateOnly cut, string moldCode)
        => (await AsAsync(s => s.Get<MoldUsageDirectory>().GetCumulativeAsync(cut))).SingleOrDefault(l => l.MoldCode == moldCode)?.Cumulative ?? -1;

    private async Task<string> CodeAsync(int mold) => (await AsAsync(s => s.Get<MoldDirectory>().GetAsync(mold)))!.Code;

    // ---------- BR-008 ----------

    [Fact] // AC-12, BR-008 #1: 250 ÷ 4 → 62, rounded down; the good quantity only — rejects do not count
    public void The_shots_are_the_good_quantity_over_the_pieces_per_shot_rounded_down()
    {
        Assert.Equal(62, MoldUsageRegistry.CountShots(250, 4));
        Assert.Equal(62, MoldUsageRegistry.CountShots(251, 4));
        Assert.Equal(0, MoldUsageRegistry.CountShots(3, 4));
        Assert.Null(MoldUsageRegistry.CountShots(250, null));
        Assert.Null(MoldUsageRegistry.CountShots(250, 0));
        Assert.True(MoldUsageRegistry.IsCounted("BF-029-1/1"));
        Assert.True(MoldUsageRegistry.IsCounted("db-001"));
        Assert.True(MoldUsageRegistry.IsCounted("D01-2"));
        Assert.False(MoldUsageRegistry.IsCounted("CA-010"));
        Assert.False(MoldUsageRegistry.IsCounted("CP-010"));
    }

    [Fact] // BR-008 #2: carried over 10,000 + 1,200 used in the month → 11,200 at the cut; the rejects of the line are not shots
    public async Task The_cut_adds_the_month_to_what_was_carried_over()
    {
        var start = NewYear();
        var mold = await MoldAsync("BF-");
        var code = await CodeAsync(mold);
        var product = await ProductAsync();
        await ReportAsync("D", start.AddDays(5), product, mold, 40000, 900); // 10,000 shots in January (the rejects are not shots)
        var first = await CutAsync(start, start.AddMonths(1).AddDays(-1));
        Assert.True(first.Succeeded);
        Assert.Equal(10000, await CumulativeAsync(start.AddMonths(1).AddDays(-1), code));

        await ReportAsync("D", start.AddMonths(1).AddDays(3), product, mold, 4800); // 1,200 shots in February
        var second = await CutAsync(start.AddMonths(1), start.AddMonths(2).AddDays(-1));

        Assert.True(second.Succeeded);
        Assert.Equal(11200, await CumulativeAsync(start.AddMonths(2).AddDays(-1), code));
        Assert.Equal(10000, await CumulativeAsync(start.AddMonths(1).AddDays(-1), code)); // the history of the first cut stays
        var rows = await AsAsync(s => s.Get<MoldUsageDirectory>().GetUsageAsync(mold, start, start.AddMonths(3)));
        Assert.Equal([(true, 11200)], rows.Select(r => (r.IsOpening, r.Shots))); // up to the cut only the opening is left
    }

    [Fact] // the same cut run again gives the same result (step 1 makes the period again)
    public async Task Running_the_same_cut_again_gives_the_same_totals()
    {
        var start = NewYear();
        var mold = await MoldAsync("BL-");
        var code = await CodeAsync(mold);
        var product = await ProductAsync();
        await ReportAsync("B", start.AddDays(2), product, mold, 400);
        var end = start.AddMonths(1).AddDays(-1);

        await CutAsync(start, end);
        await CutAsync(start, end);

        Assert.Equal(100, await CumulativeAsync(end, code));
        Assert.Single(await AsAsync(s => s.Get<MoldUsageDirectory>().GetUsageAsync(mold, start, end)));
    }

    [Fact] // the clutch molds are not counted (Q-34); a line whose mold has no pieces per shot is counted aside
    public async Task Clutch_molds_and_molds_without_pieces_per_shot_are_not_counted()
    {
        var start = NewYear();
        var clutch = await MoldAsync("CA-");
        var noPieces = await MoldAsync("BF-", piecesPerShot: null);
        var counted = await MoldAsync("BF-");
        var product = await ProductAsync();
        await ReportAsync("K", start.AddDays(1), product, clutch, 1000);
        await ReportAsync("D", start.AddDays(1), product, noPieces, 1000);
        await ReportAsync("D", start.AddDays(1), product, counted, 1000);

        var result = await CutAsync(start, start.AddMonths(1).AddDays(-1));

        var end = start.AddMonths(1).AddDays(-1);
        Assert.Equal(1, result.LinesWithoutPieces);
        Assert.Equal(250, await CumulativeAsync(end, await CodeAsync(counted)));
        Assert.Equal(-1, await CumulativeAsync(end, await CodeAsync(clutch)));
        Assert.Equal(-1, await CumulativeAsync(end, await CodeAsync(noPieces)));
    }

    [Fact] // a mold with a วันที่เลิกใช้ drops out of the total at once
    public async Task A_mold_that_is_no_longer_in_use_gets_no_total()
    {
        var start = NewYear();
        var mold = await MoldAsync("BF-");
        var code = await CodeAsync(mold);
        var product = await ProductAsync();
        await ReportAsync("D", start.AddDays(1), product, mold, 400);
        await CutAsync(start, start.AddMonths(1).AddDays(-1));
        var current = (await AsAsync(s => s.Get<MoldDirectory>().GetAsync(mold)))!;
        var ended = await AsAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(mold, current.Code, current.Name, current.RowVersion, current.PiecesPerShot, current.MaintenanceShots,
            Cost: 1, Owner: MoldRegistry.DefaultPerson, EndDate: start.AddMonths(1))));
        Assert.True(ended.Succeeded);
        await ReportAsync("D", start.AddMonths(1).AddDays(1), product, mold, 400);

        await CutAsync(start.AddMonths(1), start.AddMonths(2).AddDays(-1));

        Assert.Equal(-1, await CumulativeAsync(start.AddMonths(2).AddDays(-1), code));
        Assert.Equal(100, await CumulativeAsync(start.AddMonths(1).AddDays(-1), code));
    }

    [Fact] // ตัดซ้ำ: the usage is cleared, the totals from the cut date on are dropped and it starts again from the total of the previous cut
    public async Task A_second_cut_starts_again_from_the_previous_cut()
    {
        var start = NewYear();
        var mold = await MoldAsync("BF-");
        var code = await CodeAsync(mold);
        var product = await ProductAsync();
        await ReportAsync("D", start.AddDays(1), product, mold, 400); // 100
        var februaryReport = await ReportAsync("D", start.AddMonths(1).AddDays(1), product, mold, 800); // 200
        var january31 = start.AddMonths(1).AddDays(-1);
        var february28 = start.AddMonths(2).AddDays(-1);
        await CutAsync(start, january31);
        await CutAsync(start.AddMonths(1), february28);
        Assert.Equal(300, await CumulativeAsync(february28, code));

        // the February report is corrected afterwards: 800 → 1,200 (300 shots)
        var report = (await AsAsync(s => s.Get<ProductionReportDirectory>().GetAsync(februaryReport)))!;
        var line = report.Lines.Single();
        Assert.True((await AsAsync(s => s.Get<ProductionReportRegistry>().SaveAsync(new ProductionReportInput(report.Id, report.RowVersion, "D", report.ReportDate, report.ReportNo, null,
            [new ProductionReportLineInput(line.Id, line.RowVersion, product, null, null, null, null, null, null, 1200m, null, null, mold, null)]), ProductionSaveMode.Save))).Succeeded);

        var again = await AsAsync(s => s.Get<MoldUsageRegistry>().RecutAsync(start.AddMonths(1), february28, january31));

        Assert.True(again.Succeeded);
        Assert.Equal(400, await CumulativeAsync(february28, code));
        Assert.Equal(100, await CumulativeAsync(january31, code)); // the earlier cut is as it was
        var cutoffs = (await AsAsync(s => s.Get<MoldUsageDirectory>().GetCumulativeAsync(february28))).Count(l => l.MoldCode == code);
        Assert.Equal(1, cutoffs); // replaced, not added to
    }

    [Fact] // ตัดซ้ำ needs a total of the previous cut to start from
    public async Task A_second_cut_without_a_previous_total_is_refused()
    {
        var start = NewYear();

        var result = await AsAsync(s => s.Get<MoldUsageRegistry>().RecutAsync(start, start.AddMonths(1).AddDays(-1), start.AddDays(-1)));

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
    }

    [Fact] // the start of the period cannot be after the cut date
    public async Task The_start_must_not_be_after_the_cut_date()
    {
        var start = NewYear();

        var result = await CutAsync(start.AddDays(10), start);

        Assert.False(result.Succeeded);
    }

    // ---------- RPT-15 ----------

    [Fact] // การใช้งานของพิมพ์ถึงวันที่: code, name, shots, the criterion ("50,000 SHOT" …) and the age (÷ 2, Q-34); STOCKพิมพ์: the head names the mold and the range
    public async Task The_reports_list_the_totals_with_the_criterion_and_one_mold_in_a_range()
    {
        var start = NewYear();
        var mold = await MoldAsync("BF-", shots: 50000);
        var code = await CodeAsync(mold);
        var product = await ProductAsync();
        await ReportAsync("D", start.AddDays(2), product, mold, 400);
        await ReportAsync("D", start.AddDays(9), product, mold, 800);
        var end = start.AddMonths(1).AddDays(-1);

        await CutAsync(start, end.AddDays(-15)); // the cut is in the middle of the month
        var cumulative = await AsAsync(s => MoldUsageReports.CumulativeAsync(s.Get<MoldUsageDirectory>(), end.AddDays(-15)));
        var stock = await AsAsync(s => MoldUsageReports.StockAsync(s.Get<MoldUsageDirectory>(), s.Get<MoldDirectory>(), mold, start, end));

        var line = cumulative.Lines.Single(l => l.MoldCode == code);
        Assert.Equal((300, "50,000 SHOT", 150m), (line.Cumulative, line.Criterion, line.Age));
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<MoldCumulativeView>(new() { ["Report"] = cumulative }));
        Assert.Contains("50,000 SHOT", html);
        Assert.Contains(code, html);
        Assert.NotNull(stock);
        Assert.Equal(300, stock!.Total);
        var stockHtml = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<MoldStockView>(new() { ["Report"] = stock }));
        Assert.Contains(code, stockHtml);
        Assert.Contains("ยอดยกมา", stockHtml);
        var nothing = await AsAsync(s => MoldUsageReports.CumulativeAsync(s.Get<MoldUsageDirectory>(), start.AddYears(-1)));
        Assert.Contains("ไม่มีข้อมูล", System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<MoldCumulativeView>(new() { ["Report"] = nothing })));
    }

    [Fact] // the criterion as printed
    public void The_criterion_is_whichever_of_the_three_the_mold_has()
    {
        Assert.Equal("50,000 SHOT", MoldUsageDirectory.Criterion(50000, null, null));
        Assert.Equal("100,000 ชิ้น", MoldUsageDirectory.Criterion(null, 100000, null));
        Assert.Equal("1 ปี", MoldUsageDirectory.Criterion(null, null, 1));
        Assert.Null(MoldUsageDirectory.Criterion(null, null, null));
    }

    [Fact] // one person at a time
    public async Task A_cut_while_another_runs_is_refused()
    {
        var start = NewYear();
        MoldCutResult busy;
        await using (var holder = db.Scope("qa2"))
        {
            var context = holder.Get<ProdDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sp_getapplock @Resource = 'lockte:prod:mold-cut', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;");
            busy = await CutAsync(start, start.AddMonths(1).AddDays(-1));
            await transaction.RollbackAsync();
        }

        Assert.True(busy.Busy);
        Assert.False((await CutAsync(start, start.AddMonths(1).AddDays(-1))).Busy);
    }
}

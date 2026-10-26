using System.Diagnostics;
using Lockte.Prod;
using Lockte.Prod.Data;
using Lockte.SharedKernel;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Tests;

/// <summary>
/// ตัดสต็อกสิ้นงวด (SCR-15, BR-007) and the reports of it (RPT-13, RPT-14 data) through the public services of module ผลิต (#123). A cut closes everything dated up to its date
/// for <i>every</i> product, so each test cuts in a year of its own long ago (nothing else in the database is dated then) and looks only at its own products.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class StockClosingTests(LocalDbFixture db)
{
    private static int year = 1960;

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static int NewYear() => Interlocked.Increment(ref year);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("store1");
        return await action(scope);
    }

    private async Task<int> ProductAsync(decimal? unitCost = null, string? code = null)
    {
        var saved = await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, code ?? Unique("ZC"), "สินค้าตัดสต็อก", null, Unit: "คู่", UnitCost: unitCost)));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return saved.Id!.Value;
    }

    private Task<Lockte.SharedKernel.ServiceResult> AddAsync(int product, decimal quantity, DateOnly date, string? remark = null)
        => AsAsync(s => s.Get<StockRegistry>().SaveAsync(new StockEntryInput(null, null, date, "X" + Random.Shared.Next(1000), null, product, quantity, null, remark)));

    private Task<StockCutResult> CutAsync(DateOnly date) => AsAsync(s => s.Get<StockClosingRegistry>().CutAsync(date));

    private Task<decimal> BalanceAsync(int product) => AsAsync(s => s.Get<StockDirectory>().GetBalanceAsync(product));

    private async Task<IReadOnlyList<StockEntryDto>> OpenEntriesAsync(int product) => await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(product));

    private async Task<IReadOnlyList<StockEntryDto>> AllEntriesAsync(int product) => await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(product, includeClosed: true));

    /// <summary>BR-007 #1: opening 500 at the end of October, +200 and −300 in November.</summary>
    private async Task<(int Product, DateOnly October, DateOnly November)> OctoberAndNovemberAsync(decimal? unitCost = null)
    {
        var y = NewYear();
        var (october, november) = (new DateOnly(y, 10, 31), new DateOnly(y, 11, 30));
        var product = await ProductAsync(unitCost);
        await AddAsync(product, 500, october.AddDays(-3));
        await CutAsync(october);
        await AddAsync(product, 200, new DateOnly(y, 11, 10), "ผลผลิต");
        var withdrawal = await AsAsync(s => s.Get<StockWithdrawalRegistry>().SaveAsync(new WithdrawalSlipInput(Random.Shared.Next(100000).ToString(), new DateOnly(y, 11, 15), null, [new WithdrawalLineInput(null, null, 1, product, 300)])));
        Assert.True(withdrawal.Succeeded);
        return (product, october, november);
    }

    // ---------- BR-007 ----------

    [Fact] // BR-007 #1, AC-10: opening 500, +200, −300 → 400 carried over; what was closed is marked, not moved
    public async Task The_cut_carries_over_the_balance_and_closes_what_it_took()
    {
        var (product, october, november) = await OctoberAndNovemberAsync();

        var result = await CutAsync(november);

        Assert.True(result.Succeeded);
        Assert.Equal(400m, await BalanceAsync(product));
        var open = await OpenEntriesAsync(product);
        var opening = Assert.Single(open);
        Assert.Equal((StockEntryKind.OpeningBalance, 400m, november, "stock"), (opening.Kind, opening.Quantity, opening.Date, opening.DocumentNo));
        var all = await AllEntriesAsync(product);
        Assert.Equal(5, all.Count); // opening October (closed by this cut), the first manual entry (closed by October), +200, −300 and the new opening
        Assert.Equal(3, all.Count(e => e.ClosedOn == november)); // the October opening, +200 and −300
        Assert.All(all.Where(e => e.Kind == StockEntryKind.OpeningBalance), e => Assert.Contains(e.Date, new DateOnly?[] { october, november }));
    }

    [Fact] // BR-007 #2: the same cut twice gives the same opening, once
    public async Task Cutting_the_same_date_again_gives_the_same_balance()
    {
        var (product, _, november) = await OctoberAndNovemberAsync();
        await CutAsync(november);

        await CutAsync(november);

        Assert.Equal(400m, await BalanceAsync(product));
        Assert.Single(await OpenEntriesAsync(product));
        Assert.Single((await AllEntriesAsync(product)).Where(e => e.Kind == StockEntryKind.OpeningBalance && e.Date == november));
    }

    [Fact] // an entry dated up to the cut date that comes in after the cut still counts (⚠ Q-47 kept)
    public async Task An_entry_with_an_old_date_that_comes_in_late_is_counted_by_the_next_cut()
    {
        var (product, _, november) = await OctoberAndNovemberAsync();
        await CutAsync(november);

        await AddAsync(product, 50, november.AddDays(-10));
        Assert.Equal(450m, await BalanceAsync(product));
        await CutAsync(november);

        Assert.Equal(450m, await BalanceAsync(product));
        Assert.Equal(450m, Assert.Single(await OpenEntriesAsync(product)).Quantity);
    }

    [Fact] // a product at 0 gets an opening of 0; the entries dated after the cut are left open
    public async Task A_product_at_zero_is_carried_over_and_later_entries_stay_open()
    {
        var y = NewYear();
        var cut = new DateOnly(y, 6, 30);
        var product = await ProductAsync();
        await AddAsync(product, 10, cut.AddDays(-5));
        await AddAsync(product, -10, cut.AddDays(-2));
        await AddAsync(product, 7, cut.AddDays(1)); // after the cut

        await CutAsync(cut);

        var open = await OpenEntriesAsync(product);
        Assert.Equal(2, open.Count);
        Assert.Contains(open, e => e.Kind == StockEntryKind.OpeningBalance && e.Quantity == 0);
        Assert.Contains(open, e => e.Quantity == 7 && e.Date == cut.AddDays(1));
        Assert.Equal(7m, await BalanceAsync(product));
    }

    [Fact] // ตัดซ้ำ: the opening of the previous period is open again, the withdrawal and the entry closed by the first cut do not come back (⚠ Q-29 kept)
    public async Task A_second_cut_does_not_bring_back_what_the_first_one_closed()
    {
        var (product, october, november) = await OctoberAndNovemberAsync();
        await CutAsync(november);
        Assert.Equal(400m, await BalanceAsync(product));
        await AddAsync(product, 200, new DateOnly(november.Year, 11, 10), "ผลผลิตโอนใหม่"); // the transfer made again makes the production again

        var again = await AsAsync(s => s.Get<StockClosingRegistry>().RecutAsync(november, october));

        Assert.True(again.Succeeded);
        Assert.Equal(700m, await BalanceAsync(product)); // 500 + 200: the withdrawal of 300 is not taken off (and the old +200 is not counted twice)
        var open = Assert.Single(await OpenEntriesAsync(product));
        Assert.Equal((StockEntryKind.OpeningBalance, november), (open.Kind, open.Date));
        Assert.Single((await AllEntriesAsync(product)).Where(e => e.Kind == StockEntryKind.Withdrawal && e.ClosedOn != null)); // still closed
        Assert.Equal(1, (await AllEntriesAsync(product)).Count(e => e.Kind == StockEntryKind.OpeningBalance && e.Date == october));
    }

    [Fact] // a second cut needs the opening of the previous period
    public async Task A_second_cut_without_the_previous_opening_is_refused()
    {
        var y = NewYear();

        var result = await AsAsync(s => s.Get<StockClosingRegistry>().RecutAsync(new DateOnly(y, 11, 30), new DateOnly(y, 10, 31)));

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
    }

    [Fact] // one person at a time
    public async Task A_cut_while_another_runs_is_refused()
    {
        var y = NewYear();
        StockCutResult busy;
        await using (var holder = db.Scope("store2"))
        {
            var context = holder.Get<ProdDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sp_getapplock @Resource = 'lockte:prod:stock-close', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;");
            busy = await CutAsync(new DateOnly(y, 12, 31));
            await transaction.RollbackAsync();
        }

        Assert.True(busy.Busy);
    }

    [Fact] // the year in one cut, on a ledger of about 20,000 entries, is fast enough (#29: one cut at 31 Dec, no month by month)
    public async Task One_cut_over_a_big_ledger_is_fast()
    {
        var y = NewYear();
        var product = await ProductAsync();
        await AsAsync(async s =>
        {
            await s.Get<ProdDbContext>().Database.ExecuteSqlAsync($"""
                INSERT INTO prod.StockEntries (Date, DocumentNo, [LineNo], ProductId, Quantity, Kind, ClosedOn, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt)
                SELECT TOP (20000) DATEFROMPARTS({y}, 1 + (ROW_NUMBER() OVER (ORDER BY (SELECT 1)) % 12), 15), 'P' + CAST(ROW_NUMBER() OVER (ORDER BY (SELECT 1)) AS varchar(10)), 1,
                       {product}, 1, 1, NULL, 'test', SYSDATETIMEOFFSET(), 'test', SYSDATETIMEOFFSET()
                FROM sys.all_objects a CROSS JOIN sys.all_objects b
                """);
            return 0;
        });
        Assert.Equal(20000m, await BalanceAsync(product));

        var clock = Stopwatch.StartNew();
        var result = await CutAsync(new DateOnly(y, 12, 31));
        clock.Stop();

        Assert.True(result.Succeeded);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"the cut took {clock.Elapsed}");
        Assert.Equal(20000m, await BalanceAsync(product));
        Assert.Single(await OpenEntriesAsync(product));
    }

    // ---------- RPT-13, RPT-14 ----------

    [Fact] // RPT-13: only the balances that are not 0, by code, value = quantity × cost; no cost → no value; a negative balance takes its value off the total
    public async Task The_cut_report_lists_the_balances_with_their_value()
    {
        var y = NewYear();
        var cut = new DateOnly(y, 12, 31);
        var (codeA, codeB, codeC, codeZero) = (Unique("ZA"), Unique("ZB"), Unique("ZC"), Unique("ZD"));
        var a = await ProductAsync(10.5m, codeA);
        var b = await ProductAsync(null, codeB);
        var c = await ProductAsync(2m, codeC);
        var zero = await ProductAsync(5m, codeZero);
        await AddAsync(a, 100, cut.AddDays(-9));
        await AddAsync(b, 40, cut.AddDays(-8));
        await AddAsync(c, -30, cut.AddDays(-7));
        await AddAsync(zero, 5, cut.AddDays(-6));
        await AddAsync(zero, -5, cut.AddDays(-5));
        await CutAsync(cut);

        var everything = await AsAsync(s => StockCutReports.BuildAsync(s.Get<StockClosingDirectory>(), cut));
        var report = everything with { Lines = everything.Lines.Where(l => new[] { codeA, codeB, codeC, codeZero }.Contains(l.ProductCode)).ToList() }; // the openings of earlier tests are in it too

        Assert.Equal(new[] { codeA, codeB, codeC }.Order(), report.Lines.Select(l => l.ProductCode).Order());
        Assert.Equal(1050m, report.Lines.Single(l => l.ProductCode == codeA).Value);
        Assert.Null(report.Lines.Single(l => l.ProductCode == codeB).Value);
        Assert.Equal(-60m, report.Lines.Single(l => l.ProductCode == codeC).Value);
        Assert.Equal(990m, report.TotalValue);
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<StockCutReportView>(new() { ["Report"] = report }));
        Assert.Contains("รวมทั้งหมด", html);
        Assert.Contains("1,050.00", html);
        Assert.DoesNotContain(codeZero, html);
        Assert.Contains("วันที่", StockCutReports.TitleOf(cut));
        var empty = await AsAsync(s => StockCutReports.BuildAsync(s.Get<StockClosingDirectory>(), cut.AddYears(-30)));
        Assert.Contains("ไม่มีข้อมูล", System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<StockCutReportView>(new() { ["Report"] = empty })));
    }

    [Fact] // RPT-14: the opening balances of every cut in a range, a row per product and a column per cut date; the code group narrows it
    public async Task The_opening_history_has_a_column_per_cut_date()
    {
        var (product, october, november) = await OctoberAndNovemberAsync();
        await CutAsync(november);

        var (dates, rows) = await AsAsync(s => s.Get<StockClosingDirectory>().GetOpeningHistoryAsync(october, november));
        var code = (await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(product))).First().ProductCode;
        var narrowed = await AsAsync(s => s.Get<StockClosingDirectory>().GetOpeningHistoryAsync(october, november, code));

        Assert.Equal([october, november], dates.Where(d => d == october || d == november));
        var row = rows.Single(r => r.ProductCode == code);
        Assert.Equal((500m, 400m), (row.Balances[october], row.Balances[november]));
        Assert.Equal([code], narrowed.Rows.Select(r => r.ProductCode));
        Assert.Contains(november, await AsAsync(s => s.Get<StockClosingDirectory>().GetCutDatesAsync()));
    }

    [Fact] // RPT-14 as a table: a column per cut date, a blank where the product had no opening; Excel holds the same cells
    public async Task The_opening_history_is_a_table_with_a_column_per_cut_date()
    {
        var (product, october, november) = await OctoberAndNovemberAsync();
        await CutAsync(november);
        var code = (await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(product))).First().ProductCode;

        var table = await AsAsync(s => StockCutReports.OpeningHistoryAsync(s.Get<StockClosingDirectory>(), october, november, code));

        Assert.Equal(["รหัส", "ชื่อสินค้า", "หน่วย", ThaiDate.Format(october), ThaiDate.Format(november)], table.Columns.Select(c => c.Header));
        var row = Assert.Single(table.Rows);
        Assert.Equal((code, 500m, 400m), ((string)row[0]!, (decimal)row[3]!, (decimal)row[4]!));
        Assert.Contains(code, table.Filter);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(XlsxWriter.Write(table)));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
        var none = await AsAsync(s => StockCutReports.OpeningHistoryAsync(s.Get<StockClosingDirectory>(), october.AddYears(-30), october.AddYears(-30).AddDays(3), null));
        Assert.Empty(none.Rows);
    }
}

using Lockte.Hr;
using Lockte.Prod;
using Lockte.Prod.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Tests;

/// <summary>
/// สมุดสต็อก (BR-006, SCR-14) and โอนผลผลิตเข้าสต็อก (BR-005, SCR-16) through the public services of module ผลิต (#119). Fictional data only; every
/// transfer test works in a month of its own because the tests of this collection share a database and a transfer replaces a whole range.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class StockTests(LocalDbFixture db)
{
    private static int year = 2300;
    private static int counter = Random.Shared.Next(10000, 60000);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static DateOnly NewMonth() => new(Interlocked.Increment(ref year), 8, 1);

    private static string FreeNumber(string department) => department + Interlocked.Increment(ref counter).ToString("00000");

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action, string user = "store1")
    {
        await using var scope = db.Scope(user);
        return await action(scope);
    }

    private async Task<int> NewProductAsync()
    {
        var saved = await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("ZS"), "สินค้าสต็อก", null, Unit: "คู่")));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return saved.Id!.Value;
    }

    private Task<Lockte.SharedKernel.ServiceResult> AddAsync(int product, decimal quantity, string? remark = null, string? document = null, DateOnly? date = null, int? employee = null)
        => AsAsync(s => s.Get<StockRegistry>().SaveAsync(new StockEntryInput(null, null, date ?? new DateOnly(2026, 8, 1), document, null, product, quantity, employee, remark)));

    private Task<decimal> BalanceAsync(int product) => AsAsync(s => s.Get<StockDirectory>().GetBalanceAsync(product));

    private static ProductionReportLineInput Line(int product, decimal good, decimal? reject = null)
        => new(null, null, product, null, null, null, 8m, null, null, good, reject, null, null, null);

    private async Task<int> ReportAsync(string department, DateOnly date, params ProductionReportLineInput[] lines)
    {
        var saved = await AsAsync(s => s.Get<ProductionReportRegistry>().SaveAsync(new ProductionReportInput(null, null, department, date, FreeNumber(department), null, lines), ProductionSaveMode.Save));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return saved.Id!.Value;
    }

    private Task<StockTransferResult> TransferAsync(DateOnly from, DateOnly to) => AsAsync(s => s.Get<StockRegistry>().TransferProductionAsync(from, to));

    // ---------- BR-006 ----------

    [Fact] // BR-006 #1: opening 500, production +200, withdrawal −300 → 400
    public async Task The_balance_is_the_sum_of_the_entries()
    {
        var product = await NewProductAsync();
        await AddAsync(product, 500, "ยอดยกมา", "stock");
        await AddAsync(product, 200, null, "D69205");
        await AddAsync(product, -300, null, "4343");

        Assert.Equal(400m, await BalanceAsync(product));
    }

    [Fact] // BR-006 #2: a hand entry of −50 with the remark "เสียหาย" → 350; the kind is a field of its own
    public async Task A_hand_entry_keeps_its_sign_and_its_remark_apart_from_the_kind()
    {
        var product = await NewProductAsync();
        await AddAsync(product, 400, "ยกมา", "stock");

        var added = await AddAsync(product, -50, "เสียหาย");

        Assert.True(added.Succeeded);
        Assert.Equal(350m, await BalanceAsync(product));
        var entry = (await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(product))).Single(e => e.Remark == "เสียหาย");
        Assert.Equal((StockEntryKind.Manual, -50m), (entry.Kind, entry.Quantity));
    }

    [Fact] // the balance may go below 0 with no warning (⚠ BR-006 kept); all the entries of the product are listed, every field can be changed
    public async Task The_balance_can_go_negative_and_every_field_of_an_entry_can_be_changed()
    {
        var product = await NewProductAsync();
        var id = (await AddAsync(product, 10, "เดิม", "A1")).Id!.Value;
        await AddAsync(product, -30, null, "A2");
        Assert.Equal(-20m, await BalanceAsync(product));

        var entry = (await AsAsync(s => s.Get<StockDirectory>().GetAsync(id)))!;
        var other = await NewProductAsync();
        var changed = await AsAsync(s => s.Get<StockRegistry>().SaveAsync(new StockEntryInput(id, entry.RowVersion, new DateOnly(2026, 9, 9), "B9", 3, other, 99, null, "แก้แล้ว")));

        Assert.True(changed.Succeeded);
        var after = (await AsAsync(s => s.Get<StockDirectory>().GetAsync(id)))!;
        Assert.Equal((new DateOnly(2026, 9, 9), "B9", 3, other, 99m, "แก้แล้ว"), (after.Date, after.DocumentNo, after.LineNo, after.ProductId, after.Quantity, after.Remark));
        Assert.Equal(-30m, await BalanceAsync(product));
        Assert.Equal(99m, await BalanceAsync(other));
    }

    [Fact] // two people on the same entry: the second is told
    public async Task A_stale_entry_is_refused()
    {
        var product = await NewProductAsync();
        var id = (await AddAsync(product, 10)).Id!.Value;
        var loaded = (await AsAsync(s => s.Get<StockDirectory>().GetAsync(id)))!;
        Assert.True((await AsAsync(s => s.Get<StockRegistry>().SaveAsync(new StockEntryInput(id, loaded.RowVersion, loaded.Date, null, null, product, 11, null, null)))).Succeeded);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => AsAsync(s => s.Get<StockRegistry>().SaveAsync(new StockEntryInput(id, loaded.RowVersion, loaded.Date, null, null, product, 12, null, null))));
    }

    [Fact] // the employee comes from the register; a product must be in the register
    public async Task The_employee_and_the_product_must_be_in_the_registers()
    {
        var product = await NewProductAsync();

        var unknownEmployee = await AddAsync(product, 1, employee: 999_999_999);
        var unknownProduct = await AddAsync(999_999_999, 1);
        var code = "S" + Random.Shared.Next(100, 1000);
        var employee = await AsAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput { Code = code, Name = "พนักงานสต็อกทดสอบ" }), "hr1");
        var known = employee.Succeeded ? await AddAsync(product, 1, employee: employee.Id) : null;

        Assert.False(unknownEmployee.Succeeded);
        Assert.False(unknownProduct.Succeeded);
        if (known is not null) Assert.True(known.Succeeded); // a clash of the random code leaves this check out
    }

    [Fact] // deleting an entry
    public async Task An_entry_can_be_deleted()
    {
        var product = await NewProductAsync();
        var id = (await AddAsync(product, 10)).Id!.Value;
        var entry = (await AsAsync(s => s.Get<StockDirectory>().GetAsync(id)))!;

        Assert.True((await AsAsync(s => s.Get<StockRegistry>().DeleteAsync(id, entry.RowVersion))).Succeeded);

        Assert.Equal(0m, await BalanceAsync(product));
    }

    // ---------- BR-005 ----------

    [Fact] // BR-005 #1: good 980 + reject 20 → the stock gets 1,000 (Q-17 kept), for every one of the seven departments
    public async Task The_transfer_takes_the_total_of_every_department()
    {
        var month = NewMonth();
        var product = await NewProductAsync();
        foreach (var department in ProductionDepartments.All)
            await ReportAsync(department.Code, month.AddDays(3), Line(product, 980, 20));

        var result = await TransferAsync(month, month.AddMonths(1).AddDays(-1));

        Assert.Equal((7, 7, 0), (result.Reports, result.Entries, result.LinesWithoutProduct));
        Assert.False(result.OverlapsClosedPeriod);
        Assert.Equal(7000m, await BalanceAsync(product));
        var entries = await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(product));
        Assert.All(entries, e => Assert.Equal((StockEntryKind.Production, 1000m), (e.Kind, e.Quantity)));
    }

    [Fact] // BR-005 #2 and AC-07: running the same range again — also after a report was changed — replaces, never doubles
    public async Task The_transfer_of_a_range_run_again_gives_the_same_result()
    {
        var month = NewMonth();
        var product = await NewProductAsync();
        var first = await ReportAsync("D", month.AddDays(1), Line(product, 100));
        await ReportAsync("T", month.AddDays(9), Line(product, 50, 10));
        await TransferAsync(month, month.AddMonths(1).AddDays(-1));
        await TransferAsync(month, month.AddMonths(1).AddDays(-1));
        Assert.Equal(160m, await BalanceAsync(product));

        // a report of the month is changed and the month is transferred again: the entries are replaced, not added to
        var report = (await AsAsync(s => s.Get<ProductionReportDirectory>().GetAsync(first)))!;
        var line = report.Lines.Single();
        var changed = await AsAsync(s => s.Get<ProductionReportRegistry>().SaveAsync(new ProductionReportInput(report.Id, report.RowVersion, "D", report.ReportDate, report.ReportNo, null,
            [new ProductionReportLineInput(line.Id, line.RowVersion, product, null, null, null, null, null, null, 120m, null, null, null, null)]), ProductionSaveMode.Save));
        Assert.True(changed.Succeeded);
        await TransferAsync(month, month.AddMonths(1).AddDays(-1));

        Assert.Equal(180m, await BalanceAsync(product));
        Assert.Equal(2, (await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(product))).Count);
    }

    [Fact] // a hand entry with the remark "pd" is not a production entry: the transfer does not delete it (ข้อสมมติ ช)
    public async Task A_hand_entry_with_the_remark_pd_survives_the_transfer()
    {
        var month = NewMonth();
        var product = await NewProductAsync();
        await AddAsync(product, 5, "pd", date: month.AddDays(2));
        await ReportAsync("D", month.AddDays(2), Line(product, 100));

        await TransferAsync(month, month.AddMonths(1).AddDays(-1));
        await TransferAsync(month, month.AddMonths(1).AddDays(-1));

        Assert.Equal(105m, await BalanceAsync(product));
    }

    [Fact] // the range decides: reports outside it are not touched, and an earlier transfer of them stays
    public async Task Only_the_reports_of_the_range_are_transferred()
    {
        var month = NewMonth();
        var product = await NewProductAsync();
        await ReportAsync("D", month.AddDays(1), Line(product, 100));
        await ReportAsync("D", month.AddDays(20), Line(product, 300));

        await TransferAsync(month, month.AddDays(10));
        Assert.Equal(100m, await BalanceAsync(product));
        await TransferAsync(month.AddDays(15), month.AddDays(25));

        Assert.Equal(400m, await BalanceAsync(product));
    }

    [Fact] // a line with no product cannot make an entry; it is counted
    public async Task A_line_without_a_product_is_counted_and_left_out()
    {
        var month = NewMonth();
        var product = await NewProductAsync();
        await ReportAsync("P", month.AddDays(1), Line(product, 100), new ProductionReportLineInput(null, null, null, "สินค้าไม่มีรหัส", null, null, null, null, null, 7m, null, null, null, null));

        var result = await TransferAsync(month, month.AddMonths(1).AddDays(-1));

        Assert.Equal((1, 1, 1), (result.Reports, result.Entries, result.LinesWithoutProduct));
    }

    [Fact] // the warning: the range holds production that a period closing already carried over — not a stop, and the closed entries are left alone
    public async Task A_range_that_overlaps_a_closed_period_warns_and_leaves_the_closed_entries()
    {
        var month = NewMonth();
        var product = await NewProductAsync();
        await ReportAsync("D", month.AddDays(1), Line(product, 100));
        await TransferAsync(month, month.AddMonths(1).AddDays(-1));
        await AsAsync(async s =>
        {
            await s.Get<ProdDbContext>().Database.ExecuteSqlAsync($"UPDATE prod.StockEntries SET ClosedOn = {month.AddMonths(1)} WHERE ProductId = {product}");
            return 0;
        });

        var again = await TransferAsync(month, month.AddMonths(1).AddDays(-1));

        Assert.True(again.OverlapsClosedPeriod);
        Assert.Equal(1, again.ClosedEntriesInRange);
        Assert.Equal(100m, await BalanceAsync(product)); // a new open entry; the closed one is not in the balance
        Assert.Single(await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(product)));
        Assert.Equal(2, (await AsAsync(s => s.Get<StockDirectory>().GetEntriesAsync(product, includeClosed: true))).Count);
    }

    [Fact] // one person at a time: while another transfer holds the lock this one is told it is busy and changes nothing
    public async Task A_transfer_while_another_runs_is_refused()
    {
        var month = NewMonth();
        var product = await NewProductAsync();
        await ReportAsync("D", month.AddDays(1), Line(product, 100));

        StockTransferResult busy;
        await using (var holder = db.Scope("store2"))
        {
            var context = holder.Get<ProdDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sp_getapplock @Resource = 'lockte:prod:stock-transfer', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0;");
            busy = await TransferAsync(month, month.AddMonths(1).AddDays(-1));
            await transaction.RollbackAsync();
        }

        Assert.True(busy.Busy);
        Assert.Equal(0m, await BalanceAsync(product));
        Assert.False((await TransferAsync(month, month.AddMonths(1).AddDays(-1))).Busy);
    }

    // ---------- BR-019 (#121) ----------

    [Fact] // BR-019 (ข้อสมมติ ค): the report is changed after the transfer — the stock stays as it was until the range is transferred again
    public async Task Changing_a_transferred_report_leaves_the_stock_until_the_range_is_transferred_again()
    {
        var month = NewMonth();
        var product = await NewProductAsync();
        var id = await ReportAsync("D", month.AddDays(3), Line(product, 100), Line(product, 40));
        await TransferAsync(month, month.AddMonths(1).AddDays(-1));
        Assert.Equal(140m, await BalanceAsync(product));
        var report = (await AsAsync(s => s.Get<ProductionReportDirectory>().GetAsync(id)))!;
        Assert.True(report.InStock);

        // the first line becomes 300 + 20 rejects; the second line is deleted (left out of the lines saved)
        var first = report.Lines[0];
        var changed = await AsAsync(s => s.Get<ProductionReportRegistry>().SaveAsync(new ProductionReportInput(report.Id, report.RowVersion, "D", report.ReportDate, report.ReportNo, null,
            [new ProductionReportLineInput(first.Id, first.RowVersion, product, null, null, null, null, null, null, 300m, 20m, null, null, null)]), ProductionSaveMode.Save));
        Assert.True(changed.Succeeded);

        Assert.Equal(140m, await BalanceAsync(product)); // the stock is not touched
        var after = (await AsAsync(s => s.Get<ProductionReportDirectory>().GetAsync(id)))!;
        Assert.Equal([320m], after.Lines.Select(l => l.TotalQuantity)); // the total is worked out again
        await TransferAsync(month, month.AddMonths(1).AddDays(-1));
        Assert.Equal(320m, await BalanceAsync(product));
    }

    [Fact] // SCR-11: reports are found by their number (the start of it is enough) or by their date
    public async Task Reports_are_found_by_number_or_by_date()
    {
        var month = NewMonth();
        var product = await NewProductAsync();
        var first = await ReportAsync("D", month.AddDays(1), Line(product, 10));
        await ReportAsync("T", month.AddDays(1), Line(product, 20));
        await ReportAsync("T", month.AddDays(8), Line(product, 30));
        var number = (await AsAsync(s => s.Get<ProductionReportDirectory>().GetAsync(first)))!.ReportNo;

        var byNumber = await AsAsync(s => s.Get<ProductionReportDirectory>().SearchAsync(number.ToLowerInvariant(), null));
        var byDate = await AsAsync(s => s.Get<ProductionReportDirectory>().SearchAsync(null, month.AddDays(1)));
        var byBoth = await AsAsync(s => s.Get<ProductionReportDirectory>().SearchAsync(number, month.AddDays(8)));

        Assert.Equal([first], byNumber.Select(r => r.Id));
        Assert.Equal(("ไดคาสท์", 1, false), (byNumber[0].DepartmentName, byNumber[0].Lines, byNumber[0].StockedAt is not null));
        Assert.Equal(2, byDate.Count);
        Assert.Empty(byBoth);
    }
}

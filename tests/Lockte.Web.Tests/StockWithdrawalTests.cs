using Lockte.Hr;
using Lockte.Prod;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Tests;

/// <summary>เบิกเพื่อผลิต (SCR-13, BR-018) and รายงานใบเบิก (RPT-18) through the public services of module ผลิต (#120). Fictional data only.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class StockWithdrawalTests(LocalDbFixture db)
{
    private static int year = 2500;
    private static int counter = Random.Shared.Next(100000, 900000);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static string NewNumber() => Interlocked.Increment(ref counter).ToString();

    private static DateOnly NewMonth() => new(Interlocked.Increment(ref year), 6, 1);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("store1");
        return await action(scope);
    }

    private async Task<int> ProductAsync(string unit = "คู่")
    {
        var saved = await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("ZW"), "สินค้าเบิก", null, Unit: unit)));
        return saved.Id!.Value;
    }

    private Task<Lockte.SharedKernel.ServiceResult> SaveAsync(string number, DateOnly? date, int? employee, params WithdrawalLineInput[] lines)
        => AsAsync(s => s.Get<StockWithdrawalRegistry>().SaveAsync(new WithdrawalSlipInput(number, date, employee, lines)));

    private Task<decimal> BalanceAsync(int product) => AsAsync(s => s.Get<StockDirectory>().GetBalanceAsync(product));

    private static WithdrawalLineInput Line(int product, decimal? quantity, int? lineNo = null, int? id = null, byte[]? rowVersion = null) => new(id, rowVersion, lineNo, product, quantity);

    private async Task<int> EmployeeAsync()
    {
        for (var i = 0; i < 30; i++)
        {
            var code = "W" + Random.Shared.Next(100, 1000);
            var saved = await AsAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput { Code = code, Name = "พนักงานเบิกทดสอบ" }));
            if (saved.Succeeded) return saved.Id!.Value;
        }
        throw new InvalidOperationException("no free employee code");
    }

    // ---------- BR-018 ----------

    [Fact] // BR-018 #1, AC-08: 700 typed → the stock goes down by 700
    public async Task A_positive_quantity_takes_the_stock_down()
    {
        var product = await ProductAsync();
        await AsAsync(s => s.Get<StockRegistry>().SaveAsync(new StockEntryInput(null, null, new DateOnly(2026, 8, 1), "stock", null, product, 1000, null, "ยอดยกมา")));

        var saved = await SaveAsync(NewNumber(), new DateOnly(2026, 9, 1), null, Line(product, 700));

        Assert.True(saved.Succeeded);
        Assert.Equal(300m, await BalanceAsync(product));
    }

    [Fact] // BR-018 #2 and #3 (ข้อสมมติ ข): −700 typed is kept −700, not turned round; and never +700
    public async Task A_negative_quantity_is_kept_negative_too()
    {
        var product = await ProductAsync();
        var number = NewNumber();

        await SaveAsync(number, new DateOnly(2026, 9, 1), null, Line(product, -700));

        Assert.Equal(-700m, await BalanceAsync(product));
        var slip = (await AsAsync(s => s.Get<StockWithdrawalDirectory>().GetSlipAsync(number)))!;
        Assert.Equal(-700m, slip.Lines.Single().Quantity);
    }

    [Fact] // "ใบต่อไป" drops the lines with no quantity; the head is copied to every line; the balance may go below 0 (⚠ kept)
    public async Task Lines_without_quantity_are_dropped_and_the_head_goes_to_every_line()
    {
        var product = await ProductAsync();
        var other = await ProductAsync();
        var employee = await EmployeeAsync();
        var number = NewNumber();

        var saved = await SaveAsync(number, new DateOnly(2026, 9, 2), employee, Line(product, 5, 1), Line(other, null, 2), Line(other, 8, 3));

        Assert.True(saved.Succeeded);
        var slip = (await AsAsync(s => s.Get<StockWithdrawalDirectory>().GetSlipAsync(number)))!;
        Assert.Equal([1, 3], slip.Lines.Select(l => l.LineNo));
        Assert.Equal((new DateOnly(2026, 9, 2), employee), (slip.Date, slip.EmployeeId));
        Assert.Equal(-5m, await BalanceAsync(product));
        Assert.Equal(-8m, await BalanceAsync(other));
    }

    [Fact] // the slip is chosen by its number and its lines are changed; a line whose quantity is cleared is deleted
    public async Task A_slip_can_be_changed_by_its_number_and_a_cleared_line_is_deleted()
    {
        var product = await ProductAsync();
        var other = await ProductAsync();
        var number = NewNumber();
        await SaveAsync(number, new DateOnly(2026, 9, 3), null, Line(product, 10, 1), Line(other, 20, 2));
        var slip = (await AsAsync(s => s.Get<StockWithdrawalDirectory>().GetSlipAsync(number)))!;
        var first = slip.Lines[0];
        var second = slip.Lines[1];

        var changed = await SaveAsync(number, slip.Date, null, Line(product, 15, first.LineNo, first.Id, first.RowVersion), Line(other, null, second.LineNo, second.Id, second.RowVersion));

        Assert.True(changed.Succeeded);
        var after = (await AsAsync(s => s.Get<StockWithdrawalDirectory>().GetSlipAsync(number)))!;
        Assert.Equal([-15m], after.Lines.Select(l => l.Quantity));
        Assert.Equal(0m, await BalanceAsync(other));
        Assert.Contains(number, await AsAsync(s => s.Get<StockWithdrawalDirectory>().GetSlipNumbersAsync(number)));
    }

    [Fact] // two people on the same slip: the second is told
    public async Task A_stale_line_is_refused()
    {
        var product = await ProductAsync();
        var number = NewNumber();
        await SaveAsync(number, new DateOnly(2026, 9, 4), null, Line(product, 10, 1));
        var line = (await AsAsync(s => s.Get<StockWithdrawalDirectory>().GetSlipAsync(number)))!.Lines.Single();
        Assert.True((await SaveAsync(number, new DateOnly(2026, 9, 4), null, Line(product, 11, 1, line.Id, line.RowVersion))).Succeeded);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => SaveAsync(number, new DateOnly(2026, 9, 4), null, Line(product, 12, 1, line.Id, line.RowVersion)));
    }

    [Fact] // the number is required; the employee and the products come from the registers
    public async Task The_number_the_employee_and_the_products_are_checked()
    {
        var product = await ProductAsync();

        Assert.False((await SaveAsync(" ", new DateOnly(2026, 9, 4), null, Line(product, 1))).Succeeded);
        Assert.False((await SaveAsync(NewNumber(), new DateOnly(2026, 9, 4), 999_999_999, Line(product, 1))).Succeeded);
        Assert.False((await SaveAsync(NewNumber(), new DateOnly(2026, 9, 4), null, Line(999_999_999, 1))).Succeeded);
    }

    // ---------- RPT-18 ----------

    [Fact] // the slips of the range with their lines, the quantity positive, totals per unit; the head says the range
    public async Task The_report_lists_the_slips_of_a_range()
    {
        var month = NewMonth();
        var pairs = await ProductAsync("คู่");
        var boxes = await ProductAsync("ตลับ");
        var employee = await EmployeeAsync();
        var first = NewNumber();
        var second = NewNumber();
        await SaveAsync(first, month.AddDays(2), employee, Line(pairs, 100, 1), Line(boxes, 30, 2));
        await SaveAsync(second, month.AddDays(5), null, Line(pairs, 50, 1));
        await SaveAsync(NewNumber(), month.AddMonths(2), null, Line(pairs, 9, 1)); // outside the range

        var report = await AsAsync(s => WithdrawalReports.BuildAsync(s.Get<StockWithdrawalDirectory>(), s.Get<IProductionEmployeeLookup>(), month, month.AddMonths(1).AddDays(-1)));

        Assert.Equal([first, second], report.Slips.Select(s => s.DocumentNo));
        Assert.Equal((100m, 30m), (report.Slips[0].Lines[0].Quantity, report.Slips[0].Lines[1].Quantity));
        Assert.NotNull(report.Slips[0].Employee);
        Assert.Null(report.Slips[1].Employee);
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<WithdrawalReportView>(new() { ["Report"] = report }));
        Assert.Contains($"เลขที่ {first}", html);
        Assert.Contains("150", html); // 100 + 50 คู่, apart from the ตลับ
        Assert.Contains("รวมทั้งหมด 2 ใบ", html);
        var empty = await AsAsync(s => WithdrawalReports.BuildAsync(s.Get<StockWithdrawalDirectory>(), s.Get<IProductionEmployeeLookup>(), month.AddYears(-9), month.AddYears(-9).AddDays(3)));
        Assert.Contains("ไม่มีข้อมูล", System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<WithdrawalReportView>(new() { ["Report"] = empty })));
    }
}

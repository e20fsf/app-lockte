using Lockte.Hr;
using Lockte.Purch;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Web.Tests;

/// <summary>เบิกวัตถุดิบ (#226, SCR-14, SCR-15, BR-040, AC-17, AC-41): always stored negative, no balance check, a warning instead, the slip editable.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class StockIssueTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsStoreAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("store1");
        return await action(scope);
    }

    private async Task<int> MaterialAsync(bool main = true)
        => (await AsStoreAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("is").ToUpperInvariant(), "วัตถุดิบเบิก", null, "กก.", IsMain: main)))).Id!.Value;

    private async Task<string> EmployeeCodeAsync()
    {
        for (var i = 0; i < 30; i++)
        {
            var code = "I" + Random.Shared.Next(1000, 10000);
            if ((await AsStoreAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput { Code = code, Name = "พนักงานเบิกทดสอบ" }))).Succeeded) return code;
        }
        throw new InvalidOperationException("no free employee code");
    }

    private static string NewDocument() => Random.Shared.Next(10, 99) + "/" + Guid.NewGuid().ToString("N")[..5];

    private Task<StockIssueResult> IssueAsync(string document, string? employee, params StockIssueLineInput[] lines)
        => AsStoreAsync(s => s.Get<StockIssueRegistry>().SaveAsync(new StockIssueInput(document, new DateOnly(2026, 10, 1), employee, lines)));

    private Task<decimal> BalanceAsync(int material) => AsStoreAsync(s => s.Get<StockDirectory>().GetBalanceAsync(material));

    private Task AddStockAsync(int material, decimal quantity)
        => AsStoreAsync(s => s.Get<StockLedger>().SaveAsync(new StockEntryInput(null, new DateOnly(2026, 9, 1), material, quantity, null, "stock")));

    [Fact]
    public async Task Typing_90_takes_90_out_of_the_stock_AC_17_AC_41()
    {
        var material = await MaterialAsync();
        await AddStockAsync(material, 1000m);
        var result = await IssueAsync(NewDocument(), await EmployeeCodeAsync(), new StockIssueLineInput(material, 90m));
        Assert.True(result.Succeeded);
        Assert.Equal(910m, await BalanceAsync(material));
        var row = Assert.Single(await AsStoreAsync(s => s.Get<StockDirectory>().GetEntriesAsync(material)), e => e.Kind == StockEntryKind.Issue);
        Assert.Equal(-90m, row.Quantity);
        Assert.NotNull(row.EmployeeId);
    }

    [Fact]
    public async Task Typing_minus_5_stays_an_issue_of_5_BR_040_2()
    {
        var material = await MaterialAsync();
        Assert.True((await IssueAsync(NewDocument(), null, new StockIssueLineInput(material, -5m))).Succeeded);
        Assert.Equal(-5m, await BalanceAsync(material));
    }

    [Fact]
    public async Task An_issue_of_a_material_never_received_or_beyond_the_balance_is_saved_with_a_warning_BR_040_3_4()
    {
        var notMain = await MaterialAsync(main: false);
        var warned = await IssueAsync(NewDocument(), null, new StockIssueLineInput(notMain, 1m));
        Assert.True(warned.Succeeded);
        Assert.Equal(-1m, await BalanceAsync(notMain));
        Assert.Contains(warned.Warnings, w => w.Contains("ไม่ใช่วัตถุดิบหลัก"));
        Assert.Contains(warned.Warnings, w => w.Contains("ติดลบ"));

        var main = await MaterialAsync();
        await AddStockAsync(main, 10m);
        var over = await IssueAsync(NewDocument(), null, new StockIssueLineInput(main, 25m));
        Assert.True(over.Succeeded);
        Assert.Equal(-15m, await BalanceAsync(main));
        Assert.Single(over.Warnings);
        Assert.Empty((await IssueAsync(NewDocument(), null, new StockIssueLineInput(main, 0m))).Warnings);
    }

    [Fact]
    public async Task Blank_lines_are_left_out_and_a_slip_without_lines_is_not_made()
    {
        var material = await MaterialAsync();
        var other = await MaterialAsync();
        var document = NewDocument();
        var result = await IssueAsync(document, null, new StockIssueLineInput(material, 3m), new StockIssueLineInput(other, null), new StockIssueLineInput(other, 0m));
        Assert.Equal(1, result.Saved);
        Assert.Single(await AsStoreAsync(s => s.Get<StockIssueDirectory>().GetDocumentAsync(document)));

        var empty = NewDocument();
        Assert.True((await IssueAsync(empty, null, new StockIssueLineInput(other, null))).Succeeded);
        Assert.Empty(await AsStoreAsync(s => s.Get<StockIssueDirectory>().GetDocumentAsync(empty)));
    }

    [Fact]
    public async Task An_employee_not_in_hr_a_blank_number_and_a_material_outside_the_register_are_refused()
    {
        var material = await MaterialAsync();
        var noEmployee = await IssueAsync(NewDocument(), "ZZ-NOT-THERE", new StockIssueLineInput(material, 1m));
        Assert.Equal(["ไม่พบรหัสพนักงาน 'ZZ-NOT-THERE' ในทะเบียนพนักงาน"], noEmployee.Errors);
        Assert.Contains("กรุณากรอกเลขที่ใบเบิก", (await IssueAsync(" ", null, new StockIssueLineInput(material, 1m))).Errors);
        Assert.Contains("กรุณาเลือกรหัสวัตถุดิบที่มีในทะเบียน", (await IssueAsync(NewDocument(), null, new StockIssueLineInput(int.MaxValue, 1m))).Errors);
        Assert.Equal(0m, await BalanceAsync(material));
    }

    [Fact]
    public async Task A_saved_slip_is_found_by_its_number_and_its_lines_can_be_edited_but_stay_negative_SCR_15()
    {
        var material = await MaterialAsync();
        var document = NewDocument();
        await IssueAsync(document, null, new StockIssueLineInput(material, 10m), new StockIssueLineInput(material, 4m));
        Assert.Contains(document, await AsStoreAsync(s => s.Get<StockIssueDirectory>().GetDocumentsAsync()));

        var rows = await AsStoreAsync(s => s.Get<StockIssueDirectory>().GetDocumentAsync(document));
        Assert.Equal([-10m, -4m], rows.Select(r => r.Quantity));
        var first = rows[0];
        Assert.True((await AsStoreAsync(s => s.Get<StockIssueRegistry>().UpdateLineAsync(new StockIssueLineUpdate(first.Id, first.RowVersion, first.EntryDate, "1", 7m, null, "แก้จำนวน")))).Succeeded);
        Assert.Equal(-11m, await BalanceAsync(material)); // -7 and -4
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsStoreAsync(s => s.Get<StockIssueRegistry>().UpdateLineAsync(new StockIssueLineUpdate(first.Id, first.RowVersion, first.EntryDate, "1", 9m, null, null))));
        Assert.Contains("กรุณากรอกจำนวนที่เบิก (ถ้าไม่ต้องการบรรทัดนี้ ใช้ปุ่มลบ)",
            (await AsStoreAsync(s => s.Get<StockIssueRegistry>().UpdateLineAsync(new StockIssueLineUpdate(rows[1].Id, rows[1].RowVersion, rows[1].EntryDate, "2", 0m, null, null)))).Errors);
    }

    [Fact]
    public async Task Only_rows_of_kind_issue_are_edited_here()
    {
        var material = await MaterialAsync();
        var adjustment = (await AsStoreAsync(s => s.Get<StockLedger>().SaveAsync(new StockEntryInput(null, new DateOnly(2026, 9, 1), material, 5m, null)))).Id!.Value;
        var row = (await AsStoreAsync(s => s.Get<StockDirectory>().GetAsync(adjustment)))!;
        var result = await AsStoreAsync(s => s.Get<StockIssueRegistry>().UpdateLineAsync(new StockIssueLineUpdate(row.Id, row.RowVersion, row.EntryDate, null, 5m, null, null)));
        Assert.Equal(["แก้ได้เฉพาะรายการชนิดเบิก"], result.Errors);
    }
}

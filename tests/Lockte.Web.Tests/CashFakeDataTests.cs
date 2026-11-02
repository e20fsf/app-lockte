using Lockte.Cash;
using Lockte.Web.Reports.Cash;
using Lockte.Web.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Tests;

/// <summary>
/// ชุดสาธิตตัวเลขของโปรแกรมเงินสดย่อย (#247) in a database of its own (the shared test database is left alone): the set gives the figures of acceptance.md through the services and the report
/// builders, it cannot be added twice, and a second database gets the same set.
/// </summary>
public sealed class CashFakeDataTests : IAsyncLifetime
{
    private readonly LocalDbFixture db = new();
    private CashFakeSummary summary = null!;

    private AsyncServiceScope NewScope()
    {
        var scope = db.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentUser>().UserName = "seed";
        return scope;
    }

    private async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = NewScope();
        return await action(scope.ServiceProvider);
    }

    public async Task InitializeAsync()
    {
        await db.InitializeAsync();
        summary = await new CashFakeDataSeeder(NewScope).SeedAsync();
    }

    public async Task DisposeAsync() => await db.DisposeAsync();

    [Fact]
    public void The_set_is_small_and_complete()
    {
        Assert.Equal((10, 3, 7, 5, 2), (summary.ExpenseTypes, summary.Buyers, summary.ScrapItems, summary.Receipts, summary.Remitted));
        Assert.Equal(18, summary.ExpenseLines);
    }

    [Fact] // AC-07, BR-006 #1: 1–3 Sep 2026, nine types, 28,710.00 and with the 21,456 of the last period 50,166.00
    public async Task AC07_the_summary_of_1_to_3_September()
    {
        var rows = await RunAsync(sp => sp.GetRequiredService<ExpenseLineDirectory>().SummaryByTypeAsync(CashFakeDataSeeder.Sep1, CashFakeDataSeeder.Sep1.AddDays(2)));
        Assert.Equal(9, rows.Count);
        Assert.Equal(CashFakeDataSeeder.Ac07Types.OrderBy(t => t.Name, StringComparer.Create(Lockte.SharedKernel.ThaiDate.Culture, true)).Select(t => (t.Name, t.Total)), rows.Select(r => (r.TypeName, r.Total)));
        var report = new ExpenseSummaryReport(CashFakeDataSeeder.Sep1, CashFakeDataSeeder.Sep1.AddDays(2), rows, 21456m);
        Assert.Equal((28710m, 50166m, "(ห้าหมื่นหนึ่งร้อยหกสิบหกบาทถ้วน)"), (report.Total, report.Grand, report.GrandInWords));
    }

    [Fact] // AC-06, BR-005 #1: the three lines of 09/30 → 140.00
    public async Task AC06_the_voucher_09_30()
    {
        var lines = await RunAsync(sp => sp.GetRequiredService<ExpenseLineDirectory>().ListByDateAsync(new DateOnly(2026, 9, 9)));
        Assert.Equal(["09/30", "09/30", "09/30"], lines.Select(l => l.VoucherNo));
        Assert.False(PaymentVoucherForm.MixesVouchers(lines));
        Assert.Equal(140m, PaymentVoucherForm.Total(lines));
        Assert.Equal("(หนึ่งร้อยสี่สิบบาทถ้วน)", PaymentVoucherForm.TotalInWords(140m));
        Assert.Single(PaymentVoucherForm.Layout(lines));
    }

    [Fact] // AC-20, BR-008 #2: 11 September
    public async Task AC20_the_purchases_of_11_September()
    {
        var day = new DateOnly(2026, 9, 11);
        var report = PurchaseListReports.Build(day, day, await RunAsync(sp => sp.GetRequiredService<ExpenseLineDirectory>().ListAsync(day, day)));
        Assert.Equal([("09/34", 633m), ("09/35", 90m), ("09/36", 80m)], report.Lines.Select(l => (l.VoucherNo, l.Amount)));
        Assert.Equal(803m, report.Total);
    }

    [Fact] // AC-10, AC-15, BR-018 #2
    public async Task AC10_AC15_the_receipts_and_the_sales_of_a_month()
    {
        var r29 = (await RunAsync(sp => sp.GetRequiredService<ScrapReceiptDirectory>().GetByNoAsync("69029")))!;
        Assert.Equal((5, 2270m, "(สองพันสองร้อยเจ็ดสิบบาทถ้วน)"), (r29.Lines.Count, r29.Total, r29.TotalInWords));

        var (from, to) = (new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        var september = ScrapSalesReports.Build(from, to, await RunAsync(sp => sp.GetRequiredService<ScrapReceiptDirectory>().ListAsync(from, to)));
        Assert.Equal((6, 3270m), (september.Count, september.Total));
        var day = new DateOnly(2026, 8, 5);
        var august = ScrapSalesReports.Build(day, day, await RunAsync(sp => sp.GetRequiredService<ScrapReceiptDirectory>().ListAsync(day, day)));
        Assert.Equal((4, 1360m), (august.Count, august.Total));
    }

    [Fact] // AC-13: 69025–69026 were sent; 69027–69029 wait; closing 69028–69029 takes 3,270.00 and leaves 69027
    public async Task AC13_closing_69028_to_69029()
    {
        var waiting = await RunAsync(sp => sp.GetRequiredService<ScrapRemittanceDirectory>().UnremittedAsync());
        Assert.Equal(["69027", "69028", "69029"], waiting.Select(r => r.No));
        Assert.Equal(4630m, waiting.Sum(r => r.Total));

        var closed = await RunAsync(sp => sp.GetRequiredService<ScrapRemittanceRegistry>().CloseAsync(new("69028", "69029", new DateOnly(2026, 10, 5))));
        Assert.True(closed.Succeeded);
        Assert.Equal((2, 3270m), (closed.Count, closed.Total));
        Assert.Equal(["69027"], (await RunAsync(sp => sp.GetRequiredService<ScrapRemittanceDirectory>().UnremittedAsync())).Select(r => r.No));
    }

    [Fact]
    public async Task The_set_cannot_be_added_twice_and_a_second_database_gets_the_same_one()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CashFakeDataSeeder(NewScope).SeedAsync());

        await using var other = new LocalDbFixture();
        await other.InitializeAsync();
        AsyncServiceScope OtherScope()
        {
            var scope = other.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<TestCurrentUser>().UserName = "seed";
            return scope;
        }
        Assert.Equal(summary, await new CashFakeDataSeeder(OtherScope).SeedAsync());

        async Task<(string, decimal)[]> Totals(Func<AsyncServiceScope> scopeOf)
        {
            await using var scope = scopeOf();
            var sums = await scope.ServiceProvider.GetRequiredService<ExpenseLineDirectory>().SummaryByTypeAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
            var receipts = await scope.ServiceProvider.GetRequiredService<ScrapReceiptDirectory>().ListAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
            return [.. sums.Select(s => (s.TypeName, s.Total)), .. receipts.Select(r => (r.No, r.Total))];
        }
        Assert.Equal(await Totals(NewScope), await Totals(OtherScope));
    }
}

using Lockte.Purch;
using Lockte.Web.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Tests;

/// <summary>
/// ชุดข้อมูลปลอมของโปรแกรมวัตถุดิบ (#269) in a database of its own (the shared test database is left alone): the set is complete, has the edge cases the acceptance tests use, and cannot be added twice.
/// </summary>
public sealed class PurchFakeDataTests : IAsyncLifetime
{
    private readonly LocalDbFixture db = new();
    private PurchFakeSummary summary = null!;

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
        summary = await new PurchFakeDataSeeder(NewScope).SeedAsync();
    }

    public async Task DisposeAsync() => await db.DisposeAsync();

    [Fact]
    public void The_set_has_the_size_of_the_real_one()
    {
        Assert.Equal(40, summary.Suppliers);
        Assert.InRange(summary.Materials, 190, 230);
        Assert.True(summary.Requests >= 80 && summary.Orders == summary.Requests, $"{summary.Requests} requests, {summary.Orders} orders");
        Assert.True(summary.Receipts > 50 && summary.Issues > 10 && summary.Evaluations > 0);
    }

    [Fact]
    public async Task The_edge_cases_of_the_acceptance_tests_are_there()
    {
        var materials = await RunAsync(sp => sp.GetRequiredService<MaterialDirectory>().GetAllAsync());
        Assert.False(materials.Single(m => m.Code == "E0054").IsMain);
        Assert.Contains(materials, m => m.Code.StartsWith("SP") && m.IsMain);
        foreach (var group in new[] { "BA", "BR", "CH", "SL", "SP" }) Assert.Contains(materials, m => m.Code.StartsWith(group));

        // exactly 5,000.00 is within the small-order limit, and an order of more than ten lines exists
        var small = await RunAsync(sp => sp.GetRequiredService<PurchaseOrderDirectory>().GetSmallOrderLinesAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 5000m));
        Assert.Contains(small, l => l.AmountWithTax == 5000m);
        var orders = await RunAsync(sp => sp.GetRequiredService<PurchaseOrderDirectory>().SearchAsync(null, 500));
        Assert.Contains(orders, o => o.LineCount > 10);

        // a request in the shape D69-0001, an outstanding order, a failed delivery, a negative carried-forward balance
        var requests = await RunAsync(sp => sp.GetRequiredService<PurchaseRequestDirectory>().SearchAsync(null, 500));
        Assert.Contains(requests, r => r.RequestNo.EndsWith("69-0001"));
        Assert.NotEmpty(await RunAsync(sp => sp.GetRequiredService<GoodsReceiptDirectory>().GetAllOutstandingAsync()));
        var received = await RunAsync(sp => sp.GetRequiredService<GoodsReceiptDirectory>().GetReceivedLinesAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        Assert.Contains(received, l => l.Failed);
        var balances = await RunAsync(sp => sp.GetRequiredService<StockClosingDirectory>().GetBalancesAsync(summary.CutDate));
        Assert.Contains(balances, b => b.Quantity < 0);
    }

    [Fact]
    public async Task The_set_is_made_the_same_every_time_and_is_refused_a_second_time()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PurchFakeDataSeeder(NewScope).SeedAsync());
        var suppliers = await RunAsync(sp => sp.GetRequiredService<SupplierDirectory>().GetAllAsync());
        Assert.Equal(Enumerable.Range(1, 40).Select(i => $"FS{i:000}"), suppliers.Select(s => s.Code));
        Assert.All(suppliers, s => Assert.StartsWith("ผู้ขายทดสอบ", s.Name));
    }
}

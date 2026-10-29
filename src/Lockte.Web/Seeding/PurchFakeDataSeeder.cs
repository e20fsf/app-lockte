using Lockte.Org;
using Lockte.Purch;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Seeding;

/// <summary>What <see cref="PurchFakeDataSeeder"/> made.</summary>
public sealed record PurchFakeSummary(int Suppliers, int Materials, int Requests, int Orders, int Receipts, int Issues, int Evaluations, DateOnly CutDate);

/// <summary>
/// ชุดข้อมูลปลอมของโปรแกรมวัตถุดิบ (#269): everything is invented here — no name, address, phone, price or code of the company's real data is read or written — and the same seed always
/// gives the same data. It goes through the module's own services (so every rule of the program applies and the rows are audited), is the fixture the tests can fill a database with, and is
/// the way to fill the customer's test server (<c>Lockte.Web seed-purch-fake</c>, see docs/migration/purch-fake-data.md).
/// The shape follows the real thing: ~40 suppliers, ~200 materials (the main ones, the other groups, the resale groups, SP springs, E0054 which is not a main material), requests and orders in every
/// state (fully/partly ordered, outstanding, partly/fully received, failed, VAT NO, exactly 5,000.00, more than 10 lines), receipts with LOT and QA results, issues, one overdrawn material,
/// one ตัดสต็อก, and the evaluation of the quarter before.
/// </summary>
public sealed class PurchFakeDataSeeder(Func<AsyncServiceScope> newScope)
{
    public const int Seed = 20261029;
    public const string FirstSupplierCode = "FS001";

    /// <summary>The day of the one ตัดสต็อก; the quarter before it (April–June) is the one evaluated.</summary>
    public static readonly DateOnly CutDate = new(2026, 6, 30);

    private static readonly DateOnly QuarterFrom = new(2026, 4, 1);

    private readonly Random random = new(Seed);
    private readonly List<int> supplierIds = [];
    private readonly List<(int Id, string Code, bool Main, decimal Cost)> materials = [];
    private readonly List<(int Id, string Prefix)> departments = [];
    private readonly Dictionary<string, int> requestCounters = [];
    private int requests, orders, receipts, issues;
    private int lotCounter;

    private async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = newScope();
        return await action(scope.ServiceProvider);
    }

    public async Task<PurchFakeSummary> SeedAsync()
    {
        if (await RunAsync(async sp => (await sp.GetRequiredService<SupplierDirectory>().SearchAsync(FirstSupplierCode, 5)).Any(s => s.Code == FirstSupplierCode)))
            throw new InvalidOperationException("ชุดข้อมูลปลอมถูกเติมในฐานนี้แล้ว — ลบฐานแล้วสร้างใหม่ก่อน (Reset-PurchFake.ps1)");

        await SeedDepartmentsAsync();
        await SeedSuppliersAsync();
        await SeedMaterialsAsync();

        // the months before the cut: normal buying, every delivery with a LOT, a few that failed
        for (var i = 0; i < 40; i++) await BuyAsync(QuarterFrom.AddDays(random.Next(0, 88)), Scenario.ReceivedFully, lines: random.Next(1, 4));
        for (var i = 0; i < 6; i++) await BuyAsync(QuarterFrom.AddDays(random.Next(0, 88)), Scenario.Failed, lines: 1);
        await IssueStockAsync(QuarterFrom.AddDays(40), 12);

        // one material overdrawn at the cut: a negative balance is carried forward
        var overdrawn = materials.Last(m => m.Main && m.Code.StartsWith("R"));
        await RunAsync(async sp => await sp.GetRequiredService<StockLedger>().SaveAsync(new StockEntryInput(null, CutDate.AddDays(-3), overdrawn.Id, -5000m, null, Note: "ปรับยอดติดลบ (ข้อมูลปลอม)")));
        var closed = await RunAsync(sp => sp.GetRequiredService<StockClosingRegistry>().CloseAsync(CutDate));
        if (!closed.Succeeded) throw new InvalidOperationException(string.Join(" ", closed.Errors));

        // the quarter before is evaluated
        var evaluations = 0;
        foreach (var id in supplierIds)
        {
            var point = 3 + random.Next(0, 3);
            var result = await RunAsync(sp => sp.GetRequiredService<SupplierEvaluationRegistry>().EvaluateAsync(new SupplierEvaluationInput(id, QuarterFrom, CutDate, 2, point, Math.Min(5, point + 1))));
            if (result.Succeeded && result.Evaluation is not null) evaluations++;
        }

        // after the cut: every state of a request and an order
        var july = new DateOnly(2026, 7, 1);
        foreach (var scenario in new[] { Scenario.ReceivedFully, Scenario.ReceivedFully, Scenario.ReceivedFully, Scenario.PartlyOrdered, Scenario.Outstanding, Scenario.Outstanding, Scenario.PartlyReceived,
                                         Scenario.Failed, Scenario.VatNo, Scenario.Exactly5000, Scenario.ManyLines, Scenario.Springs, Scenario.NotMain, Scenario.Resale })
            for (var i = 0; i < 2; i++) await BuyAsync(july.AddDays(random.Next(0, 80)), scenario, lines: scenario == Scenario.ManyLines ? 12 : random.Next(1, 4));
        await IssueStockAsync(july.AddDays(20), 10);

        return new PurchFakeSummary(supplierIds.Count, materials.Count, requests, orders, receipts, issues, evaluations, CutDate);
    }

    private async Task SeedDepartmentsAsync()
    {
        var existing = await RunAsync(sp => sp.GetRequiredService<OrgDirectory>().GetDepartmentsAsync());
        if (existing.Count == 0)
        {
            var sectionId = await RunAsync(async sp => (await sp.GetRequiredService<OrgRegistry>().SaveSectionAsync(new SectionInput(null, "ฝ่ายทดสอบ", 1, null))).Id!.Value);
            foreach (var (code, name) in new[] { ("FD1", "แผนกทดสอบ 1"), ("FD2", "แผนกทดสอบ 2"), ("FD3", "แผนกทดสอบ 3") })
                await RunAsync(sp => sp.GetRequiredService<OrgRegistry>().SaveDepartmentAsync(new DepartmentInput(null, code, name, "f", 3, sectionId, null, [], null)));
            existing = await RunAsync(sp => sp.GetRequiredService<OrgDirectory>().GetDepartmentsAsync());
        }
        var prefixes = new[] { "B", "D", "E", "G", "K", "MT" };
        for (var i = 0; i < Math.Min(existing.Count, prefixes.Length); i++)
        {
            var department = existing[i];
            await RunAsync(sp => sp.GetRequiredService<RequestSetupRegistry>().SetPrefixAsync(department.Id, prefixes[i], null));
            departments.Add((department.Id, prefixes[i]));
        }
        if (departments.Count == 0) throw new InvalidOperationException("ไม่มีแผนกสำหรับใบขอซื้อ");
    }

    private async Task SeedSuppliersAsync()
    {
        string[] products = ["ลวดเหล็ก", "กล่องกระดาษ", "ผงเคมี", "บริการขนส่ง", "อะไหล่เครื่องจักร", "ถุงพลาสติก", "น้ำมันหล่อลื่น"];
        int[] credits = [0, 30, 30, 45, 60];
        for (var i = 1; i <= 40; i++)
        {
            var n = i;
            var result = await RunAsync(sp => sp.GetRequiredService<SupplierRegistry>().SaveAsync(new SupplierInput(null, $"FS{n:000}", $"ผู้ขายทดสอบ {n:000}", null,
                Address1: $"{n} หมู่ {1 + n % 9} ถนนทดสอบ", Address2: "ตำบลทดสอบ อำเภอทดสอบ จังหวัดทดสอบ 10000", Tel1: $"02-000-{n:0000}", Fax: $"02-001-{n:0000}", Contact: $"คุณทดสอบ {n}",
                CreditDays: credits[n % credits.Length], Vat: n % 3 != 0, ProductType: products[n % products.Length], BusinessType: "ผลิต (manufacturing)", Certificate: n % 4 == 0, UsedInProduction: n % 2 == 0)));
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors));
            supplierIds.Add(result.Id!.Value);
        }
    }

    private async Task SeedMaterialsAsync()
    {
        var codes = new List<(string Code, bool Main)>();
        void Add(string prefix, int count, bool main, int width = 4) { for (var i = 1; i <= count; i++) codes.Add(($"{prefix}{i.ToString().PadLeft(width, '0')}", main)); }
        Add("R", 60, true); Add("F", 30, true); Add("DC", 20, true, 3);
        Add("B", 30, false); Add("E", 10, false); codes.Add(("E0054", false)); Add("T", 10, false); Add("MM", 20, false);
        Add("BA", 3, false, 3); Add("BR", 4, false, 3); Add("CH", 3, false, 3); Add("SL", 3, false, 3); Add("SP", 6, true, 3);
        string[] units = ["กก.", "ชิ้น", "ใบ", "ลิตร", "ม้วน"];
        foreach (var (code, main) in codes)
        {
            var cost = Math.Round((decimal)(random.NextDouble() * 495 + 5), 2);
            var result = await RunAsync(sp => sp.GetRequiredService<MaterialRegistry>().SaveAsync(new MaterialInput(null, code, $"วัตถุดิบทดสอบ {code}", null, units[random.Next(units.Length)], 10m, cost, main)));
            if (!result.Succeeded) throw new InvalidOperationException($"{code}: " + string.Join(" ", result.Errors));
            materials.Add((result.Id!.Value, code, main, cost));
        }
    }

    private enum Scenario { ReceivedFully, PartlyOrdered, Outstanding, PartlyReceived, Failed, VatNo, Exactly5000, ManyLines, Springs, NotMain, Resale }

    /// <summary>Distinct materials for the lines (an order of 12 lines really has 12).</summary>
    private List<(int Id, string Code, bool Main, decimal Cost)> PickMaterials(Scenario scenario, int lines)
        => PoolOf(scenario).OrderBy(_ => random.Next()).Take(lines).ToList();

    private IEnumerable<(int Id, string Code, bool Main, decimal Cost)> PoolOf(Scenario scenario) => scenario switch
    {
        Scenario.Springs => materials.Where(m => m.Code.StartsWith("SP")),
        Scenario.NotMain => materials.Where(m => m.Code == "E0054"),
        Scenario.Resale => materials.Where(m => m.Code.StartsWith("BA") || m.Code.StartsWith("BR") || m.Code.StartsWith("CH") || m.Code.StartsWith("SL")),
        _ => materials.Where(m => m.Main && !m.Code.StartsWith("SP")),
    };

    /// <summary>A request, its order and (by the scenario) the deliveries of it.</summary>
    private async Task BuyAsync(DateOnly date, Scenario scenario, int lines)
    {
        var (departmentId, prefix) = departments[random.Next(departments.Count)];
        var counter = requestCounters[prefix] = requestCounters.GetValueOrDefault(prefix) + 1;
        var requestNo = $"{prefix}69-{counter:0000}";
        var picked = PickMaterials(scenario, lines);
        var quantities = picked.Select(_ => (decimal)random.Next(5, 200)).ToList();

        var request = await RunAsync(sp => sp.GetRequiredService<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput(requestNo, date, departmentId, date.AddDays(10), "F", "ข้อมูลปลอม", null,
            picked.Select((m, i) => new PurchaseRequestLineInput(m.Id, quantities[i])).ToList())));
        if (!request.Succeeded) throw new InvalidOperationException($"{requestNo}: " + string.Join(" ", request.Errors));
        requests++;
        var requestLines = (await RunAsync(sp => sp.GetRequiredService<PurchaseRequestDirectory>().GetAsync(request.Id!.Value)))!.Lines;

        var supplierIndex = random.Next(supplierIds.Count);
        var supplierId = supplierIds[supplierIndex];
        var supplier = (await RunAsync(sp => sp.GetRequiredService<SupplierDirectory>().SearchAsync($"FS{supplierIndex + 1:000}", 1))).Single();
        var chargesVat = scenario != Scenario.VatNo && supplier.Vat;
        var orderedQuantities = picked.Select((_, i) => scenario == Scenario.PartlyOrdered ? Math.Max(1m, Math.Floor(quantities[i] * 0.6m)) : quantities[i]).ToList();
        // the order of exactly 5,000.00 (VAT NO): one line, one piece, the price 5,000
        if (scenario == Scenario.Exactly5000) { picked = [picked[0]]; orderedQuantities = [1m]; requestLines = [requestLines[0]]; }
        decimal? Price(int i) => scenario == Scenario.Exactly5000 ? 5000m : null;

        var orderNo = await RunAsync(sp => sp.GetRequiredService<PurchaseOrderDirectory>().SuggestNumberAsync(date));
        var order = await RunAsync(sp => sp.GetRequiredService<PurchaseOrderRegistry>().IssueAsync(new PurchaseOrderInput(orderNo, date, supplierId, supplier.Name, supplier.Address1, supplier.Address2, supplier.Tel1,
            supplier.Fax, supplier.CreditDays, scenario == Scenario.Exactly5000 ? false : chargesVat, "F", "ข้อมูลปลอม", null,
            requestLines.Select((l, i) => new PurchaseOrderLineInput(l.Id, orderedQuantities[i], Price(i))).ToList())));
        if (!order.Succeeded) throw new InvalidOperationException($"{orderNo}: " + string.Join(" ", order.Errors));
        orders++;
        if (scenario == Scenario.Outstanding) return; // ordered, nothing delivered yet (ค้างรับ)

        var orderLines = (await RunAsync(sp => sp.GetRequiredService<PurchaseOrderDirectory>().GetAsync(order.Id!.Value)))!.Lines;
        var received = orderLines.Select((l, i) => scenario == Scenario.PartlyReceived ? Math.Max(1m, Math.Floor(l.Quantity * 0.5m)) : l.Quantity).ToList();
        var failed = scenario == Scenario.Failed;
        var receiveDate = date.AddDays(random.Next(2, 12));
        var inputs = orderLines.Select((l, i) => new GoodsReceiptInput(l.Id, receiveDate, received[i], $"INV-{orders:0000}", (i + 1).ToString(), Failed: failed)).ToList();
        var done = await RunAsync(sp => sp.GetRequiredService<GoodsReceiptRegistry>().ReceiveAsync(inputs));
        if (!done.Succeeded) throw new InvalidOperationException($"{orderNo}: " + string.Join(" ", done.Errors));
        receipts += inputs.Count;

        // QA has looked at them: a LOT, the result and whether the certificate came
        var made = (await RunAsync(sp => sp.GetRequiredService<GoodsReceiptDirectory>().GetByDateAsync(receiveDate))).Where(r => orderLines.Any(l => l.Id == r.OrderLineId)).ToList();
        foreach (var receipt in made)
            await RunAsync(sp => sp.GetRequiredService<GoodsReceiptRegistry>().SaveQaAsync(new GoodsReceiptQaUpdate(receipt.Id, receipt.RowVersion, $"LOT-{++lotCounter:0000}", failed, !failed && random.Next(3) > 0, failed ? "ไม่ผ่าน (ข้อมูลปลอม)" : null)));
    }

    private async Task IssueStockAsync(DateOnly from, int documents)
    {
        var mains = materials.Where(m => m.Main && !m.Code.StartsWith("SP")).ToList();
        for (var i = 1; i <= documents; i++)
        {
            var picked = Enumerable.Range(0, random.Next(1, 4)).Select(_ => mains[random.Next(mains.Count)]).DistinctBy(m => m.Id).ToList();
            var result = await RunAsync(sp => sp.GetRequiredService<StockIssueRegistry>().SaveAsync(new StockIssueInput($"{from:MMdd}/{i:000}", from.AddDays(i % 20), null,
                picked.Select((m, n) => new StockIssueLineInput(m.Id, random.Next(1, 40), (n + 1).ToString())).ToList())));
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors));
            issues++;
        }
    }
}

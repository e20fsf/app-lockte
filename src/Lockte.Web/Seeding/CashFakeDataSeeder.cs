using Lockte.Cash;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Seeding;

/// <summary>What <see cref="CashFakeDataSeeder"/> made.</summary>
public sealed record CashFakeSummary(int ExpenseTypes, int ExpenseLines, int Buyers, int ScrapItems, int Receipts, int Remitted);

/// <summary>
/// ชุดสาธิตตัวเลขของโปรแกรมเงินสดย่อย (#247): a small set that is entirely invented — no name, address, phone, plate or amount of the real data is read or written, and the same call always
/// gives the same set. It goes through the module's own services (every rule applies and the rows are audited) and is set to give the figures of <c>acceptance.md</c>:
/// AC-06 (the voucher 09/30 of 9 Sep 2026: 30 + 50 + 60 = 140.00), AC-07 (1–3 Sep 2026 over nine types: 28,710.00; with 21,456 from the last period 50,166.00), AC-20 (11 Sep: 633 + 90 + 80 = 803.00),
/// AC-10 (receipt 69029: 2,270.00), AC-13 and AC-15 (69028 + 69029 = 3,270.00, waiting to be closed) and BR-018 #2 (receipt 69027 of 5 Aug: 1,360.00). The receipts 69025 and 69026 are already
/// remitted, so ยอดรอนำส่ง starts with 69027–69029. It is the fixture of the tests and the way to fill a test server (<c>Lockte.Web seed-cash-fake</c>, see docs/migration/cash-fake-data.md).
/// </summary>
public sealed class CashFakeDataSeeder(Func<AsyncServiceScope> newScope)
{
    public const string MarkerReceiptNo = "69029";

    public static readonly DateOnly Sep1 = new(2026, 9, 1);

    /// <summary>The nine types of the AC-07 window, with what each adds up to there (28,710.00 together).</summary>
    public static readonly (string Name, decimal Total)[] Ac07Types =
    [
        ("ค่าขนส่ง", 70m), ("ค่าใช้จ่ายเบ็ดเตล็ด - โรงงาน", 6132m), ("ค่าใช้จ่ายแผนกซ่อมบำรุง", 750m), ("ค่าใช้จ่ายแผนกไดคาสท์", 3000m), ("ค่าใช้จ่ายแผนกอัดผ้าเบรก", 590m),
        ("ค่าใช้จ่ายยานพาหนะ (กข 0001)", 348m), ("ค่าใช้จ่ายยานพาหนะ (กข 0002)", 2200m), ("ค่าใช้จ่ายยานพาหนะ (รถยก)", 1000m), ("ค่าสวัสดิการพนักงานโรงงาน", 14620m),
    ];

    public const string RetiredType = "ค่าใช้จ่ายเก่า (เลิกใช้)";

    private async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = newScope();
        return await action(scope.ServiceProvider);
    }

    public async Task<CashFakeSummary> SeedAsync()
    {
        if (await RunAsync(async sp => await sp.GetRequiredService<ScrapReceiptDirectory>().GetByNoAsync(MarkerReceiptNo) is not null))
            throw new InvalidOperationException("ชุดข้อมูลปลอมถูกเติมในฐานนี้แล้ว — ลบฐานแล้วสร้างใหม่ก่อน (Reset-CashFake.ps1)");

        var types = new Dictionary<string, int>();
        foreach (var (name, _) in Ac07Types) types[name] = await RunAsync(async sp => Id(await sp.GetRequiredService<ExpenseTypeRegistry>().SaveAsync(new(null, name, null))));
        types[RetiredType] = await RunAsync(async sp => Id(await sp.GetRequiredService<ExpenseTypeRegistry>().SaveAsync(new(null, RetiredType, null))));

        // AC-07: 1–3 Sep 2026, nine types, every line keyed on its day with its own voucher number
        var voucher = 0;
        var lines = 0;
        async Task Line(DateOnly day, string number, string name, decimal qty, string? unit, decimal price, decimal amount, string type)
        {
            var result = await RunAsync(sp => sp.GetRequiredService<ExpenseLineRegistry>().SaveAsync(new(null, day, number, name, qty, unit, price, amount, types[type], null)));
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors));
            lines++;
        }
        string Next() => $"09/{(++voucher).ToString("00", System.Globalization.CultureInfo.InvariantCulture)}";
        foreach (var (type, total) in Ac07Types)
        {
            var first = total > 5000m ? Math.Round(total / 3m, 0) : total;
            await Line(Sep1.AddDays(voucher % 3), Next(), "รายการตัวอย่าง 1", 1, "ชิ้น", 0, first, type);
            if (first != total) await Line(Sep1.AddDays(voucher % 3), Next(), "รายการตัวอย่าง 2", 1, "ชิ้น", 0, total - first, type);
        }

        // AC-06: the voucher 09/30 of 9 Sep; AC-20: 11 Sep, three lines
        var sep9 = new DateOnly(2026, 9, 9);
        var repair = "ค่าใช้จ่ายแผนกซ่อมบำรุง";
        await Line(sep9, "09/30", "โอริง 3.5X27", 2, "ตัว", 15m, 30m, repair);
        await Line(sep9, "09/30", "โอริง 3X56", 2, "ตัว", 25m, 50m, repair);
        await Line(sep9, "09/30", "โอริง 3.5X62", 2, "ตัว", 30m, 60m, repair);
        var sep11 = new DateOnly(2026, 9, 11);
        await Line(sep11, "09/34", "แก๊ส NGV", 31.53m, "กก.", 0m, 633m, "ค่าขนส่ง");
        await Line(sep11, "09/35", "ซีล TC 25-40-8", 1, "ตัว", 90m, 90m, repair);
        await Line(sep11, "09/36", "หัวสปริงเกอร์ดับเพลิง", 2, "หัว", 40m, 80m, repair);
        await Line(new DateOnly(2026, 8, 20), "08/15", "ของใช้แบบเก่า", 1, "ชิ้น", 0, 100m, RetiredType); // a line that keeps a retired type in use

        // the registers of the receipts
        var buyers = new[] { ("คุณตัวอย่าง ก", "99/1 ซอยตัวอย่าง แขวงตัวอย่าง", "000-000-0001"), ("คุณตัวอย่าง ข", "99/2 ซอยตัวอย่าง แขวงตัวอย่าง", "000-000-0002"), ("คุณตัวอย่าง ค", null, "000-000-0003") };
        foreach (var (name, address, tel) in buyers) await RunAsync(async sp => Id(await sp.GetRequiredService<ScrapBuyerRegistry>().SaveAsync(new(null, name, address, tel, null))));
        var scrap = new[] { ("เศษเหล็ก", "กก."), ("ถังพลาสติก", "ใบ"), ("น้ำมันเก่า", "ถัง"), ("ฝุ่น", "กก."), ("เบ้าเหล็ก", "กก."), ("พลาสติกสกินแพ็ค", "กก."), ("ถังเหล็ก", "ใบ") };
        foreach (var (name, unit) in scrap) await RunAsync(async sp => Id(await sp.GetRequiredService<ScrapItemRegistry>().SaveAsync(new(null, name, unit, null))));

        async Task Receipt(string no, DateOnly date, string buyer, params (string Item, decimal Qty, decimal Price)[] items)
        {
            var result = await RunAsync(sp => sp.GetRequiredService<ScrapReceiptRegistry>().SaveAsync(new(null, no, date, buyer, null, null, null,
                items.Select((l, i) => new ScrapReceiptLineInput(i + 1, l.Item, null, scrap.First(s => s.Item1 == l.Item).Item2, l.Qty, l.Price, null)).ToList(), null, date)));
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors));
        }
        // two receipts already sent to the office, then three waiting (AC-13 closes 69028–69029)
        await Receipt("69025", new DateOnly(2026, 7, 20), "คุณตัวอย่าง ก", ("เศษเหล็ก", 250, 2));
        await Receipt("69026", new DateOnly(2026, 7, 28), "คุณตัวอย่าง ค", ("ฝุ่น", 350, 2));
        var closed = await RunAsync(sp => sp.GetRequiredService<ScrapRemittanceRegistry>().CloseAsync(new("69025", "69026", new DateOnly(2026, 7, 31))));
        if (!closed.Succeeded) throw new InvalidOperationException(string.Join(" ", closed.Errors));
        await Receipt("69027", new DateOnly(2026, 8, 5), "คุณตัวอย่าง ก", ("เศษเหล็ก", 100, 2), ("ถังพลาสติก", 2, 60), ("น้ำมันเก่า", 0.8m, 800), ("ถังเหล็ก", 5, 80)); // 1,360.00 (BR-018 #2)
        await Receipt("69028", new DateOnly(2026, 9, 5), "คุณตัวอย่าง ข", ("ฝุ่น", 500, 2)); // 1,000.00
        await Receipt("69029", new DateOnly(2026, 9, 8), "คุณตัวอย่าง ข", ("เบ้าเหล็ก", 220, 1), ("ถังพลาสติก", 1, 120), ("น้ำมันเก่า", 1.5m, 800), ("พลาสติกสกินแพ็ค", 110, 3), ("ถังเหล็ก", 5, 80)); // 2,270.00 (AC-10)

        return new CashFakeSummary(types.Count, lines, buyers.Length, scrap.Length, 5, 2);
    }

    private static int Id(Lockte.SharedKernel.ServiceResult result)
        => result.Succeeded ? result.Id!.Value : throw new InvalidOperationException(string.Join(" ", result.Errors));
}

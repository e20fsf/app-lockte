using Lockte.Purch;
using Lockte.SharedKernel;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>UAT วัตถุดิบ ครั้งที่ 2 (#448): names A–Z then ก–ฮ, Enter to the next field, the order prints from the order page, F in the box, the bigger number.</summary>
public sealed class PurchUat3Tests
{
    private static readonly PurchLetterheadOptions Letterhead = new() { Line1 = "ที่อยู่ทดสอบ", Line2 = "Test address" };

    [Fact]
    public void Names_run_A_to_Z_ignoring_case_and_then_Thai_in_dictionary_order()
    {
        var names = new[] { "ไทยเจริญ", "zeta co", "กขค", "Alpha", "beta", "เอกชัย", "บริษัท ก", "Zeta Co", "ขนส่ง", "a-1" };
        var sorted = NameOrder.ByName(names, n => n, n => n).ToList();
        var firstThai = sorted.FindIndex(n => n[0] >= 'ก');
        Assert.All(sorted.Take(firstThai), n => Assert.True(n[0] < 'ก')); // every Latin name before every Thai one
        Assert.Equal(["a-1", "Alpha", "beta"], sorted.Take(3));
        Assert.Equal(["Zeta Co", "zeta co"], sorted.Skip(3).Take(2).OrderBy(n => n, StringComparer.Ordinal)); // case does not split them
        Assert.Equal(["กขค", "ขนส่ง", "บริษัท ก"], sorted.Skip(firstThai).Where(n => n is "กขค" or "ขนส่ง" or "บริษัท ก"));
        Assert.True(sorted.IndexOf("เอกชัย") < sorted.IndexOf("ไทยเจริญ") || sorted.IndexOf("เอกชัย") > sorted.IndexOf("กขค")); // a leading vowel does not put it last of all
    }

    [Fact]
    public void The_printed_name_lists_use_the_same_order()
    {
        var all = new[] { "ก", "b", "A" }.Select((n, i) => new SupplierDto(i, "C" + i, n, null, null, null, null, null, null, null, true, null, null, false, true, [])).ToList();
        Assert.Equal(["A", "b", "ก"], ListReports.Suppliers(all, byName: true).Rows.Select(s => s.Name));
    }

    private static PurchaseOrderPrint Order(string? location)
        => new("691125", new DateOnly(2026, 11, 11), "ผู้ขายทดสอบ", "1 ถนนทดสอบ", "เขตทดสอบ", "02-000-0000", "02-000-0001", "ไดคาสท์", new DateOnly(2026, 11, 13), 30,
            [new(1, "D69-0093", "รายการทดสอบ", 2m, "ถัง", 8400m, 16800m)], 16800m, 1176m, 17976m, "หนึ่งหมื่นเจ็ดพันเก้าร้อยเจ็ดสิบหกบาทถ้วน", null, null, location);

    [Fact]
    public void The_box_beside_the_number_holds_the_purchasing_location_and_the_number_is_larger()
    {
        var withF = string.Concat(PurchaseOrderForm.Layout(Order("F"), "PC-PU002-2/2547 ฉบับที่ 1", Letterhead).Select(p => p.ToSvg()));
        var without = string.Concat(PurchaseOrderForm.Layout(Order(null), "PC-PU002-2/2547 ฉบับที่ 1", Letterhead).Select(p => p.ToSvg()));
        Assert.Contains(">F<", withF);
        Assert.DoesNotContain(">F<", without);
        Assert.Contains(">691125<", withF);
        Assert.Contains("font-size=\"7.02\"", withF[(withF.IndexOf(">691125<", StringComparison.Ordinal) - 200)..withF.IndexOf(">691125<", StringComparison.Ordinal)]);
        Assert.DoesNotContain(">F<", string.Concat(PurchaseOrderForm.Blank("x", Letterhead).ToSvg())); // the empty form stays empty
    }

    [Fact]
    public void The_order_page_has_a_save_and_print_button_that_opens_the_pdf_and_Enter_moves_between_fields()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && Directory.GetFiles(root, "*.sln*").Length == 0) root = Path.GetDirectoryName(root);
        var web = Path.Combine(root!, "src", "Lockte.Web");
        var page = File.ReadAllText(Path.Combine(web, "Components", "Pages", "Purch", "OrderNew.razor"));
        Assert.Contains("บันทึกและพิมพ์ใบสั่งซื้อ", page);
        Assert.Contains("reports/purch/order/{orderId}", page);
        foreach (var form in new[] { "OrderNew", "Suppliers", "Materials", "RequestEntry" })
            Assert.Contains("data-enter-next", File.ReadAllText(Path.Combine(web, "Components", "Pages", "Purch", form + ".razor")));
        Assert.Contains("js/enter-next.js", File.ReadAllText(Path.Combine(web, "Components", "App.razor")));
        Assert.Contains("'Enter'", File.ReadAllText(Path.Combine(web, "wwwroot", "js", "enter-next.js")));
    }
}

/// <summary>UAT วัตถุดิบ ครั้งที่ 2 (#448) on the database: the pickers list by name.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class PurchUat3DbTests(LocalDbFixture db)
{
    [Fact]
    public async Task The_supplier_and_material_pickers_list_by_name_A_to_Z_then_Thai()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        await using (var scope = db.Scope("buyer1"))
        {
            foreach (var name in new[] { $"ก {tag}", $"b {tag}", $"A {tag}", $"ข {tag}" })
            {
                Assert.True((await scope.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, "s" + name[..1] + tag, name, null))).Succeeded);
                Assert.True((await scope.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, "m" + name[..1] + tag, name, null))).Succeeded);
            }
        }
        await using var read = db.Scope("buyer1");
        var expected = new[] { $"A {tag}", $"b {tag}", $"ก {tag}", $"ข {tag}" };
        Assert.Equal(expected, (await read.Get<SupplierDirectory>().SearchAsync(tag)).Select(s => s.Name));
        Assert.Equal(expected, (await read.Get<MaterialDirectory>().SearchAsync(tag)).Select(m => m.Name));
    }
}

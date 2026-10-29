using Lockte.Web.Menus;

namespace Lockte.Web.Tests;

/// <summary>The menu trees are data copied from the legacy Switchboards; these check they hang together.</summary>
public sealed class ProgramMenuTests
{
    public static TheoryData<string> Programs => new(ProgramMenus.All.Select(p => p.Key));

    private static ProgramMenu Program(string key) => ProgramMenus.Find(key)!;

    [Theory]
    [MemberData(nameof(Programs))]
    public void Every_sub_menu_and_back_target_exists_and_every_menu_page_is_reachable(string key)
    {
        var menu = Program(key);
        var opened = menu.Pages.SelectMany(p => p.Items).Where(i => i.SubMenu is not null).Select(i => i.SubMenu!.Value).ToHashSet();

        Assert.All(opened, n => Assert.NotNull(menu.Find(n)));
        Assert.All(menu.Pages.Where(p => p.Back is not null), p => Assert.NotNull(menu.Find(p.Back!.Value)));
        Assert.All(menu.Pages.Where(p => p.Number != ProgramMenu.Main), p => Assert.Contains(p.Number, opened));
        Assert.Null(menu.Find(ProgramMenu.Main)!.Back);
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public void Placeholder_codes_are_unique_within_a_program(string key)
    {
        var codes = Program(key).Pages.SelectMany(p => p.Items).Where(i => i.Code is not null).Select(i => i.Code!.ToUpperInvariant()).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void Order_main_menu_matches_the_legacy_switchboard_without_the_left_out_items()
    {
        var main = OrderMenu.Menu.Find(ProgramMenu.Main)!;
        Assert.Equal(
            [("ข้อมูลลูกค้า", 7), ("ข้อมูลสินค้า", 8), ("ORDER ลูกค้าและเขตการขาย", 2), ("ผลผลิต", 3),
             ("แม่พิมพ์และรหัสเครื่องจักร", 6), ("QA พิมพ์รายงานการตรวจสอบ", 15)],
            main.Items.Select(i => (i.Text, i.SubMenu!.Value)));

        var all = OrderMenu.Menu.Pages.SelectMany(p => p.Items).Select(i => i.Text).ToList();
        Assert.DoesNotContain(all, t => t.Contains("ลบข้อมูลปีที่แล้ว") || t.Contains("ย้ายข้อมูล") || t.Contains("NCR") || t.Contains("จบโปรแกรม"));
        Assert.Equal(17, OrderMenu.Menu.Pages.Count);
    }

    [Fact]
    public void Order_labels_use_the_context_terms_not_the_legacy_ones()
    {
        var labels = OrderMenu.Menu.Pages.SelectMany(p => p.Items.Select(i => i.Text).Append(p.Title)).ToList();
        Assert.DoesNotContain(labels, t => t.Contains("เซล") || t.Contains("ส่งออก") || t.Contains("STOCK") || t.Contains("SALE ORDER"));
    }

    [Fact]
    public void An_item_not_built_yet_opens_the_placeholder_of_its_own_program()
    {
        Assert.Equal("order/qa/production/D", OrderMenu.Menu.HrefOf(OrderMenu.Menu.FindItem("SCR-12-1")!.Value.Item)); // built (#130): every item of the program has its page now
        Assert.Equal("hr/SCR-41", EmployeeMenu.Menu.HrefOf(EmployeeMenu.Menu.FindItem("SCR-41")!.Value.Item));
        Assert.Equal("order/menu/17", OrderMenu.HrefOf(17));
        Assert.Equal("order", OrderMenu.HrefOf(ProgramMenu.Main));
    }

    [Fact]
    public void Purch_main_menu_matches_the_legacy_switchboard_without_the_left_out_items()
    {
        var main = PurchMenu.Menu.Find(ProgramMenu.Main)!;
        Assert.Equal(
            [("ข้อมูลผู้ขาย", 9), ("ข้อมูลวัตถุดิบ", 10), ("พิมพ์ใบขออนุมัติสั่งซื้อและใบสั่งซื้อ", 3), ("รับวัตถุดิบและรายงาน", 8), ("สต็อกวัตถุดิบ", 11)],
            main.Items.Where(i => i.SubMenu is not null).Select(i => (i.Text, i.SubMenu!.Value)));
        Assert.Equal(9, PurchMenu.Menu.Pages.Count);

        var all = PurchMenu.Menu.Pages.SelectMany(p => p.Items).Select(i => i.Text).ToList();
        Assert.DoesNotContain(all, t => t.Contains("ลบข้อมูลปีที่แล้ว") || t.Contains("จบโปรแกรม"));
        Assert.DoesNotContain(PurchMenu.Menu.Pages.SelectMany(p => p.Items.Select(i => i.Text).Append(p.Title)), t => t.Contains("ค้างส่ง") || t.Contains("STOCK"));
    }

    [Fact]
    public void Purch_menus_return_to_the_legacy_parents_and_open_their_own_placeholders()
    {
        Assert.Equal(8, PurchMenu.Menu.Find(5)!.Back); // รับวัตถุดิบ → รับวัตถุดิบและรายงาน
        Assert.Equal(2, PurchMenu.Menu.Find(7)!.Back); // บันทึก/ประเมิน → รายงาน
        Assert.Equal("purch/SCR-07", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("SCR-07")!.Value.Item));
        Assert.Equal("purch/materials", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("SCR-17-2")!.Value.Item)); // built (#215)
        Assert.Equal("purch/suppliers", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("SCR-02-2")!.Value.Item)); // built (#214)
        Assert.Equal("purch/menu/9", PurchMenu.HrefOf(9));
        Assert.Same(PurchMenu.Menu, ProgramMenus.Of("purch/menu/5"));
        Assert.Same(PurchMenu.Menu, ProgramMenus.Of("purch/settings"));
    }
}

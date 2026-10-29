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
        Assert.Equal("purch/RPT-10", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("RPT-10")!.Value.Item));
        Assert.Equal("purch/reports/receipts/supplier", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("RPT-18")!.Value.Item)); // built (#258)
        Assert.Equal("purch/reports/incoming/inspection", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("RPT-06")!.Value.Item)); // built (#257)
        Assert.Equal("reports/purch/backlog/orders", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("RPT-04")!.Value.Item)); // built (#256)
        Assert.Equal("purch/reports/evaluation", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("RPT-07")!.Value.Item)); // built (#254)
        Assert.Equal("purch/reports/evaluation-annual", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("RPT-08")!.Value.Item)); // built (#255)
        Assert.Equal("purch/reports/evaluation-summary", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("RPT-09")!.Value.Item));
        Assert.Equal("purch/stock/close", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("SCR-18")!.Value.Item)); // built (#252)
        Assert.Equal("purch/receipts/qa", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("SCR-13")!.Value.Item)); // built (#251)
        Assert.Equal("purch/receipts", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("SCR-12")!.Value.Item)); // built (#250)
        Assert.Equal("purch/receipts/new", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("SCR-11")!.Value.Item)); // built (#249)
        Assert.Equal("purch/stock/issue", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("SCR-14")!.Value.Item)); // built (#226)
        Assert.Equal("purch/materials", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("SCR-17-2")!.Value.Item)); // built (#215)
        Assert.Equal("purch/suppliers", PurchMenu.Menu.HrefOf(PurchMenu.Menu.FindItem("SCR-02-2")!.Value.Item)); // built (#214)
        Assert.Equal("purch/menu/9", PurchMenu.HrefOf(9));
        Assert.Same(PurchMenu.Menu, ProgramMenus.Of("purch/menu/5"));
        Assert.Same(PurchMenu.Menu, ProgramMenus.Of("purch/settings"));
    }

    [Fact]
    public void Calib_menus_are_the_eight_legacy_switchboard_pages_without_จบโปรแกรม()
    {
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], CalibMenu.Menu.Pages.Select(p => p.Number));
        Assert.Equal(
            [("แผนการสอบเทียบ", null), ("สอบเทียบภายนอก", 2), ("สอบเทียบภายใน", 3)],
            CalibMenu.Menu.Find(ProgramMenu.Main)!.Items.Select(i => (i.Text, i.SubMenu)));
        Assert.Equal(
            ["เวอร์เนีย, ไฮเกจ, ไมโครมิเตอร์", "เพรสเซอร์เกจ", "เครื่องชั่ง", "JIG", "ตัวควบคุมอุณหภูมิ (TEMP CONTROL)"],
            CalibMenu.Menu.Find(CalibMenu.Internal)!.Items.Select(i => i.Text));

        var texts = CalibMenu.Menu.Pages.SelectMany(p => p.Items.Select(i => i.Text).Append(p.Title)).ToList();
        Assert.DoesNotContain(texts, t => t.Contains("จบโปรแกรม"));
        Assert.DoesNotContain(texts, t => t.Contains("ภาน")); // the legacy's misspelt สอบเทียบภานใน / ภานนอก
        Assert.DoesNotContain(texts, t => t.Contains("หน่วยงานสอบเทียบ")); // CONTEXT.md: บริษัทที่สอบเทียบ
        Assert.Contains("เพิ่มรายชื่อบริษัทที่สอบเทียบ", texts);
    }

    [Fact]
    public void Calib_menus_return_to_the_legacy_parents_and_open_their_own_placeholders()
    {
        Assert.Equal(1, CalibMenu.Menu.Find(CalibMenu.External)!.Back);
        Assert.Equal(1, CalibMenu.Menu.Find(CalibMenu.Internal)!.Back);
        Assert.All([CalibMenu.Caliper, CalibMenu.PressureGauge, CalibMenu.Scale, CalibMenu.Jig, CalibMenu.TempControl],
            n => Assert.Equal(CalibMenu.Internal, CalibMenu.Menu.Find(n)!.Back));

        Assert.Equal("calib/RPT-01", CalibMenu.Menu.HrefOf(CalibMenu.Menu.FindItem("RPT-01")!.Value.Item));
        Assert.Equal("calib/SCR-05", CalibMenu.Menu.HrefOf(CalibMenu.Menu.FindItem("SCR-05")!.Value.Item));
        Assert.Equal("calib/menu/7", CalibMenu.HrefOf(CalibMenu.Jig));
        Assert.Equal("calib", CalibMenu.HrefOf(ProgramMenu.Main));
        Assert.Same(CalibMenu.Menu, ProgramMenus.Of("calib/menu/4"));
        Assert.Same(CalibMenu.Menu, ProgramMenus.Of("calib/SCR-06"));
        Assert.Contains(CalibMenu.Menu, ProgramMenus.All);
    }

    [Fact]
    public void Each_instrument_menu_has_the_same_five_kinds_of_item()
    {
        // add, edit, list (RPT-02), the record form (RPT-03; JIG opens SCR-05 instead) and the sticker (SCR-04, internal types only)
        foreach (var (page, suffix) in new[] { (CalibMenu.Caliper, "caliper"), (CalibMenu.PressureGauge, "pg"), (CalibMenu.Scale, "scale"), (CalibMenu.TempControl, "tc") })
            Assert.Equal([$"SCR-02-{suffix}", $"SCR-03-{suffix}", $"RPT-02-{suffix}", $"RPT-03-{suffix}", $"SCR-04-{suffix}"],
                CalibMenu.Menu.Find(page)!.Items.Select(i => i.Code));
        Assert.Equal(["SCR-02-jig", "SCR-03-jig", "RPT-02-jig", "SCR-05", "SCR-04-jig"], CalibMenu.Menu.Find(CalibMenu.Jig)!.Items.Select(i => i.Code));
        Assert.Equal(["SCR-02-ext", "SCR-03-ext", "RPT-02-ext", "RPT-03-ext", "SCR-06"], CalibMenu.Menu.Find(CalibMenu.External)!.Items.Select(i => i.Code));
    }

    [Fact]
    public void Built_instrument_screens_are_opened_by_their_menu_items()
    {
        Assert.Equal("calib/instruments/caliper/new", CalibMenu.Menu.HrefOf(CalibMenu.Menu.FindItem("SCR-02-caliper")!.Value.Item)); // built (#194)
        Assert.Equal("calib/instruments/caliper", CalibMenu.Menu.HrefOf(CalibMenu.Menu.FindItem("SCR-03-caliper")!.Value.Item));
        Assert.Same(CalibMenu.Menu, ProgramMenus.Of("calib/instruments/caliper/new"));
    }

    [Fact]
    public void Every_instrument_kind_has_its_screen_info_and_returns_to_its_own_menu()
    {
        Assert.Equal(Enum.GetValues<Lockte.Calib.InstrumentKind>().Order(), Lockte.Web.Components.Pages.Calib.InstrumentKindInfo.All.Select(k => k.Kind).Order());
        Assert.All(Lockte.Web.Components.Pages.Calib.InstrumentKindInfo.All, k => Assert.NotNull(CalibMenu.Menu.Find(k.MenuNumber)));
        Assert.Equal(Lockte.Web.Components.Pages.Calib.InstrumentKindInfo.All.Count, Lockte.Web.Components.Pages.Calib.InstrumentKindInfo.All.Select(k => k.Key).Distinct().Count());
    }

    [Theory]
    [InlineData("pg")]
    [InlineData("scale")]
    [InlineData("tc")]
    public void Pressure_gauge_scale_and_temp_control_screens_are_opened_by_their_menu_items(string key) // built (#195)
    {
        Assert.Equal($"calib/instruments/{key}/new", CalibMenu.Menu.HrefOf(CalibMenu.Menu.FindItem($"SCR-02-{key}")!.Value.Item));
        Assert.Equal($"calib/instruments/{key}", CalibMenu.Menu.HrefOf(CalibMenu.Menu.FindItem($"SCR-03-{key}")!.Value.Item));
    }

    [Fact]
    public void Jig_screens_are_opened_by_their_menu_items() // built (#196)
    {
        Assert.Equal("calib/instruments/jig/new", CalibMenu.Menu.HrefOf(CalibMenu.Menu.FindItem("SCR-02-jig")!.Value.Item));
        Assert.Equal("calib/instruments/jig", CalibMenu.Menu.HrefOf(CalibMenu.Menu.FindItem("SCR-03-jig")!.Value.Item));
    }

    [Fact]
    public void The_calibration_company_screen_is_opened_from_the_external_menu() // built (#197)
        => Assert.Equal("calib/providers", CalibMenu.Menu.HrefOf(CalibMenu.Menu.FindItem("SCR-06")!.Value.Item));

    [Fact]
    public void External_instrument_screens_are_opened_by_their_menu_items() // built (#198)
    {
        Assert.Equal("calib/instruments/ext/new", CalibMenu.Menu.HrefOf(CalibMenu.Menu.FindItem("SCR-02-ext")!.Value.Item));
        Assert.Equal("calib/instruments/ext", CalibMenu.Menu.HrefOf(CalibMenu.Menu.FindItem("SCR-03-ext")!.Value.Item));
    }
}

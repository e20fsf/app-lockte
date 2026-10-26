using Lockte.Web.Menus;

namespace Lockte.Web.Tests;

public sealed class MenuTrailTests
{
    private static string[] Texts(string path) => EmployeeMenu.Trail(path).Select(c => c.Text).ToArray();

    [Fact]
    public void Main_menu_is_the_only_step_and_is_the_current_page()
    {
        var trail = EmployeeMenu.Trail("");
        Assert.Equal([new EmployeeMenu.Crumb("เมนูหลัก", null)], trail);
    }

    [Fact]
    public void A_screen_shows_the_menus_above_it_and_links_back_to_each()
    {
        var trail = EmployeeMenu.Trail("hr/time/entry");
        Assert.Equal(["เมนูหลัก", "วันอาทิตย์และวันหยุด", "เวลาทำงาน", "บันทึกเวลาทำงาน, OT, วันอาทิตย์และวันหยุด"], trail.Select(c => c.Text));
        Assert.Equal(["", "menu/7", "menu/3", null], trail.Select(c => c.Href));
    }

    [Fact]
    public void A_sub_menu_is_the_current_page_and_its_parents_link()
    {
        var trail = EmployeeMenu.Trail("menu/19");
        Assert.Equal(["เมนูหลัก", "ข้อมูลพนักงาน", "พิมพ์หนังสือรับรอง"], trail.Select(c => c.Text));
        Assert.Null(trail[^1].Href);
        Assert.Equal("menu/2", trail[1].Href);
    }

    [Fact]
    public void A_page_below_a_menu_item_belongs_to_that_item_and_the_exact_item_wins()
    {
        Assert.Equal("แก้ไขข้อมูลพนักงาน", Texts("hr/employees/12/kor2")[^1]);
        Assert.Equal("เพิ่มข้อมูลพนักงาน", Texts("hr/employees/new")[^1]);
    }

    [Fact]
    public void Query_string_is_ignored_and_a_page_on_no_menu_has_no_trail()
    {
        Assert.Equal("เลขเอกสารควบคุม", Texts("document-numbers?x=1")[^1]);
        Assert.Empty(EmployeeMenu.Trail("history"));
    }
}

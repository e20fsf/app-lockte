namespace Lockte.Web.Tests;

/// <summary>UAT7-5 (#466): ช่อง ได้ค่าแรง อยู่ติดเหตุผล (ลำดับเดียวกับฟอร์ม LEAVE1 เดิม: SAL แล้ว REMARK) และตัวเลือก Y/N อ่านได้บรรทัดเดียว.</summary>
public sealed class LeaveTabLayoutTests
{
    private static string Page()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && Directory.GetFiles(root, "*.sln*").Length == 0) root = Path.GetDirectoryName(root);
        return File.ReadAllText(Path.Combine(root!, "src", "Lockte.Web", "Components", "Pages", "Hr", "LeaveTab.razor"));
    }

    [Fact]
    public void Paid_sits_right_before_the_reason_in_the_header_and_in_the_edit_row()
    {
        var page = Page();
        Assert.Contains("<th>ได้ค่าแรง</th><th>เหตุผล</th>", page);
        var paid = page.IndexOf("Value=\"f.Paid\"", StringComparison.Ordinal);
        var remark = page.IndexOf("@bind-Value=\"f.Remark\"", StringComparison.Ordinal);
        var type = page.IndexOf("@bind-Value=\"f.Type\"", StringComparison.Ordinal);
        Assert.True(type < paid && paid < remark);
    }

    [Fact]
    public void Paid_choices_are_wide_enough_and_do_not_wrap()
    {
        var page = Page();
        Assert.Contains("min-width:8rem\" Class=\"leave-paid-select\"", page);
        Assert.Contains("<span style=\"white-space:nowrap\">N ไม่ได้</span>", page);
    }
}

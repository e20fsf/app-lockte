namespace Lockte.Web.Tests;

/// <summary>UAT7-8 (#473): ช่องกรอกเวลากด Tab ไปช่องถัดไปได้ (Shift+Tab ย้อนกลับ) ใช้คีย์บอร์ดล้วนได้.
/// ตรวจสัญญาแบบโครงสร้าง: ช่องเวลาทุกจุดใช้ TimeBox กลาง (MudAutocomplete) หรือ MudTextField ธรรมดา และไม่มีโค้ดใดในแอปที่ดักคีย์ Tab,
/// ตั้ง tabindex ลบ หรือ preventDefault บนช่องกรอก. (ลองกับ MudBlazor 9.11 จริงแล้ว: Tab/Shift+Tab ผ่าน TimeBox ได้ ทั้งตอนพิมพ์ค่าและตอนรายการเปิด.)</summary>
public sealed class TimeBoxKeyboardTests
{
    private static string WebRoot()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && Directory.GetFiles(root, "*.sln*").Length == 0) root = Path.GetDirectoryName(root);
        return Path.Combine(root!, "src", "Lockte.Web");
    }

    private static IEnumerable<(string Name, string Text)> Razor() =>
        Directory.EnumerateFiles(Path.Combine(WebRoot(), "Components"), "*.razor", SearchOption.AllDirectories)
            .Select(f => (Path.GetFileName(f), File.ReadAllText(f)));

    [Fact]
    public void TimeBox_is_a_plain_autocomplete_that_does_not_touch_Tab()
    {
        var box = Razor().Single(r => r.Name == "TimeBox.razor").Text;
        Assert.Contains("<MudAutocomplete", box);
        foreach (var banned in new[] { "TabIndex", "tabindex", "preventDefault", "PreventDefault", "OnKeyDown", "@onkeydown", "KeyInterceptor" })
            Assert.DoesNotContain(banned, box, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void No_page_makes_an_input_unreachable_by_Tab_or_swallows_keys()
    {
        foreach (var (name, text) in Razor())
        {
            Assert.False(text.Contains("tabindex=\"-1\"", StringComparison.OrdinalIgnoreCase), $"{name}: tabindex=-1");
            Assert.False(text.Contains("TabIndex=\"-1\"", StringComparison.OrdinalIgnoreCase), $"{name}: TabIndex -1");
            Assert.False(text.Contains("preventDefault", StringComparison.OrdinalIgnoreCase), $"{name}: preventDefault");
        }
    }

    [Fact]
    public void Time_fields_all_use_the_shared_TimeBox_or_a_plain_text_field()
    {
        // MudTimePicker would open a clock dialog the keyboard cannot type into: time boxes must stay TimeBox / MudTextField.
        foreach (var (name, text) in Razor())
            Assert.False(text.Contains("MudTimePicker", StringComparison.Ordinal), $"{name}: MudTimePicker");
    }

    [Fact]
    public void Global_scripts_only_handle_Enter_never_Tab()
    {
        foreach (var js in Directory.EnumerateFiles(Path.Combine(WebRoot(), "wwwroot", "js"), "*.js"))
            Assert.DoesNotContain("'Tab'", File.ReadAllText(js));
    }
}

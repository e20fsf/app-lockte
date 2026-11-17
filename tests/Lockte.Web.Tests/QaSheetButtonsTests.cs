using Lockte.Prod;

namespace Lockte.Web.Tests;

/// <summary>
/// ปุ่มรุ่นของฟอร์มพิมพ์ใบตรวจ QA (UAT-45, #442): the buttons stand in the order of the legacy forms with their captions, a button can print another model's sheet as in the legacy, and a spec no
/// button names still gets a button. The specs here are only names — no content of the real sheets.
/// </summary>
public sealed class QaSheetButtonsTests
{
    private static int id;

    private static QaSpecSummary Spec(string name) => new(Interlocked.Increment(ref id), 1, "WI-QA-T", name, null, null, 0, 0);

    [Fact] // the die-cast form: 32 buttons, row by row as P_REP6ไดคาสท์ stands; the buttons added later come last, not in name order
    public void The_die_cast_buttons_stand_as_on_the_legacy_form()
    {
        var layout = QaSheetButtons.OfDepartment("D");
        Assert.Equal(32, layout.Count);
        Assert.Equal(["029", "052", "061", "063", "147", "168", "233", "A100,GTO R"], layout.Take(8).Select(b => b.Caption));
        Assert.Equal(["043", "BELL,200", "MATE U,087", "KAZE", "068", "ATTILA", "143", "SPARK115I"], layout.Skip(24).Select(b => b.Caption));
        Assert.All(layout, b => Assert.StartsWith("ไดคาสท์", b.SpecName, StringComparison.Ordinal));
        Assert.Equal(27, layout.Select(b => b.SpecName).Distinct().Count()); // the 27 sheets of the legacy, 5 buttons print another model's
        Assert.Equal(layout.Select(b => b.Caption), QaSheetButtons.OfDepartment("T").Select(b => b.Caption));
        Assert.Equal(27, QaSheetButtons.OfMold.Select(b => b.SpecName).Distinct().Count());
    }

    [Fact] // the press form has its two blocks: ผ้าเบรค 18 buttons, ดิสเบรค 39; both have a CLICK and a WAVE of their own
    public void The_press_buttons_are_in_two_blocks()
    {
        var layout = QaSheetButtons.OfDepartment("b");
        Assert.Equal(18, layout.Count(b => b.Section == "ผ้าเบรค"));
        Assert.Equal(39, layout.Count(b => b.Section == "ดิสเบรค"));
        Assert.Equal(57, layout.Select(b => b.SpecName).Distinct().Count());
        Assert.Equal(["อัดผ้าเบรคCLICK", "อัดดิสเบรคCLICK"], layout.Where(b => b.Caption == "CLICK").Select(b => b.SpecName));
        Assert.Empty(QaSheetButtons.OfDepartment("G"));
    }

    [Fact] // the buttons whose spec is there, in the layout's order; one spec can stand behind several buttons; a button without its spec is left out
    public void Buttons_follow_the_layout_and_share_a_spec()
    {
        var c100 = Spec("ไดคาสท์C100");
        var rx = Spec("ไดคาสท์RX100");
        var buttons = QaSheetButtons.Arrange(QaSheetButtons.OfDepartment("D"), [rx, c100]);

        Assert.Equal(["C100,WAVE", "RX100", "XL078,078", "043", "BELL,200", "MATE U,087", "SPARK115I"], buttons.Select(b => b.Caption));
        Assert.Equal([c100.Id, rx.Id, c100.Id, c100.Id, rx.Id, rx.Id, rx.Id], buttons.Select(b => b.Spec.Id));
    }

    [Fact] // a spec no button names (added later by the QA) gets its own button after the layout, by name, captioned with its short name
    public void A_spec_no_button_names_comes_after()
    {
        var buttons = QaSheetButtons.Arrange(QaSheetButtons.OfMold, [Spec("พิมพ์ZZ2"), Spec("พิมพ์029"), Spec("พิมพ์ZZ1")]);
        Assert.Equal(["029", "ZZ1", "ZZ2"], buttons.Select(b => b.Caption));

        var press = QaSheetButtons.Arrange(QaSheetButtons.OfDepartment("B"), [Spec("อัดดิสเบรคNEW"), Spec("อัดผ้าเบรคTS100")]);
        Assert.Equal([("TS100", "ผ้าเบรค"), ("NEW", "อื่น ๆ")], press.Select(b => (b.Caption, b.Section!)));

        var noLayout = QaSheetButtons.Arrange([], [Spec("ฝนผ้าเบรคB"), Spec("ฝนผ้าเบรคA")]);
        Assert.Equal([("A", (string?)null), ("B", null)], noLayout.Select(b => (b.Caption, b.Section)));
    }
}

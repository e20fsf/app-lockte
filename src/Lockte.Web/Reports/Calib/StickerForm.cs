using Lockte.Calib;
using Lockte.SharedKernel;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Calib;

/// <summary>One sticker: หมายเลขเครื่อง and the two dates typed on SCR-04 (BR-010: every sticker of one print has the same CAL and DUE).</summary>
public sealed record StickerLabel(string Code, DateOnly Cal, DateOnly Due);

/// <summary>
/// RPT-04 สติกเกอร์ติดเครื่องมือวัด (วันที่ CAL …), ระดับ A (#201, Q-26): one framed label to an A4 page at its top-left corner, as the print sample has it (print-samples.md):
/// <c>ID :</c> <c>CAL:</c> <c>DUE:</c> in bold, the code, and the dates as <c>วัน/เดือน/ปี พ.ศ.</c> (<c>17/8/2569</c>). The frame is 40 × 21 mm as measured on the sample
/// (the report design says 34 × 17: Q-14 is open), the size being a setting (<see cref="CalibSettingsDto"/>). Positions are not yet overlaid on the sample — the user signs off
/// (#213).
/// </summary>
public static class StickerForm
{
    public const string Title = "สติกเกอร์ติดเครื่องมือวัด";

    /// <summary>The label's left and top edge on the page (mm): "มุมซ้ายบน".</summary>
    public const double Margin = 10;

    public static IReadOnlyList<FormPage> Layout(IReadOnlyList<StickerLabel> labels, double widthMm, double heightMm)
        => labels.Select(l => Label(l, widthMm, heightMm)).ToList();

    private static FormPage Label(StickerLabel label, double width, double height)
    {
        var page = new FormPage(Paper.A4Portrait);
        var (x, y) = (Margin, Margin);
        var line = height / 3;
        var inner = width - 4;
        page.Box(x, y, width, height, 0.5);
        Row(page, x + 2, y + line * 0.75, "ID :", label.Code, inner, 8);
        Row(page, x + 2, y + line * 1.75, "CAL:", Date(label.Cal), inner, 8);
        Row(page, x + 2, y + line * 2.75, "DUE:", Date(label.Due), inner, 8);
        return page;
    }

    private static void Row(FormPage page, double x, double baseline, string caption, string value, double width, double captionWidth)
    {
        page.Text(x, baseline, caption, 14, bold: true);
        page.Text(x + captionWidth, baseline, value, 14, fit: width - captionWidth);
    }

    /// <summary><c>17/8/2569</c>: the day and month as typed (no leading zero), the Buddhist year.</summary>
    public static string Date(DateOnly date) => ThaiDate.Format(date);
}

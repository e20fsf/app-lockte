using System.Globalization;
using System.Text;

namespace Lockte.Web.Reports;

/// <summary>
/// ระดับ A ตรงแบบเดิม (#40, #41): a form laid out where the legacy Access report printed it. Unlike แบบกลาง
/// (<see cref="ReportHtml"/>) the page has no margins, header or footer of its own — each piece of text is placed
/// absolutely in millimetres from the page's top-left corner by its baseline, measured on the legacy PDF, so the print
/// overlays the original within ~2 mm (checked by eye, #41). The body is one or more <c>.page</c> blocks
/// (<see cref="Forms.FormPage"/>), each exactly one sheet; nothing flows to a second page by accident (e.g. ฟอร์มปลดออก
/// printed a blank page 2). TH Sarabun New at the legacy's Angsana New point sizes lands within ~1–2 mm of the
/// legacy line widths, so sizes are kept.
/// </summary>
public static class FormHtml
{
    /// <summary>
    /// Where the baseline of TH Sarabun New sits below the top of a <c>line-height:1</c> box, in em (measured in
    /// Chromium: 0.64–0.71 em with pixel snapping).
    /// </summary>
    public const double BaselineEm = 0.665;

    public const double MillimetresPerPoint = 25.4 / 72;

    public static string Compose(string title, Paper paper, string bodyHtml)
    {
        var (width, height) = ReportHtml.PageMillimetres(paper);
        var css = new StringBuilder();
        foreach (var (file, weight) in ReportHtml.Fonts)
            css.Append($"@font-face{{font-family:'THSarabunNew';font-weight:{weight};src:url('{ReportHtml.FontHost}{Uri.EscapeDataString(file)}') format('truetype')}}");
        css.Append($"@page{{size:{ReportHtml.PageSize(paper)};margin:0}}");
        css.Append("html,body{margin:0}");
        css.Append("body{font-family:'THSarabunNew';color:#000;-webkit-print-color-adjust:exact;print-color-adjust:exact}");
        css.Append($".page{{position:relative;width:{Mm(width)}mm;height:{Mm(height)}mm;overflow:hidden;break-after:page}}");
        css.Append(".page:last-child{break-after:auto}");
        css.Append(".t{position:absolute;white-space:pre;line-height:1}");
        // The legacy dotted underline: 0.5 mm dashes every 1.05 mm, 0.3 mm thick.
        css.Append(".dots{position:absolute;height:0.3mm;background:repeating-linear-gradient(90deg,#000 0 0.5mm,transparent 0.5mm 1.05mm)}");
        css.Append(".line{position:absolute;background:#000}");
        css.Append(".box{position:absolute;box-sizing:border-box;border:0.3mm solid #000}");
        return $"<!doctype html><html lang=\"th\"><head><meta charset=\"utf-8\"><title>{System.Net.WebUtility.HtmlEncode(title)}</title>"
               + $"<style>{css}</style></head><body>{bodyHtml}</body></html>";
    }

    /// <summary>A length for CSS: invariant culture, at most two decimals.</summary>
    public static string Mm(double millimetres) => millimetres.ToString("0.##", CultureInfo.InvariantCulture);
}

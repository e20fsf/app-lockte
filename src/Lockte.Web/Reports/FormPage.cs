using System.Globalization;
using System.Net;
using System.Text;

namespace Lockte.Web.Reports;

/// <summary>How a text sits on its x: from its start, around its centre, or ending at it.</summary>
public enum TextAnchor
{
    Start,
    Middle,
    End,
}

/// <summary>
/// One printed page of a ระดับ A form (#40: ตรงแบบเดิม — กระดาษ ตำแหน่งช่อง ข้อความ): an SVG whose user unit is 1 mm
/// from the paper's top-left corner, so every text, dotted line and box goes exactly where the legacy print had it
/// (measured on the sample PDF). A text is placed by its baseline. The legacy labels used spaces to line things up;
/// here each piece is placed on its own, so TH Sarabun New's wider spaces do not push the rest out of place.
/// </summary>
public sealed class FormPage(Paper paper)
{
    /// <summary>Line weight of the legacy forms' boxes and table rules (1 pt ≈ 0.36 mm).</summary>
    public const double Rule = 0.36;

    private readonly StringBuilder svg = new();

    public Paper Paper { get; } = paper;

    /// <summary>
    /// A text with its baseline at (<paramref name="x"/>, <paramref name="baseline"/>) in TH Sarabun New
    /// <paramref name="points"/> pt. With <paramref name="fit"/> (mm), a text longer than that is squeezed into it when
    /// the page is printed (<see cref="FormPageHtml.FitScript"/>) — the legacy cut it off at the box edge (D-1).
    /// Blank text puts nothing on the page.
    /// </summary>
    public FormPage Text(double x, double baseline, string? text, double points, TextAnchor anchor = TextAnchor.Start,
        bool bold = false, double? fit = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return this;
        svg.Append($"<text x=\"{N(x)}\" y=\"{N(baseline)}\" font-size=\"{N(points * 25.4 / 72)}\"");
        if (anchor != TextAnchor.Start) svg.Append($" text-anchor=\"{(anchor == TextAnchor.Middle ? "middle" : "end")}\"");
        if (bold) svg.Append(" font-weight=\"bold\"");
        if (fit is { } width) svg.Append($" data-fit=\"{N(width)}\"");
        svg.Append('>').Append(WebUtility.HtmlEncode(text.Trim())).Append("</text>");
        return this;
    }

    /// <summary>
    /// A row of full stops from <paramref name="x0"/> to <paramref name="x1"/> ("ลงชื่อ......"), stretched to exactly
    /// that length: the legacy typed them into the label, and TH Sarabun New's full stop is not as wide as Angsana's.
    /// </summary>
    public FormPage Leader(double x0, double x1, double baseline, double points)
    {
        var count = Math.Max(1, (int)Math.Round((x1 - x0) / 0.79));
        svg.Append($"<text x=\"{N(x0)}\" y=\"{N(baseline)}\" font-size=\"{N(points * 25.4 / 72)}\" textLength=\"{N(x1 - x0)}\" lengthAdjust=\"spacing\">")
            .Append('.', count).Append("</text>");
        return this;
    }

    /// <summary>The legacy dotted line to write on (dots 0.52 × 0.36 mm, every 1.045 mm), its bottom at <paramref name="y"/>.</summary>
    public FormPage Dots(double x0, double x1, double y)
    {
        svg.Append($"<line class=\"dots\" x1=\"{N(x0)}\" y1=\"{N(y - 0.18)}\" x2=\"{N(x1)}\" y2=\"{N(y - 0.18)}\"/>");
        return this;
    }

    /// <summary>A straight rule centred on the line from (x0, y0) to (x1, y1).</summary>
    public FormPage Line(double x0, double y0, double x1, double y1, double weight = Rule)
    {
        svg.Append($"<line x1=\"{N(x0)}\" y1=\"{N(y0)}\" x2=\"{N(x1)}\" y2=\"{N(y1)}\" stroke-width=\"{N(weight)}\"/>");
        return this;
    }

    /// <summary>An empty box (checkbox, one digit's box) by its outer edges.</summary>
    public FormPage Box(double x, double y, double width, double height, double weight = Rule)
    {
        var h = weight / 2;
        svg.Append($"<rect x=\"{N(x + h)}\" y=\"{N(y + h)}\" width=\"{N(width - weight)}\" height=\"{N(height - weight)}\" stroke-width=\"{N(weight)}\"/>");
        return this;
    }

    public string ToSvg()
    {
        var (width, height) = ReportHtml.PageMillimetres(Paper);
        return $"<svg class=\"page\" xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(width)}mm\" height=\"{N(height)}mm\" viewBox=\"0 0 {N(width)} {N(height)}\">"
               + svg + "</svg>";
    }

    /// <summary>Millimetres to two decimals, invariant culture.</summary>
    public static string N(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>
/// ระดับ A (#40) around a form's pages: one HTML document for Chromium with no แบบกลาง header/footer and no page
/// margins — each page is a <see cref="FormPage"/> the size of the paper, as the legacy Access report printed it.
/// </summary>
public static class FormPageHtml
{
    /// <summary>
    /// <c>lockteFitToPage()</c>, which <see cref="ReportPdfRenderer"/> runs once the fonts are in: a text with
    /// <c>data-fit</c> longer than its box is squeezed to it (ฟอนต์ปรับขนาดให้ลงตำแหน่งเดิม).
    /// </summary>
    public const string FitScript =
        "window.lockteFitToPage=()=>{for(const t of document.querySelectorAll('text[data-fit]')){const w=+t.dataset.fit;"
        + "if(t.getComputedTextLength()>w){t.setAttribute('textLength',w);t.setAttribute('lengthAdjust','spacingAndGlyphs');}}};";

    public static string Compose(string title, IReadOnlyList<FormPage> pages)
    {
        if (pages.Count == 0) throw new ArgumentException("A form prints at least one page.", nameof(pages));
        var paper = pages[0].Paper;
        var css = new StringBuilder();
        foreach (var (file, weight) in ReportHtml.Fonts)
            css.Append($"@font-face{{font-family:'THSarabunNew';font-weight:{weight};src:url('{ReportHtml.FontHost}{Uri.EscapeDataString(file)}') format('truetype')}}");
        css.Append($"@page{{size:{ReportHtml.PageSize(paper)};margin:0}}");
        css.Append("html,body{margin:0}");
        css.Append("svg.page{display:block;break-after:page;font-family:'THSarabunNew';fill:#000;stroke:#000;stroke-width:0}");
        css.Append("svg.page:last-child{break-after:auto}");
        css.Append("svg.page text{stroke:none;white-space:pre}");
        css.Append("svg.page rect{fill:none}");
        css.Append($"svg.page line.dots{{stroke-width:0.36;stroke-dasharray:0.52 0.525}}");

        return $"<!doctype html><html lang=\"th\"><head><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(title)}</title>"
               + $"<style>{css}</style><script>{FitScript}</script></head><body>"
               + string.Concat(pages.Select(p => p.ToSvg())) + "</body></html>";
    }
}

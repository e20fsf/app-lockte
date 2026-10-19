using System.Text;

namespace Lockte.Web.Reports;

/// <summary>Paper of a printed document (#40): A4 portrait/landscape as before (Letter → A4), A5 kept for the 10 A5 forms.</summary>
public enum Paper
{
    A4Portrait,
    A4Landscape,
    A5Portrait,
    A5Landscape,
}

/// <summary>What a printed document is: its title (as the legacy report printed it), paper, and เลขเอกสารควบคุม.</summary>
/// <param name="DocumentNumber">The printed text of the document's number (<see cref="DocumentNumberService"/>), or none.</param>
public sealed record ReportDocument(string Title, Paper Paper, string? DocumentNumber = null);

/// <summary>ท้ายหน้า: when it was printed and by whom (#40 แบบกลาง, #39 login).</summary>
public sealed record PrintStamp(DateTimeOffset PrintedAt, string PrintedBy);

/// <summary>
/// แบบกลางระดับ B (#40) around a report's body: one HTML document for Chromium. The page header and footer are CSS page
/// margin boxes, so they repeat on every page, use the document's own font (Chromium's header/footer templates cannot
/// load it, #21) and count pages: หัว "บริษัท ล็อคเต้ จำกัด" + ชื่อเอกสาร กลาง, เลขเอกสารควบคุม ขวาบน, ไม่มีโลโก้;
/// ท้าย วันเวลาที่พิมพ์ + ผู้พิมพ์ ซ้าย, "หน้า x จาก y" ขวา. A body wider than the page is shrunk to fit
/// (<see cref="FitToPageScript"/>); long text wraps instead of being cut (D-1, D-4).
/// </summary>
public static class ReportHtml
{
    public const string Company = "บริษัท ล็อคเต้ จำกัด";

    /// <summary>
    /// Fonts come from the app folder through Chromium request interception at this made-up host — not as a 1 MB data:
    /// URI in every document (#21) and not from the server's installed fonts.
    /// </summary>
    public const string FontHost = "https://report-fonts.lockte.invalid/";

    public static readonly IReadOnlyList<(string File, string Weight)> Fonts = [("THSarabunNew.ttf", "normal"), ("THSarabunNew Bold.ttf", "bold")];

    /// <summary>
    /// Defines <c>lockteFitToPage()</c>, which <see cref="ReportPdfRenderer"/> runs once the fonts are in: the viewport
    /// is the page's printable width, so a wider body is zoomed down to it (no columns pushed to another page, D-4).
    /// </summary>
    public const string FitToPageScript =
        "window.lockteFitToPage=()=>{const b=document.body;const w=b.scrollWidth,a=document.documentElement.clientWidth;if(w>a+1)b.style.zoom=a/w;};";

    /// <summary>Margins in mm: top leaves room for the two header lines, bottom for the footer.</summary>
    public const double MarginTop = 24, MarginSide = 12, MarginBottom = 15;

    public static string PageSize(Paper paper) => paper switch
    {
        Paper.A4Portrait => "A4 portrait",
        Paper.A4Landscape => "A4 landscape",
        Paper.A5Portrait => "A5 portrait",
        Paper.A5Landscape => "A5 landscape",
        _ => throw new ArgumentOutOfRangeException(nameof(paper)),
    };

    /// <summary>Width × height in mm.</summary>
    public static (double Width, double Height) PageMillimetres(Paper paper) => paper switch
    {
        Paper.A4Portrait => (210, 297),
        Paper.A4Landscape => (297, 210),
        Paper.A5Portrait => (148, 210),
        Paper.A5Landscape => (210, 148),
        _ => throw new ArgumentOutOfRangeException(nameof(paper)),
    };

    public static string Compose(ReportDocument document, PrintStamp stamp, string bodyHtml)
    {
        const string margin = "font-family:'THSarabunNew';font-size:12pt;";
        var css = new StringBuilder();
        foreach (var (file, weight) in Fonts)
            css.Append($"@font-face{{font-family:'THSarabunNew';font-weight:{weight};src:url('{FontHost}{Uri.EscapeDataString(file)}') format('truetype')}}");
        css.Append($"@page{{size:{PageSize(document.Paper)};margin:{MarginTop}mm {MarginSide}mm {MarginBottom}mm {MarginSide}mm;");
        css.Append($"@top-center{{content:{CssString($"{Company}\n{document.Title}")};white-space:pre;text-align:center;vertical-align:bottom;"
                   + "font-family:'THSarabunNew';font-size:16pt;font-weight:bold;line-height:1.1;padding-bottom:3mm}");
        if (!string.IsNullOrWhiteSpace(document.DocumentNumber))
            css.Append($"@top-right{{content:{CssString(document.DocumentNumber)};white-space:pre;text-align:right;vertical-align:top;{margin}padding-top:6mm}}");
        css.Append($"@bottom-left{{content:{CssString($"พิมพ์เมื่อ {stamp.PrintedAt.ToString("d/M/yyyy HH:mm", SharedKernel.ThaiDate.Culture)} โดย {stamp.PrintedBy}")};{margin}vertical-align:top;padding-top:2mm}}");
        css.Append($"@bottom-right{{content:\"หน้า \" counter(page) \" จาก \" counter(pages);text-align:right;{margin}vertical-align:top;padding-top:2mm}}");
        css.Append('}');
        css.Append(BaseCss);

        return $"<!doctype html><html lang=\"th\"><head><meta charset=\"utf-8\"><title>{System.Net.WebUtility.HtmlEncode(document.Title)}</title>"
               + $"<style>{css}</style><script>{FitToPageScript}</script></head><body>{bodyHtml}</body></html>";
    }

    /// <summary>The look shared by every level-B report body: plain tables, bold headers underlined, no vertical lines (print-forms.md).</summary>
    private const string BaseCss =
        "html,body{margin:0}"
        + "body{font-family:'THSarabunNew';font-size:14pt;line-height:1.15;color:#000;-webkit-print-color-adjust:exact;print-color-adjust:exact}"
        + "h2{font-size:14pt;font-weight:normal;text-align:center;margin:0 0 2pt}"
        + ".sub{text-align:center;margin:0 0 6pt;white-space:pre-wrap}"
        + "table{border-collapse:collapse;width:100%}"
        + "thead{display:table-header-group}"
        + "th{font-weight:bold;text-align:left;border-bottom:1.5pt solid #000;padding:1pt 4pt;vertical-align:bottom}"
        + "td{padding:0 4pt;vertical-align:top;overflow-wrap:anywhere}"
        + "tr{break-inside:avoid}"
        + ".num{text-align:right;white-space:nowrap}"
        + ".total td{border-top:1pt solid #000;padding-top:2pt}"
        + ".end{border-bottom:3pt double #000;height:2pt}"
        + ".no-data{text-align:center;margin-top:12mm;font-size:16pt}";

    /// <summary>A CSS string literal: quotes, backslashes and line breaks escaped; &lt; and &gt; too, so it cannot end the style.</summary>
    internal static string CssString(string text)
    {
        var s = new StringBuilder("\"");
        foreach (var c in text)
        {
            s.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\A ",
                '\r' => "",
                '<' => "\\3C ",
                '>' => "\\3E ",
                _ => c.ToString(),
            });
        }
        return s.Append('"').ToString();
    }
}

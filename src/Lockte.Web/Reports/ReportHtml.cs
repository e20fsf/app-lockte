using System.Text;

namespace Lockte.Web.Reports;

/// <summary>Paper of a printed document (#40): A4 portrait/landscape as before (Letter → A4), A5 kept for the 10 A5 forms.</summary>
public enum Paper
{
    A4Portrait,
    A4Landscape,
    A5Portrait,
    A5Landscape,
    /// <summary>US Letter (215.9 × 279.4 mm): the paper of ใบเสร็จรับเงินขายของเก่า (เงินสดย่อย RPT-06).</summary>
    LetterPortrait,
}

/// <summary>What a printed document is: its title (as the legacy report printed it), paper, and เลขเอกสารควบคุม.</summary>
/// <param name="DocumentNumber">The printed text of the document's number (<see cref="DocumentNumberService"/>), or none.</param>
/// <param name="DateAtTop">The Access layout of รายงานการลางาน (#411): the print date at the top left and "หน้า x จาก y" at the top right, no footer.</param>
/// <param name="CompanyOnly">The Access layout of ใบขออนุมัติทำงานล่วงเวลา (#426): the header is the company line alone, no title line under it.</param>
/// <param name="PageFooter">The sheet's own left and right text at the very bottom of every page, in place of the print date and page number (#426).</param>
/// <param name="Footer">False for a one-page sheet that prints no footer at all (no print date, no page number — #408 ใบขออนุมัติทำงานล่วงเวลา).</param>
public sealed record ReportDocument(string Title, Paper Paper, string? DocumentNumber = null, bool Footer = true, bool DateAtTop = false, int? HeaderPt = null,
    bool CompanyOnly = false, (string Left, string Right)? PageFooter = null);

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
        "window.lockteFitToPage=()=>{const b=document.body;const A=()=>document.documentElement.clientWidth;"
        // one item is one line (#395): a table wider than the page first shrinks the font of its widest column (down to 70%),
        // so one long remark does not shrink the whole report; whatever is still too wide is zoomed down to the page
        + "for(let i=0;i<40&&b.scrollWidth>A()+1;i++){let best=null,bw=0;"
        + "for(const t of document.querySelectorAll('table')){const cols=[];for(const c of t.querySelectorAll('td,th')){if(c.colSpan>1)continue;(cols[c.cellIndex]??=[]).push(c);}"
        + "cols.forEach(cs=>{const w=Math.max(...cs.map(c=>c.scrollWidth));const f=parseFloat(getComputedStyle(cs[0]).fontSize);const base=+(cs[0].dataset.fs0??f);if(w>bw&&f>base*0.7+0.1){bw=w;best=cs;}});}"
        + "if(!best)break;for(const c of best){c.dataset.fs0??=parseFloat(getComputedStyle(c).fontSize);c.style.fontSize=(parseFloat(getComputedStyle(c).fontSize)*0.94)+'px';}}"
        + "const w=b.scrollWidth,a=A();if(w>a+1)b.style.zoom=a/w;};";

    /// <summary>Margins in mm: top leaves room for the two header lines, bottom for the footer.</summary>
    public const double MarginTop = 26, MarginSide = 12, MarginBottom = 15;

    public static string PageSize(Paper paper) => paper switch
    {
        Paper.A4Portrait => "A4 portrait",
        Paper.A4Landscape => "A4 landscape",
        Paper.A5Portrait => "A5 portrait",
        Paper.A5Landscape => "A5 landscape",
        Paper.LetterPortrait => "Letter portrait",
        _ => throw new ArgumentOutOfRangeException(nameof(paper)),
    };

    /// <summary>Width × height in mm.</summary>
    public static (double Width, double Height) PageMillimetres(Paper paper) => paper switch
    {
        Paper.A4Portrait => (210, 297),
        Paper.A4Landscape => (297, 210),
        Paper.A5Portrait => (148, 210),
        Paper.A5Landscape => (210, 148),
        Paper.LetterPortrait => (215.9, 279.4),
        _ => throw new ArgumentOutOfRangeException(nameof(paper)),
    };

    /// <summary>
    /// The header's size in pt (company and title, one CSS margin box, so one size): 16 → 17 to follow the Access samples, where the heading is
    /// larger than the body (#408).
    /// </summary>
    public const int TitleSizePt = 17;

    /// <summary>The footer's print date: "5 พฤศจิกายน 2569" (full month, พ.ศ.).</summary>
    public static string PrintDate(DateTimeOffset printedAt) => printedAt.ToString("d MMMM yyyy", SharedKernel.ThaiDate.Culture);

    public static string Compose(ReportDocument document, PrintStamp stamp, string bodyHtml, ReportFont? font = null)
    {
        font ??= ReportFonts.ThSarabunNew;
        var margin = $"font-family:'{font.Family}';font-size:12pt;";
        var css = new StringBuilder(font.FaceCss);
        css.Append($"@page{{size:{PageSize(document.Paper)};margin:{MarginTop}mm {MarginSide}mm {MarginBottom}mm {MarginSide}mm;");
        // #408: company bigger than the title, as the Access reports print them; the block sits a little lower (less padding under it)
        css.Append($"@top-center{{content:{CssString(document.CompanyOnly ? Company : $"{Company}\n{document.Title}")};white-space:pre;text-align:center;vertical-align:bottom;"
                   + $"font-family:'{font.Family}';font-size:{document.HeaderPt ?? TitleSizePt}pt;font-weight:bold;line-height:1.1;padding-bottom:2mm}}");
        if (!string.IsNullOrWhiteSpace(document.DocumentNumber))
            css.Append($"@top-right{{content:{CssString(document.DocumentNumber)};white-space:pre;text-align:right;vertical-align:top;{margin}padding-top:6mm}}");
        if (document.DateAtTop)
        {
            css.Append($"@top-left{{content:{CssString(PrintDate(stamp.PrintedAt))};{margin}vertical-align:middle;padding-top:6mm}}");
            if (string.IsNullOrWhiteSpace(document.DocumentNumber))
                css.Append($"@top-right{{content:\"หน้า \" counter(page) \" จาก \" counter(pages);text-align:right;vertical-align:middle;{margin}padding-top:6mm}}");
            else
                css.Append($"@bottom-right{{content:\"หน้า \" counter(page) \" จาก \" counter(pages);text-align:right;{margin}vertical-align:top;padding-top:2mm}}");
        }
        else if (document.PageFooter is { } pageFooter)
        {
            css.Append($"@bottom-left{{content:{CssString(pageFooter.Left)};{margin}font-weight:bold;vertical-align:bottom;padding-bottom:4mm}}");
            css.Append($"@bottom-right{{content:{CssString(pageFooter.Right)};text-align:right;{margin}font-weight:bold;vertical-align:bottom;padding-bottom:4mm}}");
        }
        else if (document.Footer)
        {
            // #408: the footer is the print date in full Thai (5 พฤศจิกายน 2569) on every page, no time and no user
            css.Append($"@bottom-left{{content:{CssString(PrintDate(stamp.PrintedAt))};{margin}vertical-align:top;padding-top:2mm}}");
            css.Append($"@bottom-right{{content:\"หน้า \" counter(page) \" จาก \" counter(pages);text-align:right;{margin}vertical-align:top;padding-top:2mm}}");
        }
        css.Append('}');
        css.Append(BaseCss(font));

        return $"<!doctype html><html lang=\"th\"><head><meta charset=\"utf-8\"><title>{System.Net.WebUtility.HtmlEncode(document.Title)}</title>"
               + $"<style>{css}</style><script>{FitToPageScript}</script></head><body>{bodyHtml}</body></html>";
    }

    /// <summary>The look shared by every level-B report body: plain tables, bold headers underlined, no vertical lines (print-forms.md).</summary>
    private static string BaseCss(ReportFont font) =>
        "html,body{margin:0}"
        + $"body{{font-family:'{font.Family}';" + "font-size:14pt;line-height:1.15;color:#000;-webkit-print-color-adjust:exact;print-color-adjust:exact}"
        + "h2{font-size:14pt;font-weight:normal;text-align:center;margin:0 0 2pt}"
        + ".sub{text-align:center;margin:0 0 6pt;white-space:pre-wrap}"
        + "table{border-collapse:collapse;width:100%}"
        + "thead{display:table-header-group}"
        + "th{white-space:nowrap;font-weight:bold;text-align:left;border-bottom:1.5pt solid #000;padding:1pt 4pt;vertical-align:bottom}"
        + "td{padding:0 4pt;vertical-align:top;white-space:nowrap}"
        + "tr{break-inside:avoid}"
        + ".num{text-align:right;white-space:nowrap}"
        + ".nw{white-space:nowrap}"
        + ".total td{border-top:1pt solid #000;padding-top:2pt}"
        // #408: the word "รวม" (the label cells of a total row) is bold
        + ".total td:not(.num),.lbl{font-weight:bold}"
        // #408: Angsana New's tone marks over an upper vowel (ซื้, น้ำ) rise above the line box; a collapsed table border is painted OVER the
        // text, so the first row under the header rule lost its marks. Room above the first row keeps them clear of the rule.
        + "tbody tr:first-child td{padding-top:4pt}"
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

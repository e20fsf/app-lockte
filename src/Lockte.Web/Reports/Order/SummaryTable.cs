using System.IO.Compression;
using System.Security;
using System.Text;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Order;

public enum SummaryFormat { Text, Integer, Quantity, Money, Percent }

public sealed record SummaryColumn(string Header, SummaryFormat Format = SummaryFormat.Text);

/// <summary>
/// A summary as a table: what a report of the ผลผลิตและORDER summaries prints (แบบกลาง B) and what the "Excel" button downloads — the same rows, so the file is
/// the screen cell for cell (INT-01). Cells are typed (<c>string</c>, <c>int</c>, <c>decimal</c>, or null for blank); a blank is blank, not 0.
/// </summary>
public sealed record SummaryTable(string Title, string? Filter, IReadOnlyList<SummaryColumn> Columns, IReadOnlyList<object?[]> Rows, object?[]? TotalRow = null)
{
    /// <summary>A cell as the report prints it.</summary>
    public static string Show(SummaryColumn column, object? value) => value switch
    {
        null => "",
        string text => text,
        decimal number => column.Format switch
        {
            SummaryFormat.Money => ReportText.Decimal2(number),
            SummaryFormat.Integer => number.ToString("#,##0", ThaiDate.Culture),
            SummaryFormat.Percent => number.ToString("0.0000", ThaiDate.Culture) + "%",
            _ => number.ToString("#,##0.####", ThaiDate.Culture),
        },
        int number => number.ToString("#,##0", ThaiDate.Culture),
        _ => Convert.ToString(value, ThaiDate.Culture) ?? "",
    };
}

/// <summary>
/// A minimal .xlsx (Open XML) written by hand: one sheet, a bold heading row, text and number cells — no template and no library (INT-01: the legacy produced
/// a datasheet to copy from). Numbers are numbers in the cell, with a format that shows the thousands separator and the decimals the report shows.
/// </summary>
public static class XlsxWriter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Write(SummaryTable table)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
                """);
            Add(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Add(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Summary" sheetId="1" r:id="rId1"/></sheets></workbook>
                """);
            Add(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
                """);
            // cell styles: 0 normal, 1 bold, 2 #,##0 (3), 3 #,##0.00 (4), 4 #,##0.#### (custom 164)
            Add(zip, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="2"><numFmt numFmtId="164" formatCode="#,##0.####"/><numFmt numFmtId="165" formatCode="0.0000&quot;%&quot;"/></numFmts><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="6"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="3" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="4" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>
                """);
            Add(zip, "xl/worksheets/sheet1.xml", Sheet(table));
        }
        return stream.ToArray();
    }

    private static string Sheet(SummaryTable table)
    {
        var xml = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        var row = 1;
        void Text(string text, int r) => xml.Append($"""<row r="{r}"><c r="A{r}" t="inlineStr" s="1"><is><t>{SecurityElement.Escape(text)}</t></is></c></row>""");
        Text(table.Title, row++);
        if (table.Filter is { } filter) Text(filter, row++);
        row++;
        xml.Append($"<row r=\"{row}\">");
        for (var c = 0; c < table.Columns.Count; c++)
            xml.Append($"""<c r="{Ref(c, row)}" t="inlineStr" s="1"><is><t>{SecurityElement.Escape(table.Columns[c].Header)}</t></is></c>""");
        xml.Append("</row>");
        row++;
        foreach (var values in table.Rows.Concat(table.TotalRow is { } total ? [total] : []))
        {
            var bold = table.TotalRow is not null && ReferenceEquals(values, table.TotalRow);
            xml.Append($"<row r=\"{row}\">");
            for (var c = 0; c < table.Columns.Count && c < values.Length; c++)
                Cell(xml, table.Columns[c], values[c], Ref(c, row), bold);
            xml.Append("</row>");
            row++;
        }
        return xml.Append("</sheetData></worksheet>").ToString();
    }

    private static void Cell(StringBuilder xml, SummaryColumn column, object? value, string reference, bool bold)
    {
        switch (value)
        {
            case null:
                return; // a blank cell stays blank
            case decimal number:
                xml.Append($"""<c r="{reference}" s="{(column.Format switch { SummaryFormat.Money => 3, SummaryFormat.Integer => 2, SummaryFormat.Percent => 5, _ => 4 })}"><v>{number.ToString("0.############", System.Globalization.CultureInfo.InvariantCulture)}</v></c>""");
                return;
            case int number:
                xml.Append($"""<c r="{reference}" s="2"><v>{number.ToString(System.Globalization.CultureInfo.InvariantCulture)}</v></c>""");
                return;
            default:
                xml.Append($"""<c r="{reference}" t="inlineStr" s="{(bold ? 1 : 0)}"><is><t xml:space="preserve">{SecurityElement.Escape(Convert.ToString(value, ThaiDate.Culture))}</t></is></c>""");
                return;
        }
    }

    /// <summary>A1-style reference of a zero-based column and a row (columns past Z: AA, AB …).</summary>
    public static string Ref(int column, int row)
    {
        var name = "";
        for (var n = column + 1; n > 0; n = (n - 1) / 26) name = (char)('A' + (n - 1) % 26) + name;
        return name + row;
    }

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.TrimStart());
    }
}

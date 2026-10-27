using System.Globalization;
using Lockte.Prod;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Order.Qa;

/// <summary>The values of one inspection sheet: the spec of the model, as the sheet prints it.</summary>
public sealed record QaSheetContent(string Title, string DocumentNumber, string? Models, IReadOnlyList<string> Points, IReadOnlyList<string> Defects, byte[]? Image, string? ImageContentType);

/// <summary>A production line to inspect, as the fields of the sheet show it.</summary>
public sealed record QaSheetLine(string? ProductCode, string? ProductName, string? Lot, DateOnly? Date, decimal Good, int SampleSize);

/// <summary>
/// ใบตรวจคุณภาพ QA ระดับ A (#130, RPT-20): the frame of the family's template with the spec of the model in it, one page for each production line marked for inspection — a blank sheet when
/// there is none, as the legacy printed. The sheet prints on A4 landscape, ตรงแบบเดิม within ~2 mm (checked against the sample prints). The text of the points and defects is as the QA typed it.
/// </summary>
public static class QaSheetForm
{
    public const string Title = "ใบตรวจคุณภาพ QA";

    /// <summary>The document number as the sheet prints it: <c>WI-QA055-21/2551 ฉบับที่ 2</c> — the model's sequence goes before the year.</summary>
    public static string DocumentNumber(string code, int? sequence, string? revision)
    {
        var slash = code.IndexOf('/');
        var number = sequence is { } n && slash >= 0 ? $"{code[..slash]}-{n.ToString(CultureInfo.InvariantCulture)}{code[slash..]}" : sequence is { } m ? $"{code}-{m.ToString(CultureInfo.InvariantCulture)}" : code;
        return string.IsNullOrWhiteSpace(revision) ? number : $"{number} {revision.Trim()}";
    }

    /// <summary>A measure point as one line: the number and the value as typed (a value that starts with spaces is lined up after the number as the legacy did), or the words and the value.</summary>
    public static string PointText(QaMeasurePointDto point)
    {
        var standard = point.Standard;
        if (!string.IsNullOrEmpty(point.PointNo))
            return point.PointNo + (standard is null ? "" : char.IsWhiteSpace(standard[0]) ? standard : " " + standard);
        if (!string.IsNullOrEmpty(point.Description))
            return point.Description + (standard is null ? "" : " " + standard.TrimStart());
        return standard?.TrimStart() ?? "";
    }

    public static IReadOnlyList<FormPage> Layout(QaFormTemplate template, QaSheetContent content, IReadOnlyList<QaSheetLine> lines)
        => (lines.Count == 0 ? [null] : lines.Cast<QaSheetLine?>().ToList()).Select(line => Page(template, content, line)).ToList();

    private static FormPage Page(QaFormTemplate t, QaSheetContent content, QaSheetLine? line)
    {
        var page = new FormPage(Paper.A4Landscape);
        foreach (var r in t.Rules)
        {
            if (r.X1 - r.X0 >= r.Y1 - r.Y0) page.Line(r.X0, (r.Y0 + r.Y1) / 2, r.X1, (r.Y0 + r.Y1) / 2, r.Y1 - r.Y0);
            else page.Line((r.X0 + r.X1) / 2, r.Y0, (r.X0 + r.X1) / 2, r.Y1, r.X1 - r.X0);
        }
        foreach (var s in t.Texts)
        {
            if (s.LeaderEnd is { } end) page.Leader(s.X, end, s.Base, s.Size);
            else page.Text(s.X, s.Base, s.Text, s.Size, (TextAnchor)s.Anchor, s.Bold);
        }
        Slot(page, t.Title, content.Title);
        Slot(page, t.DocumentNumber, content.DocumentNumber);
        if (t.Models is { } models) Slot(page, models, content.Models);
        for (var i = 0; i < t.Points.Count && i < content.Points.Count; i++) Slot(page, t.Points[i], content.Points[i]);
        for (var i = 0; i < t.Defects.Count && i < content.Defects.Count; i++) Slot(page, t.Defects[i], content.Defects[i]);
        if (t.Picture is { } box && content.Image is { Length: > 0 } image) page.Image(box.X, box.Y, box.Width, box.Height, content.ImageContentType ?? "image/png", image);
        if (line is not null) Fill(page, t, line);
        return page;
    }

    private static void Slot(FormPage page, QaTextSlot slot, string? text)
        => page.Text(slot.X, slot.Base, text, slot.Size, (TextAnchor)slot.Anchor, slot.Bold);

    private static void Fill(FormPage page, QaFormTemplate t, QaSheetLine line)
    {
        foreach (var f in t.Fields)
        {
            var text = f.Kind switch
            {
                QaFieldKind.ProductCode => line.ProductCode,
                QaFieldKind.ProductName => line.ProductName,
                QaFieldKind.Lot => line.Lot,
                QaFieldKind.Quantity => line.Good.ToString("#,##0.####", ThaiDate.Culture),
                QaFieldKind.Date => line.Date is { } date ? ReportText.Date(date) : null, // every date box shows the date of the report, as the legacy printed (the boxes under the signatures too)
                QaFieldKind.SampleSize => line.SampleSize.ToString(CultureInfo.InvariantCulture),
                QaFieldKind.Mark => int.TryParse(f.Near, out var first) && line.SampleSize >= first ? "/" : null,
                _ => null,
            };
            page.Text(f.X, f.Base, text, f.Size, (TextAnchor)f.Anchor);
        }
    }
}

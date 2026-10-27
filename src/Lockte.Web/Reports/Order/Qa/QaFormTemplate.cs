namespace Lockte.Web.Reports.Order.Qa;

/// <summary>A thin rule or one side of a tick box, as the rectangle the legacy PDF filled (mm from the paper's top-left corner).</summary>
public sealed record QaRule(double X0, double Y0, double X1, double Y1);

/// <summary>
/// A text the same on every sheet of a family, placed by its baseline. <see cref="Anchor"/>: 0 starts at X, 1 is centred on it, 2 ends at it (<see cref="TextAnchor"/>).
/// A text with <see cref="LeaderEnd"/> is a trail of full stops from X to there (the legacy typed them into the label).
/// </summary>
public sealed record QaStaticText(double X, double Base, double Size, bool Bold, int Anchor, string Text, double? LeaderEnd);

/// <summary>Where a text of the model goes: its baseline and point size, and how it sits on X.</summary>
public sealed record QaTextSlot(double X, double Base, double Size, bool Bold, int Anchor);

/// <summary>The box of the picture of the piece (mm).</summary>
public sealed record QaPicture(double X, double Y, double Width, double Height);

/// <summary>What a data field of the sheet shows: the product, the lot, the date of production, the quantity, the sample size, or a "/" when the sample reaches the column (<see cref="QaField.Near"/> = the first piece).</summary>
public enum QaFieldKind { ProductCode, ProductName, Lot, Quantity, Date, SampleSize, Mark }

/// <summary><paramref name="Near"/>: for a date, the start of the label beside it ("วันที่ผลิต"); for a mark, the number of the first piece of the column it stands for.</summary>
public sealed record QaField(QaFieldKind Kind, double X, double Base, double Size, int Anchor, string Near);

/// <summary>
/// A sheet that holds a strip for each product (packing): what is above <paramref name="Top"/> is the header, from there to <paramref name="FooterTop"/> the first strip, which is printed again
/// every <paramref name="Pitch"/> mm — <paramref name="PerPage"/> to a page — and what is below is the footer (the signatures), printed after the last strip on its page, or on a page of its own
/// when four strips have filled the page (as the legacy blank form did). A <paramref name="Blank"/> form prints its strips empty.
/// </summary>
public sealed record QaStrip(double Top, double FooterTop, double Pitch, int PerPage, bool Blank);

/// <summary>
/// The frame of an inspection sheet of one family (#130): what is on every sheet — rules, tick boxes, labels — the places the model's own text goes, and the data fields. Made by
/// <c>tools/qa-form-templates/generate.py</c> from the recovered report and its sample print; <see cref="QaFormTemplates"/> holds them.
/// </summary>
public sealed record QaFormTemplate(
    string Key,
    IReadOnlyList<QaRule> Rules,
    IReadOnlyList<QaStaticText> Texts,
    QaTextSlot Title,
    QaTextSlot DocumentNumber,
    IReadOnlyList<QaTextSlot> Points,
    IReadOnlyList<QaTextSlot> Defects,
    QaTextSlot? Models,
    QaPicture? Picture,
    IReadOnlyList<QaField> Fields,
    QaStrip? Strip = null);

/// <summary>The templates by the group of the sheet (the start of the name of its spec, see <c>QaSpecGroups</c>).</summary>
public static partial class QaFormTemplates
{
    private static readonly Lazy<IReadOnlyList<QaFormTemplate>> All = new(Build);

    /// <summary>
    /// The template of the sheet whose spec is named <paramref name="specName"/>; null when its group has none yet. A group can have several shapes of sheet (with measure points or
    /// without): with the numbers of points and defects of the spec the template whose rows fit them best is taken.
    /// </summary>
    public static QaFormTemplate? For(string specName, int? points = null, int? defects = null)
    {
        var group = Lockte.Prod.QaSpecGroups.GroupOf(specName);
        if (Lockte.Prod.QaSpecGroups.IsPacking(group)) group = group.EndsWith("ฟอร์มเปล่า", StringComparison.Ordinal) ? "*packing-blank" : "*packing"; // every packing sheet has the one frame
        var candidates = GroupKeys.Where(g => g.Group == group).Select(g => All.Value.First(t => t.Key == g.Key)).ToList();
        if (candidates.Count <= 1 || points is null || defects is null) return candidates.FirstOrDefault();
        return candidates.OrderBy(t => Math.Abs(t.Points.Count - points.Value) + Math.Abs(t.Defects.Count - defects.Value)).First();
    }
}

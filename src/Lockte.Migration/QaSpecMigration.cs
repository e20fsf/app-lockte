using System.Text;
using System.Text.RegularExpressions;
using Lockte.Prod;
using Lockte.Prod.Data;

namespace Lockte.Migration;

/// <summary>A label (or other control) of a recovered report definition: where it is and what it says. <see cref="Section"/> counts the sections of the report in file order.</summary>
public sealed record ReportControl(string Kind, int Section, int Left, int Top, int Width, string? Caption);

/// <summary>Reads the text of a recovered report definition (<c>reports_decompiled/*.txt</c>, UTF-16): its controls with their position and caption.</summary>
public static class ReportDefinition
{
    private static readonly HashSet<string> ControlKinds = ["Label", "TextBox", "UnboundObjectFrame", "BoundObjectFrame", "Image", "CommandButton"];
    private static readonly HashSet<string> SectionKinds = ["Section", "PageHeader", "PageFooter", "GroupHeader", "GroupFooter", "ReportHeader", "ReportFooter"];
    private static readonly Regex Property = new(@"^(Left|Top|Width|Caption)\s*=\s*(.*)$", RegexOptions.Compiled);

    public static IReadOnlyList<ReportControl> Read(string path)
        => Parse(File.ReadAllText(path, Encoding.Unicode));

    public static IReadOnlyList<ReportControl> Parse(string text)
    {
        var controls = new List<ReportControl>();
        string? kind = null;
        int left = 0, top = 0, width = 0, section = 0, depth = 0;
        string? caption = null;

        void Emit()
        {
            if (kind is not null) controls.Add(new ReportControl(kind, section, left, top, width, caption));
            kind = null;
            (left, top, width, caption) = (0, 0, 0, null);
        }

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("Begin ", StringComparison.Ordinal) && !line.Contains('='))
            {
                Emit();
                var name = line[6..].Trim();
                if (SectionKinds.Contains(name)) section++;
                else if (ControlKinds.Contains(name)) kind = name;
                continue;
            }
            if (line.EndsWith("= Begin", StringComparison.Ordinal) || line.EndsWith("=Begin", StringComparison.Ordinal)) { depth++; continue; }
            if (line == "End")
            {
                if (depth > 0) depth--;
                else Emit();
                continue;
            }
            if (kind is null || depth > 0) continue;
            if (Property.Match(line) is not { Success: true } m) continue;
            var value = m.Groups[2].Value.Trim();
            switch (m.Groups[1].Value)
            {
                case "Left": int.TryParse(value, out left); break;
                case "Top": int.TryParse(value, out top); break;
                case "Width": int.TryParse(value, out width); break;
                case "Caption": caption = value.Length >= 2 && value[0] == '"' ? value[1..^1].Replace("\"\"", "\"") : value; break;
            }
        }
        Emit();
        return controls;
    }
}

/// <summary>What was read from one inspection sheet.</summary>
public sealed record QaSheet(
    string Name, string Group, string DocumentCode, string? Revision, int? Sequence, string Title, string? SampleUnit, QaResultColumns ResultColumns, string? AttributeRanges, QaPageLayout PageLayout,
    string? Paper, string? Models, IReadOnlyList<QaMeasurePointInput> Points, IReadOnlyList<string> Defects, IReadOnlyList<string> Notes);

/// <summary>
/// สกัดสเปคจากนิยามรายงานใบตรวจ QA (#129): the values the sheets carry in their labels. A sheet is a report with a document number (<c>WI-QA…/ปี ฉบับที่ n</c>); the title is the label on the
/// same line, the result columns follow the captions of the table (แผ่นที่ / คู่ที่ / 1–8), the ranges of the defect columns are the captions like <c>1-10</c>, the measure points and the defects are
/// the labels in the column of ค่าที่กำหนด — the points first, the defects after the first one without a number, up to หมายเหตุ — and the models are the labels under the product boxes.
/// Anything it cannot read, or reads ambiguously, goes in <see cref="QaSheet.Notes"/> for the QA to decide.
/// </summary>
public static class QaSheetReader
{
    private static readonly Regex DocumentNumber = new(@"^(?<base>WI-QA\d+)(?:-(?<seq>\d+))?(?<year>/\d+)?(?:\s+(?<rev>ฉบับที่\s*\S*))?\s*$", RegexOptions.Compiled);
    private static readonly Regex Range = new(@"^\d+\s*-\s*\d+$", RegexOptions.Compiled);
    private static readonly Regex Digit = new(@"\d", RegexOptions.Compiled);
    private static readonly Regex Accepted = new(@"จำนวนที่ยอมรับ.*?\d+\s*([^\s\d(]+)", RegexOptions.Compiled);
    private static readonly string[] FixedPrefixes =
    [
        "ค่าที่กำหนด", "(Specification)", "ผลการตรวจสอบ", "ผ่านการตรวจสอบ", "ไม่ผ่านการตรวจสอบ", "P-NCR", "ผู้ตรวจสอบ", "หัวหน้า", "ผู้จัดการ", "(INSPECTOR)", "Variable Data", "Attribute Data",
        "จำนวน", "LOT NO", "รหัสสินค้า", "ชื่อสินค้า", "วันที่", "LOCKTE", "(",
    ];

    /// <summary>The group of a report by its name: the Thai (or other) text before the first model code — <c>ไดคาสท์029</c> → <c>ไดคาสท์</c>.</summary>
    public static string GroupOf(string reportName)
    {
        var match = Regex.Match(reportName, @"^(.*?)(?=[A-Za-z0-9#]|$)");
        return match.Groups[1].Value.Trim();
    }

    private static bool IsFixed(string caption)
        => FixedPrefixes.Any(p => caption.StartsWith(p, StringComparison.OrdinalIgnoreCase)) || caption.StartsWith("หมายเหตุ", StringComparison.Ordinal);

    /// <summary>Reads a sheet; null when the report carries no document number (it is not an inspection sheet).</summary>
    public static QaSheet? Read(string reportName, IReadOnlyList<ReportControl> controls)
    {
        var notes = new List<string>();
        var labels = controls.Where(c => c.Kind == "Label" && !string.IsNullOrWhiteSpace(c.Caption)).Select(c => c with { Caption = c.Caption!.Trim() })
            .OrderBy(c => c.Section).ThenBy(c => c.Top).ThenBy(c => c.Left).ToList();

        var number = labels.Select(l => (Label: l, Match: DocumentNumber.Match(l.Caption!))).FirstOrDefault(x => x.Match.Success);
        if (number.Label is null) return null;
        var m = number.Match;
        var code = m.Groups["base"].Value + m.Groups["year"].Value;
        int? sequence = m.Groups["seq"].Success ? int.Parse(m.Groups["seq"].Value) : null;
        var revision = m.Groups["rev"].Success ? Regex.Replace(m.Groups["rev"].Value, @"\s+", " ") : null;

        // the title: the label on the line of the number, the leftmost
        var title = labels.Where(l => l != number.Label && l.Section == number.Label.Section && Math.Abs(l.Top - number.Label.Top) <= 60 && !IsFixed(l.Caption!)).OrderBy(l => l.Left).FirstOrDefault()?.Caption;
        if (title is null) { notes.Add("ไม่พบชื่อใบตรวจบรรทัดเดียวกับเลขเอกสาร"); title = reportName; }

        var kind = ResultColumnsOf(labels, notes);
        var ranges = AttributeRangesOf(labels);

        // the column of ค่าที่กำหนด
        var specHeader = labels.FirstOrDefault(l => l.Caption == "ค่าที่กำหนด");
        var firstSpecification = labels.FirstOrDefault(l => l.Caption == "(Specification)");
        var points = new List<QaMeasurePointInput>();
        var defects = new List<string>();
        var overlays = new List<string>();
        if (specHeader is null || firstSpecification is null) notes.Add("ไม่พบหัวคอลัมน์ ค่าที่กำหนด/(Specification) — ไม่ได้อ่านจุดวัดและรายการตำหนิ");
        else
        {
            var column = labels.Where(l => l.Section == firstSpecification.Section && l.Top > firstSpecification.Top && Math.Abs(l.Left - specHeader.Left) <= 250 && !IsFixed(l.Caption!)
                    && !Range.IsMatch(l.Caption!))
                .OrderBy(l => l.Top).ThenBy(l => l.Left).ToList();
            var stop = labels.Where(l => l.Section == firstSpecification.Section && Math.Abs(l.Left - specHeader.Left) <= 250 && l.Caption!.StartsWith("หมายเหตุ", StringComparison.Ordinal)).Select(l => l.Top).DefaultIfEmpty(int.MaxValue).Min();
            var inDefects = false;
            foreach (var label in column.Where(l => l.Top < stop))
            {
                var text = label.Caption!;
                if (!inDefects && !Digit.IsMatch(text)) inDefects = true;
                if (inDefects) defects.Add(text);
                else points.Add(PointOf(text));
            }
            // the little labels laid over the points (+0.3 / -0.5), to the right of the column: kept out of the standard and listed
            var over = labels.Where(l => l.Section == firstSpecification.Section && l.Top > firstSpecification.Top && l.Left > specHeader.Left + 250 && l.Left < specHeader.Left + specHeader.Width
                    && Regex.IsMatch(l.Caption!, @"^[+\-±]\s*\d+([.,]\d+)?$")).Select(l => l.Caption!).ToList();
            if (over.Count > 0) { overlays.AddRange(over); notes.Add($"มีป้ายพิกัดซ้อนบนจุดวัด {over.Count} ป้าย ({string.Join(", ", over.Distinct())}) — ไม่ได้รวมในค่ามาตรฐาน ให้ QA ตรวจ"); }
            if (points.Count == 0) notes.Add("ไม่พบจุดวัด");
        }

        // the models: labels in the left part, under the boxes of the product and above the signatures
        var lot = labels.FirstOrDefault(l => l.Caption!.StartsWith("LOT NO", StringComparison.OrdinalIgnoreCase));
        var signatures = labels.Where(l => l.Caption!.StartsWith("ผลการตรวจสอบ", StringComparison.Ordinal) && (specHeader is null || l.Left < specHeader.Left - 200)).Select(l => l.Top).DefaultIfEmpty(int.MaxValue).Min();
        var models = specHeader is null || lot is null ? [] : labels.Where(l => l.Section == lot.Section && l.Top > lot.Top && l.Top < signatures && l.Left < specHeader.Left - 200 && !IsFixed(l.Caption!)
                && !Regex.IsMatch(l.Caption!, @"^P\.?$") && !l.Caption!.Contains("....")).Select(l => l.Caption!).Distinct().ToList();
        if (models.Count == 0) notes.Add("ไม่พบรุ่น (ข้อความใต้ช่องรหัสสินค้า) — ใช้ชื่อรายงานเป็นชื่อสเปคอย่างเดียว");
        if (controls.Any(c => c.Kind is "UnboundObjectFrame" or "BoundObjectFrame" && c.Top > 0 && c.Width >= 1000)) notes.Add("มีรูปชิ้นงานในรายงาน (OLE) — ไม่ได้ดึงรูป ให้ QA เพิ่มรูปในหน้าสเปค");

        var unit = labels.Select(l => Accepted.Match(l.Caption!)).FirstOrDefault(x => x.Success)?.Groups[1].Value;
        if (unit is null) { unit = labels.Select(l => l.Caption!).FirstOrDefault(c => c is "แผ่น" or "ชิ้น" or "คู่"); if (unit is null) notes.Add("ไม่พบหน่วยสุ่ม"); }

        var group = GroupOf(reportName);
        var layout = reportName.Contains("ฟอร์มเปล่า", StringComparison.Ordinal) ? QaPageLayout.PackingBlankFourStrips
            : reportName.Contains("บรรจุ", StringComparison.Ordinal) || reportName.Contains("สกินแพ็ค", StringComparison.Ordinal) ? QaPageLayout.PackingOneStrip
            : labels.Any(l => l.Caption == "Variable Data") || kind is not QaResultColumns.Pieces8 || points.Count > 0 ? QaPageLayout.Full : QaPageLayout.Short;
        var width = controls.Where(c => c.Kind == "Label").Select(c => c.Left + c.Width).DefaultIfEmpty(0).Max();
        var paper = width > 13000 ? "A4 นอน" : width > 0 ? "A4 ตั้ง" : null;

        return new QaSheet(reportName, group, code, revision, sequence, title, unit, kind, ranges, layout, paper, models.Count == 0 ? null : string.Join(", ", models),
            points, defects, notes);
    }

    private static QaMeasurePointInput PointOf(string caption)
    {
        // "P.1     51.0  mm. / 50.80-51.20 mm." → the number up to the first space, the rest as printed; "ความหนา 3.50 mm. …" → the words up to the first digit
        var first = Regex.Match(caption, @"^(?<no>(?:P\.|จุดที่\s*)\d+)(?<rest>\s+.*)$");
        if (first.Success) return new QaMeasurePointInput(first.Groups["no"].Value.Trim(), null, first.Groups["rest"].Value.TrimEnd()); // the spaces after the number stay: the sheet prints the text as typed
        var digit = Digit.Match(caption);
        if (digit.Success && digit.Index > 0) return new QaMeasurePointInput(null, caption[..digit.Index].Trim(), caption[digit.Index..].Trim());
        return new QaMeasurePointInput(null, null, caption);
    }

    private static QaResultColumns ResultColumnsOf(List<ReportControl> labels, List<string> notes)
    {
        var captions = labels.Select(l => l.Caption!).ToList();
        if (captions.Any(c => c.StartsWith("มีเขา", StringComparison.Ordinal) || c.StartsWith("ไม่มีเขา", StringComparison.Ordinal))) return QaResultColumns.WithAndWithoutHorn;
        if (captions.Any(c => Regex.IsMatch(c, @"^แผ่นที่\s*\d+$"))) return QaResultColumns.Sheets10;
        if (captions.Any(c => Regex.IsMatch(c, @"^คู่ที่\s*\d+$"))) return QaResultColumns.Pairs10;
        var numbers = captions.Where(c => Regex.IsMatch(c, @"^\d{1,2}$")).Select(int.Parse).ToList();
        if (numbers.Count == 0) { notes.Add("ไม่พบคอลัมน์ผลการวัด (Variable) — ใช้ ชิ้นที่ 1–8"); return QaResultColumns.Pieces8; }
        if (numbers.Max() > 8) notes.Add($"คอลัมน์ผลการวัดถึง {numbers.Max()} ไม่ตรงชนิดที่รู้จัก — ใช้ ชิ้นที่ 1–8");
        return QaResultColumns.Pieces8;
    }

    private static string? AttributeRangesOf(List<ReportControl> labels)
    {
        var rows = labels.Where(l => Range.IsMatch(l.Caption!)).GroupBy(l => (l.Section, Top: l.Top / 100)).OrderBy(g => g.Key.Section).ThenBy(g => g.Key.Top)
            .Select(g => string.Join(",", g.OrderBy(l => l.Left).Select(l => Regex.Replace(l.Caption!, @"\s+", "")))).ToList();
        return rows.Count == 0 ? null : string.Join(";", rows);
    }
}

/// <summary>
/// ย้ายสเปคตรวจ QA ~290 ใบ (#129, ข้อตัดสิน #27 #17) from the recovered report definitions into the tables of #128. The folder is read as it is (never changed); what is extracted stays in the database
/// and in the report folder, never in git (the specs are the company's technical secret). A sheet becomes a spec of the document type of its document number (code, edition, title); the sheets
/// of one group are counted against the specs they gave, and everything the reader could not read, or read ambiguously, is a DQ row for the QA to decide. Deletes and loads again only the specs.
/// </summary>
public sealed class QaSpecMigration(QaSpecImport import)
{
    private const string Other = "NF-06";
    private const string Ambiguous = "QA-1";

    public async Task<MigrationReport> RunAsync(string folder, string? outputFolder = null)
    {
        var report = new MigrationReport();
        await import.DeleteAllAsync();

        var files = Directory.GetFiles(folder, "*.txt").OrderBy(f => f, StringComparer.Ordinal).ToList();
        var sheets = new List<QaSheet>();
        var notQa = new List<string>();
        var failed = new Dictionary<string, string>();
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            try
            {
                var controls = ReportDefinition.Read(file);
                if (QaSheetReader.Read(name, controls) is { } sheet) sheets.Add(sheet);
                else notQa.Add(name);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                failed[name] = e.Message;
            }
        }

        var qaGroups = sheets.Select(s => s.Group).ToHashSet(StringComparer.Ordinal);
        var types = new List<QaDocumentTypeImport>();
        var duplicates = new Dictionary<string, int>();
        var notSheets = new Dictionary<string, int>();
        foreach (var byType in sheets.GroupBy(s => (s.DocumentCode, s.Revision, s.Title)).OrderBy(g => g.Key.DocumentCode, StringComparer.Ordinal).ThenBy(g => g.Key.Revision, StringComparer.Ordinal))
        {
            var first = byType.First();
            var names = new HashSet<string>(StringComparer.Ordinal);
            var specs = new List<QaSpecImportRow>();
            foreach (var sheet in byType)
            {
                foreach (var note in sheet.Notes) report.Add(sheet.Name, sheet.DocumentCode, Ambiguous, note, "โหลดเท่าที่อ่านได้");
                if (sheet.ResultColumns != first.ResultColumns || sheet.AttributeRanges != first.AttributeRanges || sheet.SampleUnit != first.SampleUnit)
                    report.Add(sheet.Name, sheet.DocumentCode, Ambiguous, "ชนิดคอลัมน์ผล/ช่วงตำหนิ/หน่วยสุ่ม ต่างจากใบแรกของชนิดเอกสารเดียวกัน — ใช้ค่าของใบแรกเป็นเทมเพลต", "โหลดสเปคตามที่อ่านได้");
                var specName = sheet.Name;
                if (!names.Add(specName)) { report.Add(sheet.Name, sheet.DocumentCode, Other, "ชื่อรายงานซ้ำในชนิดเอกสารเดียวกัน", "ไม่โหลด"); duplicates[sheet.Group] = duplicates.GetValueOrDefault(sheet.Group) + 1; continue; }
                specs.Add(new QaSpecImportRow(specName, sheet.Models, sheet.Sequence, sheet.Points, sheet.Defects));
            }
            types.Add(new QaDocumentTypeImport(first.DocumentCode, first.Revision, first.Title, null, first.SampleUnit, first.ResultColumns, first.AttributeRanges, first.PageLayout, first.Paper, specs));
        }
        await import.AddAsync(types);

        foreach (var name in notQa.Where(n => qaGroups.Contains(QaSheetReader.GroupOf(n))))
        {
            report.Add(name, "-", Other, "ไม่พบเลขเอกสาร WI-QA ในรายงานของกลุ่มใบตรวจ", MigrationReport.NotLoaded);
            var g = QaSheetReader.GroupOf(name);
            notSheets[g] = notSheets.GetValueOrDefault(g) + 1;
        }
        foreach (var (name, message) in failed) report.Add(name, "-", Other, $"อ่านไฟล์ไม่ได้: {message}", MigrationReport.NotLoaded);

        // count back: the sheets of each group, the specs they gave, the specs in the database
        var inDatabase = (await import.GetSpecNamesAsync()).GroupBy(QaSheetReader.GroupOf).ToDictionary(g => g.Key, g => g.Count());
        var perGroup = files.Select(f => Path.GetFileNameWithoutExtension(f)).Where(n => qaGroups.Contains(QaSheetReader.GroupOf(n))).GroupBy(QaSheetReader.GroupOf).OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var group in perGroup)
        {
            var read = sheets.Count(s => s.Group == group.Key);
            var rejectedHere = duplicates.GetValueOrDefault(group.Key) + notSheets.GetValueOrDefault(group.Key) + group.Count(n => failed.ContainsKey(n));
            report.Tables.Add(new TableCount($"รายงาน {group.Key}…", "prod.QaModelSpecs", group.Count(), 0, rejectedHere, read - duplicates.GetValueOrDefault(group.Key), inDatabase.GetValueOrDefault(group.Key)));
        }
        var others = files.Count(f => !qaGroups.Contains(QaSheetReader.GroupOf(Path.GetFileNameWithoutExtension(f))));
        report.Tables.Add(new TableCount("รายงานอื่น ๆ (ไม่ใช่ใบตรวจ QA)", "-", others, others, 0, 0, 0));

        var counts = await import.GetCountsAsync();
        report.Notes.Add($"อ่านนิยามรายงาน {files.Count} ไฟล์: ใบตรวจ QA {sheets.Count} ใบ, ไม่ใช่ใบตรวจ {others} ไฟล์ · ได้ชนิดเอกสาร {counts.Types} ชนิด, สเปค {counts.Specs}, จุดวัด {counts.Points}, รายการตำหนิ {counts.Defects}");
        report.Notes.Add($"ใบตรวจที่มีข้อสังเกตให้ QA ตรวจ {report.Exceptions.Select(e => e.Table).Distinct().Count()} ใบ ({report.Exceptions.Count} รายการ) — รายการอยู่ใน dq-exceptions.csv · รูปชิ้นงานไม่ได้ดึง ต้องเพิ่มในหน้าสเปค");
        if (outputFolder is not null) report.WriteTo(outputFolder);
        return report;
    }
}

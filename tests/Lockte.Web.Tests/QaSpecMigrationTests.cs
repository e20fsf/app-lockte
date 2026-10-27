using System.Text;
using Lockte.Migration;
using Lockte.Prod;
using Lockte.Prod.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Tests;

/// <summary>
/// เครื่องมือย้ายข้อมูล: สกัดสเปคตรวจ QA จากนิยามรายงานที่กู้ไว้ (#129). The report definitions here are made up in the format of the recovered ones (UTF-16 text with the controls and their
/// captions) — the real specs of the company stay out of the repo.
/// </summary>
[Collection(MigrationDbCollection.Name)]
public sealed class QaSpecMigrationTests(MigrationDbFixture db) : IDisposable
{
    private readonly DirectoryInfo work = Directory.CreateTempSubdirectory("lockte-qa-spec-test-");

    public void Dispose() => work.Delete(recursive: true);

    private string Folder => Directory.CreateDirectory(Path.Combine(work.FullName, "reports")).FullName;

    private async Task<MigrationReport> RunAsync()
    {
        await using var scope = db.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<QaSpecMigration>().RunAsync(Folder, Path.Combine(work.FullName, "out"));
    }

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("report1");
        return await action(scope);
    }

    /// <summary>A report definition in the format of the recovered files: a section with labels, each with its place and caption, and the hex blobs Access writes after them.</summary>
    private sealed class FakeReport
    {
        private readonly StringBuilder text = new("Version =21\r\nBegin Report\r\n    Width =15712\r\n    Begin Section\r\n        Name =\"Detail\"\r\n        Begin\r\n");

        public FakeReport Label(int left, int top, int width, string caption)
        {
            text.Append($"            Begin Label\r\n                Left ={left}\r\n                Top ={top}\r\n                Width ={width}\r\n                Height =453\r\n                Name =\"Label{left}_{top}\"\r\n");
            text.Append($"                Caption =\"{caption.Replace("\"", "\"\"")}\"\r\n                GUID = Begin\r\n                    0x2e4cb64e1c1b7043bcb6860fd16f46f3\r\n                End\r\n            End\r\n");
            return this;
        }

        public FakeReport TextBox(int left, int top, int width)
        {
            text.Append($"            Begin TextBox\r\n                Left ={left}\r\n                Top ={top}\r\n                Width ={width}\r\n                Name =\"Text{left}_{top}\"\r\n                ControlSource =\"PD_C\"\r\n            End\r\n");
            return this;
        }

        public FakeReport Picture(int left, int top, int width)
        {
            text.Append($"            Begin UnboundObjectFrame\r\n                Left ={left}\r\n                Top ={top}\r\n                Width ={width}\r\n                Name =\"Object{left}_{top}\"\r\n            End\r\n");
            return this;
        }

        public void Save(string folder, string name)
            => File.WriteAllText(Path.Combine(folder, name + ".txt"), text + "        End\r\n    End\r\nEnd\r\n", new UnicodeEncoding(bigEndian: false, byteOrderMark: true));
    }

    /// <summary>A sheet with the table of sheets 1–10 (Variable) and the table of defects (Attribute) under it, as the sheets of the clutch plates.</summary>
    private FakeReport Plates(string code, string title, string model, string[] points, string[] defects)
    {
        var report = new FakeReport()
            .Label(0, -3, 15696, "LOCKTE CO.,LTD.").Label(0, 396, 15696, title).Label(13426, 396, 2271, code)
            .Label(0, 850, 4101, "รหัสสินค้า.....................").Label(4081, 850, 11631, "Variable Data").TextBox(1133, 850, 2961)
            .Label(4081, 1700, 6516, "จำนวนที่ยอมรับ          0          แผ่น")
            .Label(4081, 2097, 4716, "ค่าที่กำหนด").Label(8852, 2097, 6801, "ผลการตรวจสอบ").Label(4081, 2551, 4716, "(Specification)")
            .Label(0, 2437, 4146, "LOT NO..........................................................................");
        for (var i = 1; i <= 10; i++) report.Label(8824 + (i - 1) * 690, 2551, 690, $"แผ่นที่ {i}");
        var top = 2948;
        foreach (var point in points) { report.Label(4081, top, 5961, point); top += 340; }
        report.Label(0, 3288, 4081, model).Picture(5609, 3061, 121).Picture(453, 3968, 3571)
            .Label(4081, 3344, 11631, "Attribute Data").Label(4081, 3798, 4371, "จำนวนสุ่ม................................แผ่น")
            .Label(4081, 4195, 3861, "ค่าที่กำหนด").Label(7933, 4195, 7776, "ผลการตรวจสอบ").Label(4081, 4592, 3861, "(Specification)")
            .Label(7937, 4591, 567, "1-10").Label(8503, 4591, 567, "11-20").Label(9069, 4591, 567, "21-30");
        top = 4988;
        foreach (var defect in defects) { report.Label(4081, top, 3861, defect); top += 340; }
        return report.Label(4081, top, 3861, "หมายเหตุ").Label(0, 9524, 4101, "ผลการตรวจสอบ").Label(4081, 9524, 2841, "ผู้ตรวจสอบ");
    }

    /// <summary>A sheet with the columns 1–8 and the points laid out "P.n value", with the tolerance labels over them, as the sheets of the die casting.</summary>
    private FakeReport Casting(string code, string title, string model)
    {
        var report = new FakeReport()
            .Label(0, -3, 15696, "LOCKTE CO.,LTD.").Label(0, 396, 15696, title).Label(13426, 396, 2271, code)
            .Label(0, 850, 4101, "รหัสสินค้า.....................").Label(6301, 1247, 2076, "จำนวนที่ยอมรับ      0    ชิ้น")
            .Label(4081, 1644, 5391, "ค่าที่กำหนด").Label(8893, 1644, 6816, "ผลการตรวจสอบ").Label(4081, 2041, 5391, "(Specification)")
            .Label(0, 2437, 4146, "LOT NO..........................................................................");
        for (var i = 1; i <= 8; i++) report.Label(8900 + (i - 1) * 851, 2040, 851, i.ToString());
        return report.Label(5102, 2381, 447, "-0.5").Label(4081, 2438, 5391, "P.1     51.0     0.2  mm.  /  50.80-51.20  mm.").Label(5102, 2551, 447, "+0.3")
            .Label(4081, 2835, 5391, "P.2     15.0     0.2  mm.  /  14.80-15.20  mm.").Label(1304, 3856, 1134, model)
            .Label(4081, 4818, 3861, "ขาเหล็ก ไม่ตรง").Label(4081, 5215, 3861, "ผิวชิ้นงานเป็นรอย").Label(4081, 5612, 3861, "หมายเหตุ")
            .Label(0, 9524, 4101, "ผลการตรวจสอบ");
    }

    private void Standard()
    {
        Plates("WI-QA999-12/2547 ฉบับที่ 2", "รายงานตรวจแผ่นสมมุติ", "FAKE-A", ["ความหนา 3.50 mm.    0.15 mm./3.35-3.65 mm.", "กว้าง 20.0 mm."], ["แตก", "รอยบิ่น"]).Save(Folder, "ทดสอบแผ่นA1");
        Plates("WI-QA999-13/2547 ฉบับที่ 2", "รายงานตรวจแผ่นสมมุติ", "FAKE-B", ["ความหนา 4.00 mm.  0.10 mm./3.90-4.10 mm."], ["แตก"]).Save(Folder, "ทดสอบแผ่นA2");
        Casting("WI-QA998-3/2551 ฉบับที่ 1", "รายงานตรวจชิ้นงานสมมุติ", "029").Save(Folder, "ทดสอบฉีด029");
        new FakeReport().Label(0, 0, 4000, "ไม่ใช่ใบตรวจ").Save(Folder, "ทดสอบแผ่นZZ");   // in the group of a sheet, without a document number
        new FakeReport().Label(0, 0, 4000, "รายงานสรุปอื่น").Save(Folder, "สรุปอื่น1");      // another group altogether
    }

    private static TableCount Group(MigrationReport report, string group) => report.Tables.Single(t => t.LegacyTable.StartsWith($"รายงาน {group}"));

    [Fact]
    public async Task Reads_the_document_numbers_and_makes_a_type_of_each_code_edition_and_title()
    {
        Standard();

        var report = await RunAsync();

        Assert.True(report.Tables.All(t => t.Balanced), string.Join("; ", report.Tables.Where(t => !t.Balanced)));
        var types = await AsAsync(s => s.Get<QaSpecDirectory>().GetTypesAsync());
        Assert.Equal(2, types.Count);
        var plates = types.Single(t => t.Code == "WI-QA999/2547");
        Assert.Equal(("ฉบับที่ 2", "รายงานตรวจแผ่นสมมุติ", "แผ่น", QaResultColumns.Sheets10, "1-10,11-20,21-30", 2), (plates.Revision, plates.Name, plates.SampleUnit, plates.ResultColumns, plates.AttributeRanges, plates.Specs));
        var casting = types.Single(t => t.Code == "WI-QA998/2551");
        Assert.Equal((QaResultColumns.Pieces8, "ชิ้น", 1, "A4 นอน"), (casting.ResultColumns, casting.SampleUnit, casting.Specs, casting.Paper));
    }

    [Fact] // the sequence of the model in the document number is the spec's, and the points and defects are read in the order they stand
    public async Task Reads_the_points_defects_models_and_sequence_of_a_sheet()
    {
        Standard();
        await RunAsync();

        var summary = (await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecsAsync())).Single(s => s.Name == "ทดสอบแผ่นA1");
        var spec = (await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecAsync(summary.Id)))!;

        Assert.Equal((12, "FAKE-A"), (spec.DocumentSequence, spec.Models));
        Assert.Equal(2, spec.Points.Count);
        Assert.Equal(("ความหนา", "3.50 mm.    0.15 mm./3.35-3.65 mm."), (spec.Points[0].Description, spec.Points[0].Standard));
        Assert.Equal((null, "กว้าง", "20.0 mm."), (spec.Points[1].PointNo, spec.Points[1].Description, spec.Points[1].Standard));
        Assert.Equal(["แตก", "รอยบิ่น"], spec.Defects);
    }

    [Fact] // "P.1 value" gives the point number; the little labels over the points are listed, not mixed into the standard
    public async Task Reads_points_with_numbers_and_lists_the_labels_laid_over_them()
    {
        Standard();
        var report = await RunAsync();

        var summary = (await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecsAsync())).Single(s => s.Name == "ทดสอบฉีด029");
        var spec = (await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecAsync(summary.Id)))!;

        Assert.Equal(("P.1", "51.0     0.2  mm.  /  50.80-51.20  mm."), (spec.Points[0].PointNo, spec.Points[0].Standard));
        Assert.Equal(["ขาเหล็ก ไม่ตรง", "ผิวชิ้นงานเป็นรอย"], spec.Defects);
        Assert.Equal((3, "029"), (spec.DocumentSequence, spec.Models));
        Assert.Contains(report.Exceptions, e => e.Table == "ทดสอบฉีด029" && e.Problem.Contains("ป้ายพิกัดซ้อน") && e.Problem.Contains("+0.3"));
    }

    [Fact] // everything it cannot take is listed for the QA: the pictures, a sheet of a group without a document number
    public async Task Lists_what_it_could_not_read()
    {
        Standard();

        var report = await RunAsync();

        Assert.Contains(report.Exceptions, e => e.Table == "ทดสอบแผ่นA1" && e.Problem.Contains("รูปชิ้นงาน"));
        Assert.Contains(report.Exceptions, e => e.Table == "ทดสอบแผ่นZZ" && e.Problem.Contains("ไม่พบเลขเอกสาร"));
        var group = Group(report, "ทดสอบแผ่น");
        Assert.Equal((3, 1, 2, 2), (group.SourceRows, group.Rejected, group.Loaded, group.InDatabase));
        var others = report.Tables.Single(t => t.LegacyTable.StartsWith("รายงานอื่น ๆ"));
        Assert.Equal((1, 1), (others.SourceRows, others.Excluded));
    }

    [Fact] // a sheet with no table of specs is still a spec (the head is read), and says what is missing
    public async Task A_sheet_without_the_table_is_kept_with_a_note()
    {
        new FakeReport().Label(0, 396, 15696, "ใบตรวจเปล่า").Label(13426, 396, 2271, "WI-QA997/2552 ฉบับที่ 1").Save(Folder, "ทดสอบเปล่า1");

        var report = await RunAsync();

        Assert.Contains(report.Exceptions, e => e.Table == "ทดสอบเปล่า1" && e.Problem.Contains("ไม่พบหัวคอลัมน์"));
        var types = await AsAsync(s => s.Get<QaSpecDirectory>().GetTypesAsync());
        Assert.Contains(types, t => t.Code == "WI-QA997/2552" && t.Revision == "ฉบับที่ 1" && t.Name == "ใบตรวจเปล่า");
    }

    [Fact] // run again: the same specs, once; a spec the QA added is deleted with the rest (the tool loads only this part, from the files)
    public async Task Runs_again_with_the_same_result()
    {
        Standard();
        await RunAsync();

        var report = await RunAsync();

        Assert.True(report.Tables.All(t => t.Balanced));
        Assert.Equal(3, (await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecsAsync())).Count);
        Assert.Equal(2, (await AsAsync(s => s.Get<QaSpecDirectory>().GetTypesAsync())).Count);
    }

    [Fact] // the reader on its own: captions with quotes, nested blobs and controls other than labels
    public void The_reader_takes_the_place_and_caption_of_each_label()
    {
        var controls = ReportDefinition.Parse("Begin Label\r\n Left =100\r\n Top =200\r\n Width =300\r\n Caption =\"a \"\"b\"\" c\"\r\n GUID = Begin\r\n  0x01\r\n End\r\nEnd\r\nBegin TextBox\r\n Left =5\r\nEnd\r\n");

        Assert.Equal(2, controls.Count);
        Assert.Equal(("Label", 100, 200, 300, "a \"b\" c"), (controls[0].Kind, controls[0].Left, controls[0].Top, controls[0].Width, controls[0].Caption));
        Assert.Equal(("TextBox", 5, (string?)null), (controls[1].Kind, controls[1].Left, controls[1].Caption));
    }
}

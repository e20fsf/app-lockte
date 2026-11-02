using Lockte.Calib;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;
using Lockte.Web.Reports;

namespace Lockte.Web.Tests;

/// <summary>The starting data of the inspection sheets against reports.md RPT-05/06 and print-samples.md (#199), without a database.</summary>
public sealed class JigInspectionSeedTests
{
    private static IReadOnlyList<JigSheetSeed> Of(JigSheetType type) => JigInspectionSeed.Sheets.Where(s => s.Type == type).ToList();

    private static string Spec(JigSheetSeed s) => string.Join(", ", s.Points.Select(p => $"{p.Label} {p.Kind} {p.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}{(p.Tolerance == JigToleranceKind.GreaterThan ? ">" : "±")}"));

    [Fact]
    public void There_are_23_zero_check_sheets_and_19_grinding_sheets()
    {
        Assert.Equal(23, Of(JigSheetType.ZeroCheck).Count);
        Assert.Equal(19, Of(JigSheetType.Grinding).Count);
        Assert.Equal(Of(JigSheetType.ZeroCheck).Count, Of(JigSheetType.ZeroCheck).Select(s => s.ModelKey).Distinct().Count());
        Assert.Equal(JigInspectionSeed.Sheets.Count, JigInspectionSeed.Sheets.Select(s => s.DocumentKey).Distinct().Count());
    }

    [Fact]
    public void Point_counts_follow_the_samples()
    {
        var zero = Of(JigSheetType.ZeroCheck).ToDictionary(s => s.ModelKey);
        Assert.Equal(9, zero["RX100"].Points.Count);
        Assert.Single(zero["PIN GAUGE"].Points);
        Assert.Equal(2, zero["TAPER GAUGE"].Points.Count);
        Assert.Equal(20, zero.Values.Count(s => s.Points.Count == 6));
        Assert.All(Of(JigSheetType.Grinding), s => Assert.Equal(3, s.Points.Count));
    }

    [Fact] // print-samples: P1–P3 Diameter, P4 Wide, P5–P6 Dimension; every ± point is ± 0.20 mm.
    public void A_six_point_sheet_has_the_kinds_and_values_of_the_print_sample()
    {
        var click = Of(JigSheetType.ZeroCheck).Single(s => s.ModelKey == "CLICK");
        Assert.Equal("CLICK", click.Title);
        Assert.Equal("P1 Diameter 20.00±, P2 Diameter 9.00±, P3 Diameter 20.00±, P4 Wide 8.00±, P5 Dimension 72.00±, P6 Dimension 66.50±", Spec(click));
        Assert.All(click.Points, p => Assert.Equal((JigToleranceKind.PlusMinus, (decimal?)0.20m), (p.Tolerance, p.ToleranceValue)));
    }

    [Fact]
    public void The_exceptions_of_the_print_samples_are_kept()
    {
        var zero = Of(JigSheetType.ZeroCheck).ToDictionary(s => s.ModelKey);
        Assert.Equal("P1 Diameter 20.00±, P2 Diameter 7.00±, P2.1 Diameter 10.00±, P2.2 Diameter 10.00±, P2.3 Wide 4.00±, P3 Wide 16.00±, P4 Wide 6.00±, P5 Dimension 61.00±, P6 Dimension 58.00±", Spec(zero["RX100"]));
        Assert.Equal("RX100,MATE U,BELL,087,200", zero["RX100"].Title);
        Assert.Equal("P1 Diameter 14.00±, P2 Length 8.00±, P3 Length 21.00±, P4 Wide 8.00±, P5 Length 69.60±, P6 Length 73.90±", Spec(zero["WING"]));
        Assert.Equal("P1 Diameter 11.50±", Spec(zero["PIN GAUGE"]));
        Assert.Equal("P1 Diameter 47.00±, P2 Diameter 50.00±", Spec(zero["TAPER GAUGE"])); // as printed; the register says 46.5 / 50.5 (Q-15)
    }

    [Fact] // RPT-06: P1 Diameter, P2 Length, P3 Wide; `>15.00` has no ±; K125's P2 is 9.00 ±
    public void Grinding_sheets_have_three_points_and_a_greater_than_length()
    {
        var grind = Of(JigSheetType.Grinding).ToDictionary(s => s.ModelKey);
        Assert.Equal("P1 Diameter 10.00±, P2 Length 15.00>, P3 Wide 6.00±", Spec(grind["RX100"]));
        Assert.Equal("P1 Diameter 12.00±, P2 Length 9.00±, P3 Wide 9.00±", Spec(grind["K125"]));
        Assert.Equal("P1 Diameter 10.00±, P2 Length 13.00>, P3 Wide 6.00±", Spec(grind["143"]));
        Assert.Equal("P1 Diameter 8.00±, P2 Length 15.00>, P3 Wide 8.00±", Spec(grind["WING"]));
        Assert.Null(grind["V100"].Points[1].ToleranceValue);
        Assert.Equal("NH50,SS50,029", grind["NH50"].Title);
        Assert.DoesNotContain("PIN GAUGE", grind.Keys);
        Assert.DoesNotContain("068", grind.Keys); // no grinding sheet for 068, 168, PIN and TAPER
    }

    [Fact] // WI-QA049-1…23/2550 and 24…56/2560, ฉบับที่ 2
    public void Document_numbers_are_the_printed_ones()
    {
        var docs = JigInspectionSeed.Documents.ToDictionary(d => d.Key);
        Assert.Equal(42, docs.Count);
        Assert.Equal(("WI-QA049-4/2550", " ฉบับที่ 2"), (docs["JIG-Z-4"].Code, docs["JIG-Z-4"].Edition)); // CLICK, AC-14
        Assert.Equal("WI-QA049-23/2550", docs["JIG-Z-23"].Code);
        Assert.Equal("WI-QA049-35/2560", docs["JIG-G-35"].Code); // K125 grinding, AC-14
        Assert.Equal("WI-QA049-56/2560", docs["JIG-G-56"].Code);
        Assert.Equal(Enumerable.Range(1, 23).Select(n => $"JIG-Z-{n}"), JigInspectionSeed.Sheets.Where(s => s.Type == JigSheetType.ZeroCheck).OrderBy(s => s.DocumentNo).Select(s => s.DocumentKey));
        Assert.All(DocumentKeys.All.Where(k => k.StartsWith("JIG-", StringComparison.Ordinal)), k => Assert.Contains(k, docs.Keys));
    }
}

/// <summary>แบบตรวจ JIG in the database (#199): loaded by the migration, edited by QA with history.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class JigSheetTests(LocalDbFixture db)
{
    private async Task<T> AsQaAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    private Task<JigSheetDto> SheetAsync(JigSheetType type, string model) => AsQaAsync(async s => (await s.Get<JigSheetDirectory>().FindAsync(type, model))!);

    private static JigSheetInput Edit(JigSheetDto sheet, string? title = null, Func<IReadOnlyList<JigPointInput>, IReadOnlyList<JigPointInput>>? points = null)
    {
        IReadOnlyList<JigPointInput> current = sheet.Points.Select(p => new JigPointInput(p.Id, p.Label, p.Kind, p.Value, p.Tolerance, p.ToleranceValue, p.RowVersion)).ToList();
        return new JigSheetInput(sheet.Id, title ?? sheet.Title, points?.Invoke(current) ?? current, sheet.RowVersion);
    }

    [Fact]
    public async Task The_migration_loaded_all_sheets_and_their_document_numbers()
    {
        var sheets = await AsQaAsync(s => s.Get<JigSheetDirectory>().ListAsync());
        Assert.Equal((23, 19), (sheets.Count(s => s.Type == JigSheetType.ZeroCheck), sheets.Count(s => s.Type == JigSheetType.Grinding)));
        foreach (var seed in JigInspectionSeed.Sheets)
        {
            var sheet = sheets.Single(s => s.Type == seed.Type && s.ModelKey == seed.ModelKey);
            Assert.Equal(seed.Points.Select(p => (p.Label, p.Kind, p.Value, p.Tolerance, p.ToleranceValue)),
                sheet.Points.Select(p => (p.Label, p.Kind, p.Value, p.Tolerance, p.ToleranceValue)));
            Assert.Equal((seed.Title, seed.DocumentKey), (sheet.Title, sheet.DocumentKey));
        }
        Assert.Equal("WI-QA049-4/2550 ฉบับที่ 2", await AsQaAsync(s => s.Get<DocumentNumberService>().PrintedTextAsync("JIG-Z-4")));
        Assert.Equal("WI-QA049-35/2560 ฉบับที่ 2", await AsQaAsync(s => s.Get<DocumentNumberService>().PrintedTextAsync("JIG-G-35")));
    }

    [Fact]
    public async Task Changing_a_value_is_read_back_and_history_has_old_and_new()
    {
        var sheet = await SheetAsync(JigSheetType.ZeroCheck, "JOG");
        var p2 = sheet.Points[1];
        var changed = p2.Value == 8.10m ? 8.00m : 8.10m;
        Assert.True((await AsQaAsync(s => s.Get<JigSheetRegistry>().SaveAsync(Edit(sheet, points: ps => ps.Select(p => p.Id == p2.Id ? p with { Value = changed } : p).ToList())))).Succeeded);

        var back = await SheetAsync(JigSheetType.ZeroCheck, "JOG");
        Assert.Equal(changed, back.Points[1].Value);
        await using var read = db.Scope();
        var rows = await read.Get<HistoryService>().SearchAsync(new(Table: "จุดวัดแบบตรวจ JIG", RowKey: "JOG P2"));
        var row = rows.Where(r => r.Action == HistoryActions.Modified && r.Column == "ค่าที่กำหนด (mm)" && r.UserName == "qa1").OrderByDescending(r => r.Id).First();
        Assert.Equal(changed, decimal.Parse(row.NewValue!, System.Globalization.CultureInfo.InvariantCulture));

        // put it back: the sheets are shared by the tests of this run
        var restored = await SheetAsync(JigSheetType.ZeroCheck, "JOG");
        Assert.True((await AsQaAsync(s => s.Get<JigSheetRegistry>().SaveAsync(Edit(restored, points: ps => ps.Select(p => p.Id == p2.Id ? p with { Value = p2.Value } : p).ToList())))).Succeeded);
    }

    [Fact]
    public async Task Points_can_be_added_reordered_and_deleted_and_the_title_changed()
    {
        var sheet = await SheetAsync(JigSheetType.Grinding, "TACT");
        var newTitle = "TACT " + Guid.NewGuid().ToString("N")[..4];
        var result = await AsQaAsync(s => s.Get<JigSheetRegistry>().SaveAsync(Edit(sheet, newTitle, ps =>
            [ps[2], ps[0], new JigPointInput(null, "P9", JigPointKind.Dimension, 1.5m, JigToleranceKind.PlusMinus, 0.2m, null)])));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));

        var back = await SheetAsync(JigSheetType.Grinding, "TACT");
        Assert.Equal(newTitle, back.Title);
        Assert.Equal(["P3", "P1", "P9"], back.Points.Select(p => p.Label)); // P2 deleted, order as saved

        // put it back for the other tests of this run
        var seed = JigInspectionSeed.Sheets.Single(s => s.Type == JigSheetType.Grinding && s.ModelKey == "TACT");
        Assert.True((await AsQaAsync(s => s.Get<JigSheetRegistry>().SaveAsync(new JigSheetInput(back.Id, seed.Title,
            seed.Points.Select(p => new JigPointInput(null, p.Label, p.Kind, p.Value, p.Tolerance, p.ToleranceValue, null)).ToList(), back.RowVersion)))).Succeeded);
    }

    [Fact]
    public async Task Bad_input_is_refused_and_a_stale_edit_is_a_conflict()
    {
        var sheet = await SheetAsync(JigSheetType.ZeroCheck, "233");
        var bad = await AsQaAsync(s => s.Get<JigSheetRegistry>().SaveAsync(Edit(sheet, " ", ps => [ps[0], ps[0] with { Id = null, RowVersion = null }])));
        Assert.Contains("กรุณากรอกชื่อ JIG บนแบบ", bad.Errors);
        Assert.Contains(bad.Errors, e => e.Contains("ซ้ำ"));
        Assert.Contains("แบบตรวจต้องมีจุดวัดอย่างน้อย 1 จุด", (await AsQaAsync(s => s.Get<JigSheetRegistry>().SaveAsync(Edit(sheet, points: _ => [])))).Errors);

        var title = "233 " + Guid.NewGuid().ToString("N")[..4];
        Assert.True((await AsQaAsync(s => s.Get<JigSheetRegistry>().SaveAsync(Edit(sheet, title)))).Succeeded);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => AsQaAsync(s => s.Get<JigSheetRegistry>().SaveAsync(Edit(sheet, "จากหน้าเก่า"))));
        var seed = JigInspectionSeed.Sheets.Single(s => s.Type == JigSheetType.ZeroCheck && s.ModelKey == "233");
        var current = await SheetAsync(JigSheetType.ZeroCheck, "233");
        Assert.True((await AsQaAsync(s => s.Get<JigSheetRegistry>().SaveAsync(Edit(current, seed.Title)))).Succeeded);
    }
}

using Lockte.Calib;
using Lockte.Web.Reports.Calib;

namespace Lockte.Web.Tests;

/// <summary>RPT-04 สติกเกอร์ (#201) without a database: one label to a page, the text of the sample, dates in the Buddhist year.</summary>
public sealed class StickerFormTests
{
    private static readonly DateOnly Cal = new(2026, 8, 17);
    private static readonly DateOnly Due = new(2027, 8, 17);

    [Fact] // the sample: ID : PG-DC125/2569 · CAL: 17/8/2569 · DUE: 17/8/2570
    public void A_label_has_the_code_and_the_two_dates_in_the_buddhist_year()
    {
        var pages = StickerForm.Layout([new StickerLabel("PG-DC125/2569", Cal, Due)], 40, 21);
        var svg = Assert.Single(pages).ToSvg();
        foreach (var text in new[] { ">ID :<", ">PG-DC125/2569<", ">CAL:<", ">17/8/2569<", ">DUE:<", ">17/8/2570<" }) Assert.Contains(text, svg);
        Assert.Contains("font-weight=\"bold\"", svg);
    }

    [Fact]
    public void Every_label_is_a_page_and_all_share_the_dates_of_the_print()
    {
        var labels = Enumerable.Range(1, 13).Select(n => new StickerLabel($"VN-QA{n:000}/2569", Cal, Due)).ToList();
        var pages = StickerForm.Layout(labels, 40, 21);
        Assert.Equal(13, pages.Count);
        Assert.All(pages, p => Assert.Equal(2, p.ToSvg().Split(">17/8/2569<").Length + p.ToSvg().Split(">17/8/2570<").Length - 2));
    }

    [Fact] // the frame is the size of the setting (40 × 21 mm from the sample), at the top-left corner, 0.5 mm line
    public void The_frame_follows_the_size_and_sits_at_the_top_left()
    {
        var svg = StickerForm.Layout([new StickerLabel("X", Cal, Due)], 34, 17).Single().ToSvg();
        Assert.Contains("<rect x=\"10.25\" y=\"10.25\" width=\"33.5\" height=\"16.5\"", svg);
    }

    [Fact] // DUE is typed, not worked out and not checked against CAL (BR-010 ตัวอย่าง 6)
    public void A_due_date_before_the_calibration_date_prints_as_typed()
        => Assert.Contains(">16/8/2569<", StickerForm.Layout([new StickerLabel("X", Cal, new DateOnly(2026, 8, 16))], 40, 21).Single().ToSvg());

    [Fact]
    public void A_long_code_is_squeezed_into_the_frame_not_cut()
        => Assert.Contains("data-fit=", StickerForm.Layout([new StickerLabel("JG-AS001-2/2560-LONG-LONG-LONG", Cal, Due)], 40, 21).Single().ToSvg());
}

/// <summary>Which instruments a sticker print takes (BR-010, AC-09/10/11) with fake data of the real shapes.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class StickerDataTests(LocalDbFixture db)
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private async Task<T> AsQaAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    private async Task AddAsync(InstrumentKind kind, string code)
        => Assert.True((await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(null, kind, code, null)))).Succeeded);

    private Task<IReadOnlyList<InstrumentDto>> FindAsync(InstrumentKind kind, string pattern) => AsQaAsync(s => s.Get<InstrumentDirectory>().FindByPatternAsync(kind, pattern));

    [Fact] // AC-09: CALIPER, * → every instrument of the kind
    public async Task A_star_takes_every_instrument_of_the_kind_in_order_of_code()
    {
        var tag = Tag();
        foreach (var n in Enumerable.Range(1, 13)) await AddAsync(InstrumentKind.Caliper, $"VN-{tag}{n:000}/2569");
        await AddAsync(InstrumentKind.Scale, $"VN-{tag}999/2569"); // another kind: not in a caliper print
        var found = await FindAsync(InstrumentKind.Caliper, $"VN-{tag}*");
        Assert.Equal(13, found.Count);
        Assert.Equal(found.Select(i => i.Code).Order(StringComparer.Ordinal), found.Select(i => i.Code)); // ordered so the result is the same each time
    }

    [Fact] // AC-10 and BR-010 #2: PG-DC* → 35, one exact code → 1
    public async Task A_prefix_takes_its_group_and_an_exact_code_takes_one()
    {
        var tag = Tag();
        foreach (var n in Enumerable.Range(1, 35)) await AddAsync(InstrumentKind.PressureGauge, $"PG-DC{tag}{n:000}/2569");
        foreach (var n in Enumerable.Range(1, 5)) await AddAsync(InstrumentKind.PressureGauge, $"PG-BL{tag}{n:000}/2569");
        Assert.Equal(35, (await FindAsync(InstrumentKind.PressureGauge, $"PG-DC{tag}*")).Count);
        Assert.Single(await FindAsync(InstrumentKind.PressureGauge, $"PG-DC{tag}001/2569"));
        Assert.Equal(40, (await FindAsync(InstrumentKind.PressureGauge, $"PG-??{tag}###/2569")).Count);
    }

    [Fact] // AC-11: nothing matches → no sticker (the screen says "ไม่พบเครื่องมือวัด")
    public async Task A_pattern_that_matches_nothing_gives_no_sticker()
    {
        Assert.Empty(await FindAsync(InstrumentKind.Caliper, "XX-999"));
        Assert.Empty(await FindAsync(InstrumentKind.Caliper, ""));
    }
}

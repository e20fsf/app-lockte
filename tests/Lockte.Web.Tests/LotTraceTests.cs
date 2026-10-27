using Lockte.Org;
using Lockte.Prod;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Tests;

/// <summary>เปรียบเทียบ Lot การผลิต (RPT-17, #127): the chains of the legacy queries over the production lines, with made-up Lots. Each test works in a year of its own.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class LotTraceTests(LocalDbFixture db)
{
    private static int year = 2800;
    private static int counter = Random.Shared.Next(10000, 60000);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static DateOnly NewMonth() => new(Interlocked.Increment(ref year), 2, 1);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    private async Task<int> MachineAsync(char prefix)
    {
        var department = await AsAsync(async s =>
        {
            var org = s.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ผลิต"), 1, null))).Id!.Value;
            return (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 1, section, null, [], null))).Id!.Value;
        });
        var saved = await AsAsync(s => s.Get<OrgRegistry>().SaveMachineAsync(new MachineInput(null, prefix + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(), "เครื่องตรวจย้อนกลับ", department, null)));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return saved.Id!.Value;
    }

    private async Task<int> ProductAsync(string prefix)
    {
        var saved = await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique(prefix), "สินค้าตรวจย้อนกลับ", null, Unit: "ชิ้น")));
        return saved.Id!.Value;
    }

    private async Task LineAsync(string department, int machine, DateOnly date, int product, string? lot, string? material1, string? material2 = null, decimal good = 100, decimal? reject = 5)
    {
        var no = department + Interlocked.Increment(ref counter).ToString("00000");
        var saved = await AsAsync(s => s.Get<ProductionReportRegistry>().SaveAsync(new ProductionReportInput(null, null, department, date, no, machine,
            [new ProductionReportLineInput(null, null, product, null, null, null, 8m, material1, material2, good, reject, null, null, lot)]), ProductionSaveMode.Save));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
    }

    private Task<LotTraceReport?> BuildAsync(string kind, DateOnly month)
        => AsAsync(s => LotTraceReports.BuildAsync(s.Get<ProductionReportDirectory>(), s.Get<OrgDirectory>(), kind, month, month.AddMonths(1).AddDays(-1)));

    [Fact] // ไดคาสท์ → ตกแต่ง: the die-cast Lot is matched with the Lot of raw material 1 of the trim; a Lot nobody received is kept, blank; two receivers are two rows
    public async Task A_die_cast_lot_is_followed_into_the_trim_department()
    {
        var month = NewMonth();
        var (die, trim) = (await MachineAsync('D'), await MachineAsync('T'));
        var (diePart, trimPart) = (await ProductAsync("SD"), await ProductAsync("ST"));
        var (lot1, lot2, lot3) = (Unique("L"), Unique("L"), Unique("L"));
        await LineAsync("D", die, month.AddDays(1), diePart, lot1, "AL-1");
        await LineAsync("D", die, month.AddDays(2), diePart, lot2, "AL-2");
        await LineAsync("D", die, month.AddDays(3), diePart, lot3, "AL-3");
        await LineAsync("T", trim, month.AddDays(5), trimPart, Unique("T"), lot1);
        await LineAsync("T", trim, month.AddDays(6), trimPart, Unique("T"), lot3); // two trim lines took the Lot of the third
        await LineAsync("T", trim, month.AddDays(7), trimPart, Unique("T"), lot3);

        var report = await BuildAsync("die-trim", month);

        Assert.NotNull(report);
        Assert.Equal(["ไดคาสท์", "ตกแต่ง"], report!.Stages);
        Assert.Equal(4, report.Rows.Count);
        var withTrim = report.Rows.Where(r => r.Cells[1] is not null).Select(r => r.Cells[0]!.Lot).ToList();
        Assert.Equal(new[] { lot1, lot3, lot3 }.Order(), withTrim.Order());
        var unmatched = report.Rows.Single(r => r.Cells[1] is null);
        Assert.Equal((lot2, "AL-2", 100m), (unmatched.Cells[0]!.Lot, unmatched.Cells[0]!.MaterialLot, unmatched.Cells[0]!.Quantity)); // the first stage shows the good quantity
        Assert.Equal(105m, report.Rows.First(r => r.Cells[1] is not null).Cells[1]!.Quantity); // the others the total
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<LotTraceView>(new() { ["Report"] = report }));
        Assert.Contains(lot1, html);
        Assert.Contains("ตั้งแต่วันที่", html);
    }

    [Fact] // ไดคาสท์ → ตกแต่ง → ประกอบ: the third stage received the Lot of the trim; the product pattern and the machine pick the stages (P… and G0… are not die-cast)
    public async Task The_chain_goes_on_to_the_assembly_and_picks_its_stages_by_machine_and_product()
    {
        var month = NewMonth();
        var (die, trim, assembly) = (await MachineAsync('D'), await MachineAsync('T'), await MachineAsync('A'));
        var (diePart, trimPart, assemblyPart, packPart) = (await ProductAsync("SD"), await ProductAsync("ST"), await ProductAsync("SA"), await ProductAsync("P"));
        var (dieLot, trimLot) = (Unique("L"), Unique("L"));
        await LineAsync("D", die, month.AddDays(1), diePart, dieLot, null);
        await LineAsync("D", die, month.AddDays(1), packPart, Unique("L"), null); // a P… product on a die machine is not die-cast
        await LineAsync("T", trim, month.AddDays(4), trimPart, trimLot, dieLot);
        await LineAsync("A", assembly, month.AddDays(8), assemblyPart, Unique("A"), trimLot);

        var report = await BuildAsync("die-trim-assembly", month);

        var row = Assert.Single(report!.Rows);
        Assert.Equal(3, row.Cells.Count);
        Assert.All(row.Cells, cell => Assert.NotNull(cell));
        Assert.Equal(dieLot, row.Cells[0]!.Lot);
    }

    [Fact] // อัดผ้าเบรก → ฝนผ้าเบรก → ประกอบ: inner joins (a press Lot nobody ground is not listed), the assembly through its raw material 2, only g5… press products
    public async Task The_brake_pad_chain_lists_only_what_was_followed_all_the_way()
    {
        var month = NewMonth();
        var (press, grind, assembly) = (await MachineAsync('B'), await MachineAsync('G'), await MachineAsync('A'));
        var (pressPart, otherPart, grindPart, assemblyPart) = (await ProductAsync("G5"), await ProductAsync("G3"), await ProductAsync("SG"), await ProductAsync("SA"));
        var (full, ground, alone, other) = (Unique("L"), Unique("L"), Unique("L"), Unique("L"));
        await LineAsync("B", press, month.AddDays(1), pressPart, full, null);
        await LineAsync("B", press, month.AddDays(1), pressPart, ground, null); // ground but never assembled
        await LineAsync("B", press, month.AddDays(1), pressPart, alone, null); // never ground
        await LineAsync("B", press, month.AddDays(1), otherPart, other, null); // not a g5… product
        var (grindFull, grindGround) = (Unique("F"), Unique("F"));
        await LineAsync("G", grind, month.AddDays(3), grindPart, grindFull, full);
        await LineAsync("G", grind, month.AddDays(3), grindPart, grindGround, ground);
        await LineAsync("G", grind, month.AddDays(3), grindPart, Unique("F"), other);
        await LineAsync("A", assembly, month.AddDays(6), assemblyPart, Unique("A"), "X", grindFull);

        var twoStages = await BuildAsync("press-grind", month);
        var threeStages = await BuildAsync("press-grind-assembly", month);

        Assert.Equal(2, twoStages!.Rows.Count); // the two g5… press lots that were ground
        Assert.Equal([full], threeStages!.Rows.Select(r => r.Cells[0]!.Lot));
        Assert.Equal(grindFull, threeStages.Rows[0].Cells[1]!.Lot);
        Assert.NotNull(threeStages.Rows[0].Cells[2]);
    }

    [Fact] // อัดดิสเบรก → ดิสเบรก: GD… products, press machine b…, disc machine c…; the whole chain from the die-cast end goes back through the press
    public async Task The_disc_brake_chain_and_the_whole_chain_from_the_die_cast_end()
    {
        var month = NewMonth();
        var (press, discMachine) = (await MachineAsync('B'), await MachineAsync('C'));
        var disc = await ProductAsync("GD");
        var pressLot = Unique("L");
        await LineAsync("B", press, month.AddDays(1), disc, pressLot, null);
        await LineAsync("K", discMachine, month.AddDays(4), disc, Unique("C"), pressLot);

        var discReport = await BuildAsync("disc", month);

        Assert.Equal(["อัดดิสเบรก", "ดิสเบรก"], discReport!.Stages);
        var row = Assert.Single(discReport.Rows);
        Assert.NotNull(row.Cells[1]);

        // the whole chain: die → trim → assembly → grind → press
        var (die, trim, assembly, grind) = (await MachineAsync('D'), await MachineAsync('T'), await MachineAsync('A'), await MachineAsync('G'));
        var (pad, other) = (await ProductAsync("G5"), await ProductAsync("SX"));
        var (dieLot, trimLot, grindLot, padLot) = (Unique("L"), Unique("L"), Unique("L"), Unique("L"));
        await LineAsync("B", press, month.AddDays(2), pad, padLot, null);
        await LineAsync("G", grind, month.AddDays(3), other, grindLot, padLot);
        await LineAsync("D", die, month.AddDays(5), other, dieLot, null);
        await LineAsync("T", trim, month.AddDays(6), other, trimLot, dieLot);
        await LineAsync("A", assembly, month.AddDays(7), other, Unique("A"), trimLot, grindLot);

        var fromDie = await BuildAsync("from-die", month);
        var fromPress = await BuildAsync("from-press", month);

        var chain = Assert.Single(fromDie!.Rows);
        Assert.Equal(5, chain.Cells.Count);
        Assert.Equal(padLot, chain.Cells[4]!.Lot);
        Assert.Equal(grindLot, chain.Cells[3]!.Lot);
        var pressRow = fromPress!.Rows.Single(r => r.Cells[0]!.Lot == padLot);
        Assert.Equal(5, pressRow.Cells.Count);
        Assert.Equal(dieLot, pressRow.Cells[4]!.Lot);
    }

    [Fact] // a kind the report does not know, and a range with nothing
    public async Task An_unknown_kind_has_no_report_and_an_empty_range_prints_no_data()
    {
        var month = NewMonth();

        var unknown = await BuildAsync("nope", month);
        var empty = await BuildAsync("die-trim", month);

        Assert.Null(unknown);
        Assert.Empty(empty!.Rows);
        Assert.Contains("ไม่มีข้อมูล", System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<LotTraceView>(new() { ["Report"] = empty })));
        Assert.Equal(7, LotTraceReports.Titles.Count);
    }
}

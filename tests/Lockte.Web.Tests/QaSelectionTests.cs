using Lockte.Prod;

namespace Lockte.Web.Tests;

/// <summary>เลือกบรรทัดสุ่มตรวจ (SCR-12, BR-017, #130): the lines of a department on a day, the correction of their numbers, the mark (MARK1 = Y) and the specs of the department. Fictional data.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class QaSelectionTests(LocalDbFixture db)
{
    private static int year = Random.Shared.Next(3100, 3600);
    private static int counter = Random.Shared.Next(10000, 60000);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static DateOnly NewDay() => new(Interlocked.Increment(ref year), 6, 15);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("report1");
        return await action(scope);
    }

    private async Task<int> ProductAsync()
        => (await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("ZQ"), "สินค้าสุ่มตรวจ", null, Unit: "ชิ้น")))).Id!.Value;

    private async Task SaveReportAsync(string department, DateOnly date, int product, params (decimal Good, decimal? Reject)[] lines)
    {
        var no = department + Interlocked.Increment(ref counter).ToString("00000");
        var saved = await AsAsync(s => s.Get<ProductionReportRegistry>().SaveAsync(new ProductionReportInput(null, null, department, date, no, null,
            lines.Select(l => new ProductionReportLineInput(null, null, product, null, null, null, 8m, "LM1", null, l.Good, l.Reject, null, null, "LOT-1")).ToList()), ProductionSaveMode.Save));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
    }

    private Task<IReadOnlyList<QaLineRow>> LinesAsync(string department, DateOnly day, bool onlySelected = false)
        => AsAsync(s => s.Get<QaSelectionDirectory>().GetLinesAsync(department, day, onlySelected));

    private static QaLineEdit Edit(QaLineRow row, decimal? good = null, decimal? reject = null, bool? selected = null, string? lot = null)
        => new(row.LineId, row.RowVersion, good ?? row.Good, reject ?? row.Reject, row.Hours, lot ?? row.Lot, row.MaterialLot1, row.MaterialLot2, selected ?? row.Selected);

    [Fact] // only the department's lines of that day, by report number and line
    public async Task Shows_the_lines_of_the_department_on_the_day()
    {
        var day = NewDay();
        var product = await ProductAsync();
        await SaveReportAsync("D", day, product, (100, 5), (200, null));
        await SaveReportAsync("D", day.AddDays(1), product, (300, null));   // another day
        await SaveReportAsync("T", day, product, (400, 1));                 // another department

        var lines = await LinesAsync("D", day);

        Assert.Equal([100m, 200m], lines.Select(l => l.Good));
        Assert.Equal([1, 2], lines.Select(l => l.LineNo));
        Assert.Equal((105m, "LOT-1", false), (lines[0].Total, lines[0].Lot, lines[0].Selected));
        Assert.Empty(await LinesAsync("G", day));
    }

    [Fact] // the mark is saved, and the sheet takes only the marked lines; the user clears the marks
    public async Task Marks_the_lines_and_clears_them()
    {
        var day = NewDay();
        var product = await ProductAsync();
        await SaveReportAsync("D", day, product, (100, null), (200, null), (300, null));
        var lines = await LinesAsync("D", day);

        var saved = await AsAsync(s => s.Get<QaSelectionRegistry>().SaveAsync([Edit(lines[0], selected: true), Edit(lines[2], selected: true)]));

        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        Assert.Equal([100m, 300m], (await LinesAsync("D", day, onlySelected: true)).Select(l => l.Good));
        Assert.Equal(2, await AsAsync(s => s.Get<QaSelectionRegistry>().ClearMarksAsync("D", day)));
        Assert.Empty(await LinesAsync("D", day, onlySelected: true));
        Assert.Equal(3, (await LinesAsync("D", day)).Count);
    }

    [Fact] // good or reject corrected: the total is worked out again (AC-21); the lots are kept as typed
    public async Task Correcting_the_numbers_works_out_the_total_again()
    {
        var day = NewDay();
        var product = await ProductAsync();
        await SaveReportAsync("T", day, product, (1000, 10));
        var line = (await LinesAsync("T", day)).Single();

        var saved = await AsAsync(s => s.Get<QaSelectionRegistry>().SaveAsync([Edit(line, good: 950, reject: 25, lot: "  แก้แล้ว  ")]));

        Assert.True(saved.Succeeded);
        var again = (await LinesAsync("T", day)).Single();
        Assert.Equal((950m, 25m, 975m, "แก้แล้ว"), (again.Good, again.Reject, again.Total, again.Lot));
    }

    [Fact] // a negative number is refused; an old rowversion is refused
    public async Task Refuses_negative_numbers_and_old_rowversions()
    {
        var day = NewDay();
        var product = await ProductAsync();
        await SaveReportAsync("D", day, product, (100, null));
        var line = (await LinesAsync("D", day)).Single();

        var negative = await AsAsync(s => s.Get<QaSelectionRegistry>().SaveAsync([Edit(line, good: -1)]));
        await AsAsync(s => s.Get<QaSelectionRegistry>().SaveAsync([Edit(line, good: 120)]));

        Assert.False(negative.Succeeded);
        Assert.Contains("ติดลบ", negative.Errors[0]);
        await Assert.ThrowsAsync<Lockte.SharedKernel.Persistence.ConcurrencyConflictException>(() => AsAsync(s => s.Get<QaSelectionRegistry>().SaveAsync([Edit(line, good: 130)])));
        Assert.Equal(120m, (await LinesAsync("D", day)).Single().Good);
    }

    [Fact] // the specs a department prints are those of its groups, by the name of the report; the mold sheets are their own
    public async Task The_specs_of_a_department_are_those_of_its_groups()
    {
        var typeId = (await AsAsync(s => s.Get<QaSpecRegistry>().SaveTypeAsync(new QaDocumentTypeInput(null, Unique("WI-S"), "ฉบับที่ 1", "ใบตรวจ", null, "ชิ้น", Lockte.Prod.Data.QaResultColumns.Pieces8, null,
            Lockte.Prod.Data.QaPageLayout.Full, null, null)))).Id!.Value;
        var stem = Unique("Z");
        foreach (var name in new[] { $"ไดคาสท์{stem}", $"ตกแต่ง{stem}", $"พิมพ์{stem}", $"ไดคาสท์{stem}X" })
            await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(new QaSpecInput(null, typeId, name, null, null, [], [], null, null, false, null)));

        var die = (await AsAsync(s => s.Get<QaSelectionDirectory>().GetSpecsAsync("D"))).Select(x => x.Name).Where(n => n.Contains(stem)).ToList();
        var trim = (await AsAsync(s => s.Get<QaSelectionDirectory>().GetSpecsAsync("T"))).Select(x => x.Name).Where(n => n.Contains(stem)).ToList();
        var mold = (await AsAsync(s => s.Get<QaSelectionDirectory>().GetMoldSpecsAsync())).Select(x => x.Name).Where(n => n.Contains(stem)).ToList();

        Assert.Equal([$"ไดคาสท์{stem}", $"ไดคาสท์{stem}X"], die);
        Assert.Equal([$"ตกแต่ง{stem}"], trim);
        Assert.Equal([$"พิมพ์{stem}"], mold);
        Assert.Empty(await AsAsync(s => s.Get<QaSelectionDirectory>().GetSpecsAsync("P")));
    }
}

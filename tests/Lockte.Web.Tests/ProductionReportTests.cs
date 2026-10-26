using Lockte.Hr;
using Lockte.Org;
using Lockte.Prod;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>บันทึกใบรายงานผลผลิต (SCR-10, BR-015, BR-016) through the public services of module ผลิต (#116), with the seven departments.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class ProductionReportTests(LocalDbFixture db)
{
    private static int counter = Random.Shared.Next(10000, 60000);

    // Numbers that the other tests make are letter + 5 digits below 70000; the numbering tests use 9xxxx so they are always the largest.
    private static string FreeNumber(string department) => department + Interlocked.Increment(ref counter).ToString("00000");

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action, string user = "diecast1")
    {
        await using var scope = db.Scope(user);
        return await action(scope);
    }

    private async Task<int> NewProductAsync(int? moldId = null)
    {
        var saved = await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("ZP"), "สินค้าผลิต", null, Unit: "ชิ้น", MoldId: moldId)));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return saved.Id!.Value;
    }

    private async Task<int> NewMoldAsync()
    {
        var saved = await AsAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(null, Unique("ZM-"), "แม่พิมพ์ทดสอบ", null, Cost: 1, Owner: MoldRegistry.DefaultPerson)));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return saved.Id!.Value;
    }

    private async Task<int> NewEmployeeAsync()
    {
        var code = "R" + Random.Shared.Next(100, 1000);
        for (var i = 0; i < 30; i++)
        {
            var result = await AsAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput { Code = code, Name = "พนักงานผลิตทดสอบ" }), "hr1");
            if (result.Succeeded) return result.Id!.Value;
            code = "R" + Random.Shared.Next(100, 1000);
        }
        throw new InvalidOperationException("no free employee code");
    }

    private async Task<int> AnyShiftAsync()
    {
        var shifts = await AsAsync(s => s.Get<OrgDirectory>().GetShiftsAsync());
        Assert.NotEmpty(shifts);
        return shifts[0].Id;
    }

    private static ProductionReportLineInput Line(int? product, decimal? good, decimal? reject = null, int? employee = null, int? mold = null, int? shift = null,
        string? lot = null, int? id = null, byte[]? rowVersion = null)
        => new(id, rowVersion, product, null, null, shift, 8m, "LM1", "LM2", good, reject, employee, mold, lot);

    private static ProductionReportInput Report(string department, string number, params ProductionReportLineInput[] lines)
        => new(null, null, department, new DateOnly(2026, 10, 20), number, null, lines);

    private async Task<ProductionReportSaveResult> SaveAsync(ProductionReportInput input, ProductionSaveMode mode = ProductionSaveMode.Save)
        => await AsAsync(s => s.Get<ProductionReportRegistry>().SaveAsync(input, mode));

    private async Task<ProductionReportDto> SavedAsync(ProductionReportInput input, ProductionSaveMode mode = ProductionSaveMode.Save)
    {
        var result = await SaveAsync(input, mode);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return (await AsAsync(s => s.Get<ProductionReportDirectory>().GetAsync(result.Id!.Value)))!;
    }

    // ---------- BR-015 ----------

    [Fact] // AC-14, BR-015 #1: the next number follows the highest one of the department's real reports
    public async Task The_next_number_follows_the_highest_report_of_the_department()
    {
        var product = await NewProductAsync();
        await SavedAsync(Report("K", "K90205", Line(product, 10)));
        await SavedAsync(Report("K", "K90100", Line(product, 10)));

        Assert.Equal("K90206", await AsAsync(s => s.Get<ProductionReportDirectory>().ProposeNumberAsync("K")));
    }

    [Fact] // BR-015 #2: D69999 → D691000, kept as before
    public async Task A_thousandth_report_gets_a_four_digit_tail()
    {
        var product = await NewProductAsync();
        var saved = await SaveAsync(Report("G", "G90999", Line(product, 10)));

        Assert.True(saved.Succeeded);
        Assert.Equal("G901000", saved.NextNo);
    }

    [Fact] // a department without a report yet: nothing is proposed, the user types the first
    public async Task A_department_without_reports_has_no_proposal()
    {
        // the seven departments share one table, so a letter nobody uses stands in for "no report yet"
        Assert.Null(await AsAsync(s => s.Get<ProductionReportDirectory>().ProposeNumberAsync("Z")));
    }

    [Fact] // BR-015: a number that is taken is refused, with the next free one
    public async Task A_duplicate_number_is_refused_with_the_next_free_one()
    {
        var product = await NewProductAsync();
        var number = FreeNumber("T");
        await SavedAsync(Report("T", number, Line(product, 10)));

        var again = await SaveAsync(Report("T", number.ToLowerInvariant(), Line(product, 5)));

        Assert.False(again.Succeeded);
        Assert.Contains(number, again.Errors.Single());
        Assert.NotNull(again.NextNo);
    }

    [Fact] // the number must start with the letter of its department
    public async Task The_number_must_start_with_the_letter_of_the_department()
    {
        var product = await NewProductAsync();
        var wrong = await SaveAsync(Report("T", FreeNumber("D"), Line(product, 10)));

        Assert.False(wrong.Succeeded);
        Assert.Contains("T", wrong.Errors.Single());
    }

    // ---------- BR-016 ----------

    [Fact] // AC-21: good 980 + reject 20 → total 1000, whichever of the two was keyed last, and again when edited
    public async Task The_total_is_good_plus_reject_on_every_save()
    {
        var product = await NewProductAsync();
        var report = await SavedAsync(Report("T", FreeNumber("T"), Line(product, 980, 20), Line(product, 500)));

        Assert.Equal([1000m, 500m], report.Lines.Select(l => l.TotalQuantity));

        var edit = new ProductionReportInput(report.Id, report.RowVersion, "T", report.ReportDate, report.ReportNo, null,
            [Line(product, 980, 30, id: report.Lines[0].Id, rowVersion: report.Lines[0].RowVersion), Line(product, 600, null, id: report.Lines[1].Id, rowVersion: report.Lines[1].RowVersion)]);
        var saved = await SaveAsync(edit);
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        var after = (await AsAsync(s => s.Get<ProductionReportDirectory>().GetAsync(report.Id)))!;
        Assert.Equal([1010m, 600m], after.Lines.Select(l => l.TotalQuantity));
    }

    [Fact] // BR-016 #1: "บันทึก" drops the lines without จำนวนดี
    public async Task Save_drops_the_lines_without_good_quantity()
    {
        var product = await NewProductAsync();
        var report = await SavedAsync(Report("T", FreeNumber("T"), Line(product, 100), Line(product, null, 5), Line(product, 40)));

        Assert.Equal([1, 2], report.Lines.Select(l => l.LineNo)); // numbered again after the blank one was dropped
        Assert.Equal([100m, 40m], report.Lines.Select(l => l.GoodQuantity));
    }

    [Fact] // BR-016 #2: "กลับเมนูเดิม" also drops the lines when the date is blank
    public async Task Leaving_without_a_date_keeps_nothing()
    {
        var product = await NewProductAsync();
        var number = FreeNumber("T");
        var noDate = new ProductionReportInput(null, null, "T", null, number, null, [Line(product, 100)]);

        var saved = await SaveAsync(noDate, ProductionSaveMode.Leave);

        Assert.True(saved.Succeeded);
        Assert.Null(saved.Id);
        Assert.Null(await AsAsync(s => s.Get<ProductionReportDirectory>().FindByNumberAsync(number)));
    }

    [Fact] // BR-016 #3: leaving with a date keeps the lines that have a good quantity (and does not register the number apart from the report)
    public async Task Leaving_with_a_date_keeps_the_lines_with_good_quantity()
    {
        var product = await NewProductAsync();
        var report = await SavedAsync(Report("T", FreeNumber("T"), Line(product, 100), Line(product, null)), ProductionSaveMode.Leave);

        Assert.Single(report.Lines);
    }

    [Fact] // choosing a product fills its name and unit, and the mold for the departments that take it from the product (BR-016)
    public async Task The_product_fills_name_unit_and_the_mold_only_in_departments_that_take_it()
    {
        var mold = await NewMoldAsync();
        var product = await NewProductAsync(mold);

        var diecast = await SavedAsync(Report("D", FreeNumber("D"), Line(product, 10)));
        var trim = await SavedAsync(Report("T", FreeNumber("T"), Line(product, 10)));

        Assert.Equal(("สินค้าผลิต", "ชิ้น", mold), (diecast.Lines[0].ProductName, diecast.Lines[0].Unit, diecast.Lines[0].MoldId));
        Assert.Null(trim.Lines[0].MoldId);
        var defaults = await AsAsync(s => s.Get<ProductionReportDirectory>().DefaultsForProductAsync("D", product));
        Assert.Equal(mold, defaults!.MoldId);
        Assert.Null((await AsAsync(s => s.Get<ProductionReportDirectory>().DefaultsForProductAsync("T", product)))!.MoldId);
    }

    [Fact] // the employee comes from the register of module บุคคล only (#16)
    public async Task The_employee_must_be_in_the_register()
    {
        var product = await NewProductAsync();
        var employee = await NewEmployeeAsync();

        var ok = await SavedAsync(Report("T", FreeNumber("T"), Line(product, 10, employee: employee)));
        var unknown = await SaveAsync(Report("T", FreeNumber("T"), Line(product, 10, employee: 999_999_999)));

        Assert.Equal(employee, ok.Lines[0].EmployeeId);
        Assert.False(unknown.Succeeded);
    }

    [Fact] // the shift comes from the register of module องค์กร
    public async Task The_shift_must_be_in_the_register()
    {
        var product = await NewProductAsync();
        var shift = await AnyShiftAsync();

        var ok = await SavedAsync(Report("T", FreeNumber("T"), Line(product, 10, shift: shift, lot: "260903B35-A/1")));
        var unknown = await SaveAsync(Report("T", FreeNumber("T"), Line(product, 10, shift: 999_999_999)));

        Assert.Equal((shift, "260903B35-A/1"), (ok.Lines[0].ShiftId, ok.Lines[0].Lot));
        Assert.False(unknown.Succeeded);
    }

    [Fact] // BR-011: nothing is checked beyond what the keys force — a product-less line, free-text lots and a reject without a good quantity
    public async Task Nothing_more_is_checked_than_the_legacy_system_did()
    {
        var report = await SavedAsync(Report("P", FreeNumber("P"), Line(null, 0, 3, lot: "any text")));

        Assert.Equal((0m, 3m, 3m), (report.Lines[0].GoodQuantity, report.Lines[0].RejectQuantity, report.Lines[0].TotalQuantity));
    }

    [Fact] // the machine defaults of the departments (ตกแต่ง T01, ประกอบ A01, บรรจุ P01) and the machine must be in the register
    public async Task Default_machines_come_from_the_register()
    {
        var machines = await AsAsync(s => s.Get<OrgDirectory>().GetMachinesAsync());
        if (machines.All(m => m.Code != "T01"))
        {
            var department = await AsAsync(async s =>
            {
                var org = s.Get<OrgRegistry>();
                var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ผลิต"), 1, null))).Id!.Value;
                return (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("ตกแต่ง"), "t", 1, section, null, [], null))).Id!.Value;
            }, "qa1");
            var machine = await AsAsync(s => s.Get<OrgRegistry>().SaveMachineAsync(new MachineInput(null, "T01", "เครื่องตกแต่ง", department, null)), "qa1");
            Assert.True(machine.Succeeded, string.Join(" ", machine.Errors));
        }

        var trim = await AsAsync(s => s.Get<ProductionReportDirectory>().DefaultMachineIdAsync("T"));
        var diecast = await AsAsync(s => s.Get<ProductionReportDirectory>().DefaultMachineIdAsync("D"));

        Assert.NotNull(trim);
        Assert.Null(diecast);
        var product = await NewProductAsync();
        var report = await SavedAsync(Report("T", FreeNumber("T"), Line(product, 10)) with { MachineId = trim });
        Assert.Equal(trim, report.MachineId);
        var missing = await SaveAsync(Report("T", FreeNumber("T"), Line(product, 10)) with { MachineId = 999_999_999 });
        Assert.False(missing.Succeeded);
    }

    [Fact] // two people editing the same report: the second save is refused
    public async Task A_stale_report_is_refused()
    {
        var product = await NewProductAsync();
        var report = await SavedAsync(Report("T", FreeNumber("T"), Line(product, 10)));
        var first = new ProductionReportInput(report.Id, report.RowVersion, "T", report.ReportDate, report.ReportNo, null,
            [Line(product, 11, id: report.Lines[0].Id, rowVersion: report.Lines[0].RowVersion)]);
        Assert.True((await SaveAsync(first)).Succeeded);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => SaveAsync(first with { RowVersion = report.RowVersion }));
    }

    [Fact] // deleting the report deletes its lines, and a product or mold that a report uses cannot be deleted
    public async Task A_report_can_be_deleted_and_protects_its_product()
    {
        var mold = await NewMoldAsync();
        var product = await NewProductAsync(mold);
        var report = await SavedAsync(Report("D", FreeNumber("D"), Line(product, 10)));

        var refused = await AsAsync(s => s.Get<ProductRegistry>().DeleteAsync(product, (s.Get<ProductDirectory>().GetAsync(product).Result)!.RowVersion));
        Assert.False(refused.Succeeded);

        var deleted = await AsAsync(s => s.Get<ProductionReportRegistry>().DeleteAsync(report.Id, report.RowVersion));
        Assert.True(deleted.Succeeded);
        Assert.Null(await AsAsync(s => s.Get<ProductionReportDirectory>().GetAsync(report.Id)));
    }

    [Fact] // all seven departments are configured and a report can be saved in each
    public async Task Every_department_can_save_a_report()
    {
        Assert.Equal(["D", "T", "B", "G", "K", "A", "P"], ProductionDepartments.All.Select(d => d.Code));
        var product = await NewProductAsync();
        foreach (var department in ProductionDepartments.All)
        {
            var number = FreeNumber(department.Code);
            var report = await SavedAsync(Report(department.Code, number, Line(product, 120, 5)));
            Assert.Equal((department.Code, 125m), (report.DepartmentCode, report.Lines[0].TotalQuantity));
        }
        Assert.Equal("ล๊อตอลูมิเนียม", ProductionDepartments.Find("d")!.MaterialLot1Label);
        Assert.Equal("ล๊อตเคมีผ้าเบรก", ProductionDepartments.Find("B")!.MaterialLot2Label);
        Assert.Null(ProductionDepartments.Find("T")!.MaterialLot2Label);
    }

    [Theory] // #117, #118: the next number of each department follows its own letter (BR-015), whatever the other departments hold
    [InlineData("B", "B90310", "B90311")]
    [InlineData("A", "A90310", "A90311")]
    [InlineData("P", "P90310", "P90311")]
    public async Task Each_department_numbers_its_own_reports(string department, string highest, string expected)
    {
        var product = await NewProductAsync();
        await SavedAsync(Report(department, highest, Line(product, 10)));

        var next = await AsAsync(s => s.Get<ProductionReportDirectory>().ProposeNumberAsync(department));

        Assert.Equal(expected, next);
    }

    [Fact] // #117, #118: the mold is filled from the product in อัดผ้าเบรก and ผ้าคลัทช์และดิสเบรก (and ไดคาสท์), not in the other four
    public async Task The_mold_is_filled_from_the_product_in_three_departments_only()
    {
        var mold = await NewMoldAsync();
        var product = await NewProductAsync(mold);

        var filled = new List<string>();
        foreach (var department in ProductionDepartments.All)
        {
            var report = await SavedAsync(Report(department.Code, FreeNumber(department.Code), Line(product, 10)));
            if (report.Lines[0].MoldId == mold) filled.Add(department.Code);
        }

        Assert.Equal(["D", "B", "K"], filled);
    }

    [Fact] // #117, #118: starting machines — ตกแต่ง, ประกอบ and บรรจุ have one, the other four none
    public void Only_three_departments_have_a_starting_machine()
    {
        Assert.Equal([("T", "T01"), ("A", "A01"), ("P", "P01")],
            ProductionDepartments.All.Where(d => d.DefaultMachineCode is not null).Select(d => (d.Code, d.DefaultMachineCode!)));
    }
}

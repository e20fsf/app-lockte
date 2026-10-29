using Lockte.Calib;
using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>Access's <c>Like</c> for หมายเลขเครื่อง (BR-010 ข้อ 4) without a database.</summary>
public sealed class InstrumentPatternTests
{
    [Theory]
    [InlineData("*", "VN-QA013/2561", true)]
    [InlineData("VN-QA*", "VN-QA012/2560", true)]
    [InlineData("vn-qa*", "VN-QA012/2560", true)] // case ignored
    [InlineData("VN-QA*", "VN-GR003/2567", false)]
    [InlineData("PG-DC1??/2569", "PG-DC125/2569", true)]
    [InlineData("PG-DC1??/2569", "PG-DC12/2569", false)]
    [InlineData("PG-DC1##/2569", "PG-DC125/2569", true)]
    [InlineData("PG-DC1##/2569", "PG-DC1AB/2569", false)]
    [InlineData("PG-DC125/2569", "PG-DC125/2569", true)] // no wildcard = the whole code
    [InlineData("PG-DC125", "PG-DC125/2569", false)]
    [InlineData("TC-BL058.2562", "TC-BL058/2562", false)] // '.' is a dot, not "any character"
    [InlineData("JG-AS001-2/2560", "JG-AS001-2/2560", true)]
    [InlineData("(A)+", "(A)+", true)] // regex characters are literal
    public void Like_pattern_matches_as_access_did(string pattern, string code, bool expected)
        => Assert.Equal(expected, InstrumentPattern.IsMatch(pattern, code));
}

/// <summary>ทะเบียนเครื่องมือวัด (#194, SCR-02/03, BR-001/003/004/005, AC-03/04/07/08).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class InstrumentTests(LocalDbFixture db)
{
    private static int lastEmployee = Random.Shared.Next(0x100, 0x800);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    /// <summary>Codes of the shape the real ones have; random so a re-used database does not collide.</summary>
    private static string NewCode(string prefix = "VN") => $"{prefix}-T{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}/2569";

    private async Task<T> AsQaAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    private Task<Lockte.SharedKernel.ServiceResult> SaveAsync(InstrumentInput input) => AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(input));

    private Task<InstrumentDto?> FindAsync(string code) => AsQaAsync(s => s.Get<InstrumentDirectory>().FindByCodeAsync(code));

    private async Task<InstrumentDto> AddAsync(InstrumentKind kind, string code, string? name = "Vernier Digital", string? unit = "mm", int? department = null,
        IReadOnlyList<int>? machines = null, int? calibrator = null)
    {
        var result = await SaveAsync(new InstrumentInput(null, kind, code, null, name, new DateOnly(2026, 8, 17), "Mitutoyo", department, machines, "0-150", "0.01", "0.1", unit,
            "GB-QA001/2551 - GB-QA005/2551", calibrator));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return (await FindAsync(code))!;
    }

    private async Task<(int Department, int[] Machines)> DepartmentAsync(int machineCount)
    {
        await using var scope = db.Scope("hr1");
        var org = scope.Get<OrgRegistry>();
        var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
        var department = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
        var machines = new List<int>();
        for (var i = 0; i < machineCount; i++)
            machines.Add((await org.SaveMachineAsync(new MachineInput(null, "M" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant() + i, "เครื่อง", department, null))).Id!.Value);
        return (department, machines.ToArray());
    }

    private async Task<int> EmployeeAsync(string name)
    {
        await using var scope = db.Scope("hr1");
        var result = await scope.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput { Code = $"Y{Interlocked.Increment(ref lastEmployee):X3}", Name = name });
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return result.Id!.Value;
    }

    [Fact] // AC-03
    public async Task A_new_caliper_is_in_the_register_in_order_of_code()
    {
        var late = NewCode("ZZ");
        var early = NewCode("AA");
        await AddAsync(InstrumentKind.Caliper, late);
        await AddAsync(InstrumentKind.Caliper, early);

        var list = (await AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.Caliper))).Select(i => i.Code).ToList();
        Assert.True(list.IndexOf(early) < list.IndexOf(late));
        var added = (await FindAsync(early))!;
        Assert.Equal(("Vernier Digital", "Mitutoyo", "0-150", "0.01", "0.1", "mm", new DateOnly(2026, 8, 17)),
            (added.Name, added.Maker, added.Range, added.Resolution, added.Limit, added.Unit, added.RegisteredOn));
    }

    [Fact] // AC-04, Q-22
    public async Task A_code_is_required_and_a_row_without_one_is_refused_not_deleted()
    {
        var before = (await AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.Caliper))).Count;

        var result = await SaveAsync(new InstrumentInput(null, InstrumentKind.Caliper, "   ", null, "Vernier Digital"));
        Assert.Equal(["กรุณากรอกหมายเลขเครื่อง"], result.Errors);
        Assert.Equal(before, (await AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.Caliper))).Count);
    }

    [Fact] // BR-006 #1 changed by Q-22: the code is unique over every kind, ignoring case and blanks around it
    public async Task A_repeated_code_is_refused_and_names_the_instrument_that_has_it()
    {
        var code = NewCode();
        await AddAsync(InstrumentKind.Caliper, code);

        var again = await SaveAsync(new InstrumentInput(null, InstrumentKind.Scale, $"  {code.ToLowerInvariant()} ", null, "เครื่องชั่ง 3 กก."));
        Assert.False(again.Succeeded);
        Assert.Equal([$"หมายเลขเครื่อง '{code.ToLowerInvariant()}' ซ้ำกับเครื่อง {code} (เวอร์เนีย/ไฮเกจ/ไมโครมิเตอร์)"], again.Errors);
    }

    [Fact] // BR-005 #1, #2
    public async Task Suggestions_are_the_distinct_values_of_the_kind_and_a_new_value_is_offered_next_time()
    {
        var unit = Unique("u");
        await AddAsync(InstrumentKind.PressureGauge, NewCode("PG"), "Pressure Gauge", unit);
        await AddAsync(InstrumentKind.PressureGauge, NewCode("PG"), "Pressure Gauge", unit);
        await AddAsync(InstrumentKind.TempControl, NewCode("TC"), "Temp Control", Unique("t"));

        var choices = await AsQaAsync(s => s.Get<InstrumentDirectory>().GetChoicesAsync(InstrumentKind.PressureGauge));
        Assert.Single(choices.Units, unit); // once, although two rows have it
        Assert.Equal(choices.Units.Order(StringComparer.CurrentCulture), choices.Units);
        Assert.DoesNotContain(choices.Names, n => n == "Temp Control"); // another kind's value is not suggested

        var newUnit = Unique("bar");
        await AddAsync(InstrumentKind.PressureGauge, NewCode("PG"), "Pressure Gauge", newUnit);
        Assert.Contains(newUnit, (await AsQaAsync(s => s.Get<InstrumentDirectory>().GetChoicesAsync(InstrumentKind.PressureGauge))).Units);
    }

    [Fact] // AC-07, BR-003: every field, the code too, and the history says old → new
    public async Task Editing_changes_the_fields_and_history_has_old_and_new_value()
    {
        var code = NewCode("TC");
        var original = await AddAsync(InstrumentKind.TempControl, code, "Temp Control", unit: null);
        var renamed = NewCode("TC");

        var edit = await SaveAsync(new InstrumentInput(original.Id, InstrumentKind.TempControl, renamed, original.RowVersion, "Temp Control", original.RegisteredOn, original.Maker,
            null, null, original.Range, original.Resolution, original.Limit, "°C", original.StandardUsed));
        Assert.True(edit.Succeeded, string.Join(", ", edit.Errors));

        var edited = (await FindAsync(renamed))!;
        Assert.Equal(("°C", renamed), (edited.Unit, edited.Code));
        Assert.Null(await FindAsync(code));

        await using var read = db.Scope();
        var rows = await read.Get<HistoryService>().SearchAsync(new(Table: "เครื่องมือวัด", RowKey: renamed));
        Assert.Contains(rows, r => r.Action == HistoryActions.Modified && r.Column == "หน่วย" && r.OldValue is null && r.NewValue == "°C" && r.UserName == "qa1");
        Assert.Contains(rows, r => r.Action == HistoryActions.Modified && r.Column == "หมายเลขเครื่อง" && r.OldValue == code && r.NewValue == renamed);
    }

    [Fact]
    public async Task A_stale_edit_is_a_conflict_and_a_stale_delete_too()
    {
        var instrument = await AddAsync(InstrumentKind.Scale, NewCode("WG"), "เครื่องชั่ง 3 กก.", "กรัม");
        Assert.True((await SaveAsync(new InstrumentInput(instrument.Id, InstrumentKind.Scale, instrument.Code, instrument.RowVersion, "เครื่องชั่ง 3 กก. แก้ครั้งแรก"))).Succeeded);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            SaveAsync(new InstrumentInput(instrument.Id, InstrumentKind.Scale, instrument.Code, instrument.RowVersion, "แก้จากหน้าเก่า")));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => AsQaAsync(s => s.Get<InstrumentRegistry>().DeleteAsync(instrument.Id, instrument.RowVersion)));
    }

    [Fact] // AC-08, BR-004: gone from the list, history says who deleted it
    public async Task Deleting_removes_the_instrument_and_history_keeps_who_did()
    {
        var keep = await AddAsync(InstrumentKind.PressureGauge, NewCode("PG"), "Pressure Gauge", "Kg/cm²");
        var gone = await AddAsync(InstrumentKind.PressureGauge, NewCode("PG"), "Pressure Gauge", "Kg/cm²");
        var before = (await AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.PressureGauge))).Count;

        await using (var scope = db.Scope("qa2"))
            Assert.True((await scope.Get<InstrumentRegistry>().DeleteAsync(gone.Id, gone.RowVersion)).Succeeded);

        var list = await AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.PressureGauge));
        Assert.Equal(before - 1, list.Count);
        Assert.DoesNotContain(list, i => i.Code == gone.Code);
        Assert.Contains(list, i => i.Code == keep.Code);
        Assert.Null(await FindAsync(gone.Code));
        // deleting a row that is already gone succeeds
        Assert.True((await AsQaAsync(s => s.Get<InstrumentRegistry>().DeleteAsync(gone.Id, gone.RowVersion))).Succeeded);

        await using var read = db.Scope();
        var rows = await read.Get<HistoryService>().SearchAsync(new(Table: "เครื่องมือวัด", RowKey: gone.Code));
        Assert.Contains(rows, r => r.Action == HistoryActions.Deleted && r.UserName == "qa2");
    }

    [Fact] // CONTEXT.md / Q-05: แผนก and เครื่องจักร come from org; printed back as `DC (D02)`
    public async Task Department_and_machines_come_from_org_and_print_as_the_legacy_text()
    {
        var (department, machines) = await DepartmentAsync(2);
        var instrument = await AddAsync(InstrumentKind.Scale, NewCode("WG"), department: department, machines: machines);

        var org = await AsQaAsync(async s => (await s.Get<OrgDirectory>().GetDepartmentAsync(department))!);
        var codes = (await AsQaAsync(s => s.Get<OrgDirectory>().GetMachinesAsync())).Where(m => machines.Contains(m.Id)).Select(m => m.Code).Order(StringComparer.CurrentCulture).ToList();
        Assert.Equal(org.Code + " (" + string.Join(",", codes) + ")", instrument.DepartmentText);
        Assert.Equal(machines.Order(), instrument.MachineIds.Order());

        // a department without machines prints its code alone
        var plain = await AddAsync(InstrumentKind.Scale, NewCode("WG"), department: department);
        Assert.Equal(org.Code, plain.DepartmentText);

        // dropping a machine on edit
        var one = await SaveAsync(new InstrumentInput(instrument.Id, InstrumentKind.Scale, instrument.Code, instrument.RowVersion, instrument.Name, DepartmentId: department, MachineIds: [machines[0]]));
        Assert.True(one.Succeeded);
        Assert.Single((await FindAsync(instrument.Code))!.MachineIds);
    }

    [Fact]
    public async Task A_machine_must_belong_to_the_chosen_department_and_needs_one()
    {
        var (department, machines) = await DepartmentAsync(1);
        var (other, otherMachines) = await DepartmentAsync(1);

        var wrong = await SaveAsync(new InstrumentInput(null, InstrumentKind.Scale, NewCode("WG"), null, DepartmentId: department, MachineIds: otherMachines));
        Assert.Equal(["เครื่องจักรที่เลือกต้องเป็นของแผนกที่ใช้งาน"], wrong.Errors);
        var none = await SaveAsync(new InstrumentInput(null, InstrumentKind.Scale, NewCode("WG"), null, MachineIds: machines));
        Assert.Equal(["เลือกเครื่องจักรได้เมื่อเลือกแผนกแล้ว"], none.Errors);
        var missing = await SaveAsync(new InstrumentInput(null, InstrumentKind.Scale, NewCode("WG"), null, DepartmentId: int.MaxValue));
        Assert.Equal(["ไม่พบแผนกที่เลือก"], missing.Errors);
        Assert.NotEqual(department, other);
    }

    [Fact] // ผู้สอบเทียบ = พนักงาน
    public async Task The_calibrator_is_an_employee_of_the_register()
    {
        var name = Unique("ช่างสอบเทียบ");
        var employee = await EmployeeAsync(name);
        var instrument = await AddAsync(InstrumentKind.Caliper, NewCode(), calibrator: employee);
        Assert.Equal((employee, name), (instrument.CalibratorEmployeeId, instrument.CalibratorName));

        var found = await AsQaAsync(s => s.Get<InstrumentDirectory>().SearchCalibratorsAsync(name));
        Assert.Equal(employee, Assert.Single(found).Id);

        var missing = await SaveAsync(new InstrumentInput(null, InstrumentKind.Caliper, NewCode(), null, CalibratorEmployeeId: int.MaxValue));
        Assert.Equal(["ไม่พบผู้สอบเทียบที่เลือกในทะเบียนพนักงาน"], missing.Errors);
    }

    [Fact]
    public async Task Text_values_are_trimmed_kept_as_typed_and_limited_in_length()
    {
        var code = NewCode("JG");
        var jig = await SaveAsync(new InstrumentInput(null, InstrumentKind.Jig, $" {code} ", null, "  RX100, MATE'U  ", Limit: " 0.20 mm. ", Axis: "7, 6", ManualNo: "WI-QA049"));
        Assert.True(jig.Succeeded);
        var saved = (await FindAsync(code))!;
        Assert.Equal(("RX100, MATE'U", "0.20 mm.", "7, 6", "WI-QA049", InstrumentKind.Jig), (saved.Name, saved.Limit, saved.Axis, saved.ManualNo, saved.Kind));

        var tooLong = await SaveAsync(new InstrumentInput(null, InstrumentKind.Caliper, NewCode(), null, Unit: new string('x', InstrumentRegistry.ShortTextLength + 1)));
        Assert.Equal([$"หน่วยยาวได้ไม่เกิน {InstrumentRegistry.ShortTextLength} ตัว"], tooLong.Errors);
        var kindChange = await SaveAsync(new InstrumentInput(saved.Id, InstrumentKind.Caliper, code, saved.RowVersion));
        Assert.Equal(["เปลี่ยนชนิดของเครื่องมือวัดไม่ได้"], kindChange.Errors);
    }

    [Fact] // BR-010 ข้อ 4, AC-09/10 shape: only the kind's own instruments, ordered by code
    public async Task Finding_by_pattern_looks_only_at_the_kind_and_orders_by_code()
    {
        var tag = "Q" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        foreach (var n in new[] { 3, 1, 2 }) await AddAsync(InstrumentKind.PressureGauge, $"PG-{tag}{n}/2569");
        await AddAsync(InstrumentKind.Scale, $"PG-{tag}9/2569"); // same shape, another kind

        var found = await AsQaAsync(s => s.Get<InstrumentDirectory>().FindByPatternAsync(InstrumentKind.PressureGauge, $"PG-{tag}*"));
        Assert.Equal([$"PG-{tag}1/2569", $"PG-{tag}2/2569", $"PG-{tag}3/2569"], found.Select(i => i.Code));
        Assert.Empty(await AsQaAsync(s => s.Get<InstrumentDirectory>().FindByPatternAsync(InstrumentKind.PressureGauge, "XX-999")));
        Assert.Empty(await AsQaAsync(s => s.Get<InstrumentDirectory>().FindByPatternAsync(InstrumentKind.PressureGauge, " ")));
        Assert.Single(await AsQaAsync(s => s.Get<InstrumentDirectory>().FindByPatternAsync(InstrumentKind.PressureGauge, $"PG-{tag}1/2569")));
    }
}

using Lockte.Org;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>ฝ่าย / แผนก / กะ / หัวหน้า (#44, SCR-05) through the public services of module องค์กร.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class OrgTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsHrAsync<T>(Func<OrgRegistry, OrgDirectory, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope.Get<OrgRegistry>(), scope.Get<OrgDirectory>());
    }

    private Task<int> NewSectionAsync() => AsHrAsync(async (registry, _) =>
        (await registry.SaveSectionAsync(new SectionInput(null, Unique("ผลิต"), 1, null))).Id!.Value);

    private static DepartmentInput NewDepartment(int sectionId, string? code = null, string? name = null,
        IReadOnlyCollection<DepartmentShiftInput>? shifts = null) =>
        new(null, code ?? Unique("D"), name ?? Unique("ไดคาสท์"), "d", 3, sectionId, null, shifts ?? [], null);

    private Task<DepartmentDto> FindAsync(int id) => AsHrAsync(async (_, directory) => (await directory.GetDepartmentAsync(id))!);

    private static DepartmentInput Input(DepartmentDto d, IReadOnlyCollection<DepartmentShiftInput> shifts) => new(
        d.Id, d.Code, d.Name, d.EmployeeIdPrefix, d.SortOrder, d.SectionId, d.HeadEmployeeId, shifts, d.RowVersion);

    private static DepartmentInput Input(DepartmentDto d) => Input(d,
        d.Shifts.Select(s => new DepartmentShiftInput(s.ShiftId, s.SupervisorName, s.ManagerName, s.FactoryManagerName)).ToList());

    /// <summary>กะ A–G are seeded with the database.</summary>
    private Task<int> ShiftIdAsync(string code)
        => AsHrAsync(async (_, directory) => (await directory.FindShiftByCodeAsync(code))!.Id);

    [Fact]
    public async Task Added_department_is_read_by_other_modules_through_the_directory()
    {
        var sectionId = await NewSectionAsync();
        var shiftA = await ShiftIdAsync("A");
        var input = NewDepartment(sectionId, code: Unique("x"),
            shifts: [new DepartmentShiftInput(shiftA, " หัวหน้า ก ", "ผจก. ผลิต", "ผจก. โรงงาน")]);

        var saved = await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(input));

        Assert.True(saved.Succeeded);
        var department = await FindAsync(saved.Id!.Value);
        Assert.Equal((input.Code.ToUpperInvariant(), input.Name, "D", 3, sectionId),
            (department.Code, department.Name, department.EmployeeIdPrefix, department.SortOrder, department.SectionId));
        Assert.Null(department.HeadEmployeeId); // linked once the employee register exists
        var shift = Assert.Single(department.Shifts);
        Assert.Equal(("A", "หัวหน้า ก", "ผจก. ผลิต", "ผจก. โรงงาน"),
            (shift.ShiftCode, shift.SupervisorName, shift.ManagerName, shift.FactoryManagerName));
        var byCode = await AsHrAsync((_, directory) => directory.FindDepartmentByCodeAsync(input.Code.ToLowerInvariant()));
        Assert.Equal(department.Id, byCode!.Id);
    }

    [Fact]
    public async Task Legacy_records_find_their_department_by_name_or_employee_id()
    {
        var sectionId = await NewSectionAsync();
        var name = Unique("อัดผ้าเบรก");
        var id = (await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(NewDepartment(sectionId, name: name)))).Id!.Value;

        var byName = await AsHrAsync((_, directory) => directory.FindDepartmentByNameAsync($" {name} "));
        var byEmployeeId = await AsHrAsync((_, directory) => directory.FindDepartmentsByEmployeeIdPrefixAsync("d385"));

        Assert.Equal(id, byName!.Id);
        Assert.Contains(byEmployeeId, d => d.Id == id); // other departments may share the letter (DQ-8)
        Assert.All(byEmployeeId, d => Assert.Equal("D", d.EmployeeIdPrefix));
        Assert.Empty(await AsHrAsync((_, directory) => directory.FindDepartmentsByEmployeeIdPrefixAsync(" ")));
    }

    [Fact]
    public async Task Department_code_and_name_must_be_unique_when_adding_or_editing()
    {
        var sectionId = await NewSectionAsync();
        var first = NewDepartment(sectionId);
        await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(first));
        var secondId = (await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(NewDepartment(sectionId)))).Id!.Value;
        var second = await FindAsync(secondId);

        var sameCode = await AsHrAsync((registry, _) =>
            registry.SaveDepartmentAsync(NewDepartment(sectionId, code: first.Code.ToLowerInvariant())));
        var sameName = await AsHrAsync((registry, _) =>
            registry.SaveDepartmentAsync(NewDepartment(sectionId, name: $" {first.Name} ")));
        var editToSameCode = await AsHrAsync((registry, _) =>
            registry.SaveDepartmentAsync(Input(second) with { Code = first.Code }));
        var editToSameName = await AsHrAsync((registry, _) =>
            registry.SaveDepartmentAsync(Input(second) with { Name = first.Name }));

        Assert.Equal([$"มีรหัสแผนก '{first.Code.ToUpperInvariant()}' อยู่แล้ว"], sameCode.Errors);
        Assert.Equal([$"มีแผนก '{first.Name}' อยู่แล้ว"], sameName.Errors);
        Assert.Equal([$"มีรหัสแผนก '{first.Code.ToUpperInvariant()}' อยู่แล้ว"], editToSameCode.Errors);
        Assert.Equal([$"มีแผนก '{first.Name}' อยู่แล้ว"], editToSameName.Errors);
        Assert.Equal((second.Code, second.Name), ((await FindAsync(secondId)).Code, (await FindAsync(secondId)).Name));
    }

    [Fact]
    public async Task Department_needs_a_section_and_a_one_letter_prefix()
    {
        var result = await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(
            new DepartmentInput(null, Unique("Q"), Unique("ประกัน"), "QA", 1, null, null, [], null)));

        Assert.False(result.Succeeded);
        Assert.Contains("ตัวอักษรนำหน้ารหัสพนักงานต้องเป็นตัวอักษรอังกฤษ 1 ตัว", result.Errors);
        Assert.Contains("กรุณาเลือกฝ่าย", result.Errors);
    }

    [Fact]
    public async Task Editing_keeps_the_row_and_records_old_and_new_name()
    {
        var sectionId = await NewSectionAsync();
        var id = (await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(NewDepartment(sectionId)))).Id!.Value;
        var loaded = await FindAsync(id);
        var newName = Unique("ตกแต่ง");

        var result = await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Input(loaded) with { Name = newName }));

        Assert.True(result.Succeeded);
        Assert.Equal(newName, (await FindAsync(id)).Name);
        await using var scope = db.Scope();
        var change = Assert.Single(await scope.Get<HistoryService>().SearchAsync(new(Table: "แผนก", RowKey: loaded.Code)),
            r => r.Action == HistoryActions.Modified);
        Assert.Equal(("ชื่อแผนก", loaded.Name, newName, "hr1"), (change.Column, change.OldValue, change.NewValue, change.UserName));
    }

    [Fact]
    public async Task Saving_over_someone_elses_newer_save_reports_a_conflict()
    {
        var sectionId = await NewSectionAsync();
        var id = (await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(NewDepartment(sectionId)))).Id!.Value;
        var loaded = await FindAsync(id);

        await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Input(loaded) with { SortOrder = 7 }));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Input(loaded) with { SortOrder = 8 })));
        Assert.Equal(7, (await FindAsync(id)).SortOrder);
    }

    [Fact]
    public async Task Name_is_required_so_clearing_it_never_deletes_and_a_blank_new_row_is_not_saved()
    {
        var sectionId = await NewSectionAsync();
        var id = (await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(NewDepartment(sectionId)))).Id!.Value;
        var loaded = await FindAsync(id);
        var before = (await AsHrAsync((_, directory) => directory.GetDepartmentsAsync())).Count;

        var blankExisting = await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Input(loaded) with { Name = "  " }));
        var blankNew = await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(
            new DepartmentInput(null, "", "", "", 0, null, null, [], null)));

        Assert.Contains("กรุณากรอกชื่อแผนก", blankExisting.Errors);
        Assert.Contains("กรุณากรอกชื่อแผนก", blankNew.Errors);
        Assert.Equal(loaded.Name, (await FindAsync(id)).Name);
        Assert.Equal(before, (await AsHrAsync((_, directory) => directory.GetDepartmentsAsync())).Count);
    }

    [Fact]
    public async Task Shifts_A_to_G_come_with_the_legacy_standard_times()
    {
        var shifts = await AsHrAsync((_, directory) => directory.GetShiftsAsync());

        Assert.Equal("ABCDEFG", string.Concat(shifts.Select(s => s.Code)));
        var b = shifts.Single(s => s.Code == "B");
        Assert.Equal((new TimeOnly(20, 10), new TimeOnly(5, 10), new TimeOnly(0, 10), new TimeOnly(1, 10), new TimeOnly(5, 30), new TimeOnly(8, 10), 60),
            (b.WorkIn, b.WorkOut, b.BreakStart, b.BreakEnd, b.OvertimeIn, b.OvertimeOut, b.RestMinutes));
        Assert.True(b.CrossesMidnight);
        var e = shifts.Single(s => s.Code == "E");
        Assert.Equal((new TimeOnly(8, 0), new TimeOnly(17, 0), (TimeOnly?)null), (e.WorkIn, e.WorkOut, e.OvertimeIn));
        Assert.True(shifts.Single(s => s.Code == "F").CrossesMidnight);
        Assert.False(shifts.Single(s => s.Code == "D").CrossesMidnight);
    }

    [Fact]
    public async Task Shift_codes_are_A_to_G_and_unique()
    {
        var shiftC = await AsHrAsync(async (_, directory) => (await directory.FindShiftByCodeAsync("c"))!);
        ShiftInput Edit(string code) => new(shiftC.Id, code, shiftC.WorkIn, shiftC.WorkOut, shiftC.BreakStart, shiftC.BreakEnd,
            shiftC.OvertimeIn, shiftC.OvertimeOut, shiftC.RestMinutes, shiftC.RowVersion);

        var outOfRange = await AsHrAsync((registry, _) => registry.SaveShiftAsync(new ShiftInput(null, "H",
            new(8, 0), new(17, 0), null, null, null, null, 60, null)));
        var duplicateNew = await AsHrAsync((registry, _) => registry.SaveShiftAsync(new ShiftInput(null, "a",
            new(8, 0), new(17, 0), null, null, null, null, 60, null)));
        var duplicateEdit = await AsHrAsync((registry, _) => registry.SaveShiftAsync(Edit("A")));

        Assert.Equal(["รหัสกะต้องเป็น A ถึง G"], outOfRange.Errors);
        Assert.Equal(["มีกะ 'A' อยู่แล้ว"], duplicateNew.Errors);
        Assert.Equal(["มีกะ 'A' อยู่แล้ว"], duplicateEdit.Errors);
    }

    [Fact]
    public async Task Department_shifts_keep_their_names_and_supervisor_per_shift()
    {
        var sectionId = await NewSectionAsync();
        var (shiftA, shiftB) = (await ShiftIdAsync("A"), await ShiftIdAsync("B"));
        var id = (await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(
            NewDepartment(sectionId, shifts: [new(shiftA, "หัวหน้า A", "ผจก. A", null)])))).Id!.Value;
        var withA = await FindAsync(id);
        var blank = Assert.Single(withA.Shifts);
        Assert.Null(blank.SupervisorEmployeeId); // may stay blank until the employee register links one

        await AsHrAsync((registry, _) => registry.SetShiftSupervisorAsync(id, shiftA, employeeId: 42, blank.RowVersion));
        var loaded = await FindAsync(id);
        await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Input(loaded,
            [new(shiftA, "หัวหน้า A", "ผจก. A", null), new(shiftB, "หัวหน้า B", "ผจก. B", "ผจก. โรงงาน B")])));
        var both = await FindAsync(id);
        await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Input(both, [new(shiftB, "หัวหน้า B", "ผจก. B", "ผจก. โรงงาน B")])));
        var onlyB = await FindAsync(id);

        Assert.Equal([("A", (int?)42, "หัวหน้า A", "ผจก. A", (string?)null), ("B", null, "หัวหน้า B", "ผจก. B", "ผจก. โรงงาน B")],
            both.Shifts.Select(s => (s.ShiftCode, s.SupervisorEmployeeId, s.SupervisorName, s.ManagerName, s.FactoryManagerName)));
        Assert.Equal(["B"], onlyB.Shifts.Select(s => s.ShiftCode));
    }

    [Fact]
    public async Task Changing_only_the_shifts_of_a_stale_row_reports_a_conflict()
    {
        var sectionId = await NewSectionAsync();
        var shift = await ShiftIdAsync("D");
        var id = (await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(NewDepartment(sectionId)))).Id!.Value;
        var loaded = await FindAsync(id);
        await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Input(loaded) with { SortOrder = 9 }));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Input(loaded, [new(shift, null, null, null)]))));
        Assert.Empty((await FindAsync(id)).Shifts);
    }
}

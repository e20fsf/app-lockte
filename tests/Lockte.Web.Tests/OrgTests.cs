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

    private static DepartmentInput NewDepartment(int sectionId, string? code = null, string? name = null) =>
        new(null, code ?? Unique("D"), name ?? Unique("ไดคาสท์"), "d", 3, sectionId, null, "ผจก. ผลิต", "ผจก. โรงงาน", [], null);

    private Task<DepartmentDto> FindAsync(int id) => AsHrAsync(async (_, directory) => (await directory.GetDepartmentAsync(id))!);

    [Fact]
    public async Task Added_department_is_read_by_other_modules_through_the_directory()
    {
        var sectionId = await NewSectionAsync();
        var input = NewDepartment(sectionId, code: Unique("x"));

        var saved = await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(input));

        Assert.True(saved.Succeeded);
        var department = await FindAsync(saved.Id!.Value);
        Assert.Equal((input.Code.ToUpperInvariant(), input.Name, "D", 3, sectionId, "ผจก. ผลิต", "ผจก. โรงงาน"),
            (department.Code, department.Name, department.EmployeeIdPrefix, department.SortOrder, department.SectionId,
             department.ManagerName, department.FactoryManagerName));
        Assert.Null(department.HeadEmployeeId); // linked once the employee register exists
        var byCode = await AsHrAsync((_, directory) => directory.FindDepartmentByCodeAsync(input.Code.ToLowerInvariant()));
        Assert.Equal(department.Id, byCode!.Id);
    }

    [Fact]
    public async Task Department_code_and_name_must_be_unique()
    {
        var sectionId = await NewSectionAsync();
        var first = NewDepartment(sectionId);
        await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(first));

        var sameCode = await AsHrAsync((registry, _) =>
            registry.SaveDepartmentAsync(NewDepartment(sectionId, code: first.Code.ToLowerInvariant())));
        var sameName = await AsHrAsync((registry, _) =>
            registry.SaveDepartmentAsync(NewDepartment(sectionId, name: $" {first.Name} ")));

        Assert.Equal([$"มีรหัสแผนก '{first.Code.ToUpperInvariant()}' อยู่แล้ว"], sameCode.Errors);
        Assert.Equal([$"มีแผนก '{first.Name}' อยู่แล้ว"], sameName.Errors);
    }

    [Fact]
    public async Task Department_needs_a_section_and_a_one_letter_prefix()
    {
        var result = await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(
            new DepartmentInput(null, Unique("Q"), Unique("ประกัน"), "QA", 1, null, null, null, null, [], null)));

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

        var result = await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(new DepartmentInput(
            loaded.Id, loaded.Code, newName, loaded.EmployeeIdPrefix, loaded.SortOrder, loaded.SectionId,
            null, loaded.ManagerName, loaded.FactoryManagerName, [], loaded.RowVersion)));

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
        DepartmentInput Edit(string manager) => new(loaded.Id, loaded.Code, loaded.Name, loaded.EmployeeIdPrefix,
            loaded.SortOrder, loaded.SectionId, null, manager, null, [], loaded.RowVersion);

        await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Edit("คนแรก")));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Edit("คนที่สอง"))));
        Assert.Equal("คนแรก", (await FindAsync(id)).ManagerName);
    }

    [Fact]
    public async Task Blank_name_deletes_the_row_like_the_legacy_screen()
    {
        var sectionId = await NewSectionAsync();
        var id = (await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(NewDepartment(sectionId)))).Id!.Value;
        var loaded = await FindAsync(id);

        var blankExisting = await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(new DepartmentInput(
            loaded.Id, loaded.Code, "  ", loaded.EmployeeIdPrefix, 1, sectionId, null, null, null, [], loaded.RowVersion)));
        var blankNew = await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(
            new DepartmentInput(null, "", "", "", 0, null, null, null, null, [], null)));

        Assert.True(blankExisting.Succeeded);
        Assert.True(blankNew.Succeeded);
        Assert.Null(await AsHrAsync((_, directory) => directory.GetDepartmentAsync(id)));
    }

    [Fact]
    public async Task Shift_register_holds_codes_with_times_and_knows_overnight_shifts()
    {
        var code = await FreeShiftCodeAsync();

        var saved = await AsHrAsync((registry, _) => registry.SaveShiftAsync(new ShiftInput(null, code.ToLowerInvariant(),
            new(20, 10), new(5, 10), new(0, 10), new(1, 10), new(5, 30), new(8, 10), 60, null)));
        var duplicate = await AsHrAsync((registry, _) => registry.SaveShiftAsync(new ShiftInput(null, code,
            new(8, 0), new(17, 0), null, null, null, null, 60, null)));

        Assert.True(saved.Succeeded);
        var shift = await AsHrAsync((_, directory) => directory.FindShiftByCodeAsync(code));
        Assert.Equal((code, new TimeOnly(20, 10), new TimeOnly(5, 10), 60), (shift!.Code, shift.WorkIn, shift.WorkOut, shift.RestMinutes));
        Assert.True(shift.CrossesMidnight);
        Assert.Equal([$"มีกะ '{code}' อยู่แล้ว"], duplicate.Errors);
    }

    [Fact]
    public async Task Shift_codes_are_A_to_G()
    {
        var result = await AsHrAsync((registry, _) => registry.SaveShiftAsync(new ShiftInput(null, "H",
            new(8, 0), new(17, 0), null, null, null, null, 60, null)));

        Assert.Equal(["รหัสกะต้องเป็น A ถึง G"], result.Errors);
    }

    [Fact]
    public async Task Department_shifts_are_saved_with_it_and_keep_their_supervisor()
    {
        var sectionId = await NewSectionAsync();
        var (shiftA, shiftB) = (await ShiftAsync(), await ShiftAsync());
        var id = (await AsHrAsync((registry, _) =>
            registry.SaveDepartmentAsync(NewDepartment(sectionId) with { ShiftIds = [shiftA] }))).Id!.Value;
        var withA = await FindAsync(id);
        var blank = Assert.Single(withA.Shifts);
        Assert.Null(blank.EmployeeId); // may stay blank until the employee register links one

        await AsHrAsync((registry, _) => registry.SetShiftSupervisorAsync(id, shiftA, employeeId: 42, blank.RowVersion));
        var loaded = await FindAsync(id);
        await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Input(loaded, [shiftA, shiftB])));
        var both = await FindAsync(id);
        await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Input(both, [shiftB])));
        var onlyB = await FindAsync(id);

        Assert.Equal([(shiftA, (int?)42), (shiftB, null)], both.Shifts.Select(s => (s.ShiftId, s.EmployeeId)).OrderBy(s => s.ShiftId == shiftB));
        Assert.Equal([shiftB], onlyB.Shifts.Select(s => s.ShiftId));
    }

    [Fact]
    public async Task Changing_only_the_shifts_of_a_stale_row_reports_a_conflict()
    {
        var sectionId = await NewSectionAsync();
        var shift = await ShiftAsync();
        var id = (await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(NewDepartment(sectionId)))).Id!.Value;
        var loaded = await FindAsync(id);
        await AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Input(loaded, []) with { ManagerName = "คนอื่นแก้ก่อน" }));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsHrAsync((registry, _) => registry.SaveDepartmentAsync(Input(loaded, [shift]))));
        Assert.Empty((await FindAsync(id)).Shifts);
    }

    private static DepartmentInput Input(DepartmentDto d, IReadOnlyCollection<int> shiftIds) => new(
        d.Id, d.Code, d.Name, d.EmployeeIdPrefix, d.SortOrder, d.SectionId, d.HeadEmployeeId, d.ManagerName,
        d.FactoryManagerName, shiftIds, d.RowVersion);

    /// <summary>Shift codes are A–G and tests share one database: take a free one.</summary>
    private async Task<string> FreeShiftCodeAsync()
    {
        var existing = await AsHrAsync((_, directory) => directory.GetShiftsAsync());
        return "ABCDEFG".Select(c => c.ToString()).First(c => existing.All(s => s.Code != c));
    }

    /// <summary>A shift to tick: an existing one, or a new one while codes are free.</summary>
    private async Task<int> ShiftAsync()
    {
        var code = await FreeShiftCodeAsync();
        return (await AsHrAsync((registry, _) => registry.SaveShiftAsync(new ShiftInput(null, code,
            new(8, 0), new(17, 0), new(12, 0), new(13, 0), new(17, 20), new(20, 10), 60, null)))).Id!.Value;
    }
}

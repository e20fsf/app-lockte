using Lockte.Org;
using Lockte.Prod;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Tests;

/// <summary>แม่พิมพ์ (module ผลิต, SCR-17) and เครื่องจักร (module องค์กร, SCR-19) through their public services (#104).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class MoldMachineTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<T> AsQaAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    private Task<int> NewDepartmentAsync() => AsQaAsync(async s =>
    {
        var org = s.Get<OrgRegistry>();
        var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ผลิต"), 1, null))).Id!.Value;
        return (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("ไดคาสท์"), "d", 1, section, null, [], null))).Id!.Value;
    });

    private async Task<MoldDto> SavedMoldAsync(MoldInput input)
    {
        var saved = await AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(input));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return (await AsQaAsync(s => s.Get<MoldDirectory>().GetAsync(saved.Id!.Value)))!;
    }

    private async Task<MachineDto> SavedMachineAsync(MachineInput input)
    {
        var saved = await AsQaAsync(s => s.Get<OrgRegistry>().SaveMachineAsync(input));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return (await AsQaAsync(s => s.Get<OrgDirectory>().GetMachinesAsync())).Single(m => m.Id == saved.Id);
    }

    /// <summary>A table of another module that holds a foreign key to a row, like ใบรายงานผลผลิต will.</summary>
    private async Task ReferenceAsync(string referenced, int id)
    {
        var table = Unique("Ref");
        await using var scope = db.Scope();
        var widgets = scope.Get<WidgetDbContext>();
#pragma warning disable EF1003 // identifiers built here, never user input
        await widgets.Database.ExecuteSqlRawAsync(
            $"CREATE TABLE test.{table} (Id int IDENTITY PRIMARY KEY, TargetId int NOT NULL REFERENCES {referenced}(Id));" +
            $"INSERT test.{table} (TargetId) VALUES ({id});");
#pragma warning restore EF1003
    }

    // ---------- แม่พิมพ์ ----------

    [Fact]
    public async Task Mold_keeps_every_field_of_SCR_17()
    {
        var department = await NewDepartmentAsync();
        var code = Unique("BF-");
        var mold = await SavedMoldAsync(new MoldInput(null, " " + code.ToLowerInvariant() + " ", " แม่พิมพ์ก้ามเบรก ", null,
            PiecesPerShot: 2, MaintenanceShots: 50000, MaintenancePieces: null, MaintenanceYears: null, DepartmentId: department,
            Maker: " ผู้ทำ ก ", StartDate: new DateOnly(2024, 3, 1), EndDate: null, Cost: 125000.50m, Owner: "ลูกค้า ก", AluminumWeight: 12.345m));

        Assert.Equal((code, "แม่พิมพ์ก้ามเบรก", 2, 50000, null, null, department), (mold.Code, mold.Name, mold.PiecesPerShot,
            mold.MaintenanceShots, mold.MaintenancePieces, mold.MaintenanceYears, mold.DepartmentId));
        Assert.Equal(("ผู้ทำ ก", new DateOnly(2024, 3, 1), (DateOnly?)null, 125000.50m, "ลูกค้า ก", (decimal?)12.345m),
            (mold.Maker, mold.StartDate, mold.EndDate, mold.Cost, mold.Owner, mold.AluminumWeight));
        Assert.True(mold.InUse);
    }

    [Fact]
    public async Task Editing_a_mold_records_each_changed_field_in_the_history()
    {
        var code = Unique("BL-");
        var mold = await SavedMoldAsync(new MoldInput(null, code, "เดิม", null, Cost: 100, Owner: MoldRegistry.DefaultPerson));

        var edited = await AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(
            new MoldInput(mold.Id, code, "เดิม", mold.RowVersion, Cost: 250, Owner: MoldRegistry.DefaultPerson, EndDate: new DateOnly(2025, 1, 31))));
        Assert.True(edited.Succeeded);

        await using var scope = db.Scope();
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "แม่พิมพ์", RowKey: code));
        var modified = rows.Where(r => r.Action == HistoryActions.Modified).Select(r => (r.Column, r.OldValue, r.NewValue));
        Assert.Equivalent(new (string, string?, string?)[] { ("ราคา", "100.00", "250"), ("วันที่เลิกใช้", null, "2025-01-31") },
            modified.Select(x => (x.Column, x.OldValue, x.NewValue)));
    }

    [Fact]
    public async Task Molds_in_use_are_those_without_an_end_date()
    {
        var running = await SavedMoldAsync(new MoldInput(null, Unique("U-"), "ใช้อยู่", null));
        var retired = await SavedMoldAsync(new MoldInput(null, Unique("U-"), "เลิกใช้", null, EndDate: new DateOnly(2020, 12, 31)));

        var all = await AsQaAsync(s => s.Get<MoldDirectory>().GetAllAsync());
        var inUse = await AsQaAsync(s => s.Get<MoldDirectory>().GetAllAsync(inUseOnly: true));

        Assert.Contains(all, m => m.Id == retired.Id);
        Assert.Contains(inUse, m => m.Id == running.Id);
        Assert.DoesNotContain(inUse, m => m.Id == retired.Id);
    }

    [Fact]
    public async Task Mold_with_bad_values_is_rejected()
    {
        var result = await AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(null, Unique("X-"), "ชื่อ", null,
            PiecesPerShot: -1, DepartmentId: int.MaxValue, StartDate: new DateOnly(2024, 5, 1), EndDate: new DateOnly(2024, 4, 1), Cost: -5)));

        Assert.False(result.Succeeded);
        Assert.Equivalent(new[]
        {
            "ชิ้น/พิมพ์และเกณฑ์บำรุงรักษาต้องไม่ติดลบ", "ราคาต้องไม่ติดลบ", "วันที่เลิกใช้ต้องไม่ก่อนวันที่เริ่มใช้", "ไม่พบแผนกที่เลือก",
        }, result.Errors);
        var blank = await AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(null, " ", "", null)));
        Assert.Equivalent(new[] { "กรุณากรอกรหัสแม่พิมพ์", "กรุณากรอกชื่อแม่พิมพ์" }, blank.Errors);
    }

    [Fact]
    public async Task Duplicate_mold_code_is_rejected_and_a_stale_edit_is_a_conflict()
    {
        var code = Unique("D-");
        var mold = await SavedMoldAsync(new MoldInput(null, code, "ชุดแรก", null));

        var duplicate = await AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(null, code.ToLowerInvariant(), "ซ้ำ", null)));
        Assert.Equal([$"มีรหัสแม่พิมพ์ '{code}' อยู่แล้ว"], duplicate.Errors);

        await AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(mold.Id, code, "แก้ครั้งแรก", mold.RowVersion)));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsQaAsync(s => s.Get<MoldRegistry>().SaveAsync(new MoldInput(mold.Id, code, "แก้จากหน้าเก่า", mold.RowVersion))));
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsQaAsync(s => s.Get<MoldRegistry>().DeleteAsync(mold.Id, mold.RowVersion)));
    }

    [Fact]
    public async Task Deleting_a_mold_removes_it_and_leaves_the_history()
    {
        var code = Unique("DEL-");
        var mold = await SavedMoldAsync(new MoldInput(null, code, "ลบได้", null));

        Assert.True((await AsQaAsync(s => s.Get<MoldRegistry>().DeleteAsync(mold.Id, mold.RowVersion))).Succeeded);

        Assert.Null(await AsQaAsync(s => s.Get<MoldDirectory>().FindByCodeAsync(code)));
        Assert.True((await AsQaAsync(s => s.Get<MoldRegistry>().DeleteAsync(mold.Id, mold.RowVersion))).Succeeded); // already gone
        await using var scope = db.Scope();
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "แม่พิมพ์", RowKey: code));
        Assert.Contains(rows, r => r.Action == HistoryActions.Deleted && r.Column == "รหัสแม่พิมพ์" && r.OldValue == code);
    }

    [Fact]
    public async Task A_mold_other_tables_refer_to_cannot_be_deleted()
    {
        var code = Unique("REF-");
        var mold = await SavedMoldAsync(new MoldInput(null, code, "ถูกอ้างอิง", null));
        await ReferenceAsync("prod.Molds", mold.Id);

        var result = await AsQaAsync(s => s.Get<MoldRegistry>().DeleteAsync(mold.Id, mold.RowVersion));

        Assert.Equal([$"ลบแม่พิมพ์ '{code}' ไม่ได้ เพราะมีรายการอื่นอ้างถึงอยู่"], result.Errors);
        Assert.NotNull(await AsQaAsync(s => s.Get<MoldDirectory>().FindByCodeAsync(code)));
        await using var scope = db.Scope();
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "แม่พิมพ์", RowKey: code));
        Assert.DoesNotContain(rows, r => r.Action == HistoryActions.Deleted);
    }

    // ---------- เครื่องจักร ----------

    [Fact]
    public async Task Machine_is_added_with_its_department_and_read_through_the_directory()
    {
        var department = await NewDepartmentAsync();
        var code = Unique("B");
        var machine = await SavedMachineAsync(new MachineInput(null, " " + code + " ", " เครื่องฉีด ", department, null));

        Assert.Equal((code, "เครื่องฉีด", department), (machine.Code, machine.Name, machine.DepartmentId));
        var byCode = await AsQaAsync(s => s.Get<OrgDirectory>().FindMachineByCodeAsync(code.ToLowerInvariant()));
        Assert.Equal(machine.Id, byCode!.Id);
    }

    [Fact]
    public async Task Machine_needs_code_name_and_an_existing_department_and_a_unique_code()
    {
        var department = await NewDepartmentAsync();
        var code = Unique("M");
        await SavedMachineAsync(new MachineInput(null, code, "เครื่องแรก", department, null));

        var duplicate = await AsQaAsync(s => s.Get<OrgRegistry>().SaveMachineAsync(new MachineInput(null, code.ToLowerInvariant(), "ซ้ำ", department, null)));
        Assert.Equal([$"มีรหัสเครื่องจักร '{code}' อยู่แล้ว"], duplicate.Errors);
        var blank = await AsQaAsync(s => s.Get<OrgRegistry>().SaveMachineAsync(new MachineInput(null, "", "", int.MaxValue, null)));
        Assert.Equivalent(new[] { "กรุณากรอกรหัสเครื่องจักร", "กรุณากรอกชื่อเครื่องจักร", "กรุณาเลือกแผนก" }, blank.Errors);
    }

    [Fact]
    public async Task Editing_a_machine_is_recorded_and_a_stale_edit_is_a_conflict()
    {
        var department = await NewDepartmentAsync();
        var other = await NewDepartmentAsync();
        var code = Unique("E");
        var machine = await SavedMachineAsync(new MachineInput(null, code, "เดิม", department, null));

        Assert.True((await AsQaAsync(s => s.Get<OrgRegistry>().SaveMachineAsync(new MachineInput(machine.Id, code, "ใหม่", other, machine.RowVersion)))).Succeeded);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsQaAsync(s => s.Get<OrgRegistry>().SaveMachineAsync(new MachineInput(machine.Id, code, "จากหน้าเก่า", department, machine.RowVersion))));
        await using var scope = db.Scope();
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "เครื่องจักร", RowKey: code));
        Assert.Equivalent(new (string?, string?)[] { ("เดิม", "ใหม่"), (department.ToString(), other.ToString()) },
            rows.Where(r => r.Action == HistoryActions.Modified).Select(r => (r.OldValue, r.NewValue)));
        Assert.All(rows, r => Assert.Equal("qa1", r.UserName));
    }

    [Fact]
    public async Task Deleting_a_machine_works_unless_other_tables_refer_to_it()
    {
        var department = await NewDepartmentAsync();
        var free = await SavedMachineAsync(new MachineInput(null, Unique("F"), "ลบได้", department, null));
        var used = await SavedMachineAsync(new MachineInput(null, Unique("U"), "ถูกอ้างอิง", department, null));
        await ReferenceAsync("org.Machines", used.Id);

        Assert.True((await AsQaAsync(s => s.Get<OrgRegistry>().DeleteMachineAsync(free.Id, free.RowVersion))).Succeeded);
        Assert.Null(await AsQaAsync(s => s.Get<OrgDirectory>().FindMachineByCodeAsync(free.Code)));

        var refused = await AsQaAsync(s => s.Get<OrgRegistry>().DeleteMachineAsync(used.Id, used.RowVersion));
        Assert.Equal([$"ลบเครื่องจักร '{used.Code}' ไม่ได้ เพราะมีรายการอื่นอ้างถึงอยู่"], refused.Errors);
        Assert.NotNull(await AsQaAsync(s => s.Get<OrgDirectory>().FindMachineByCodeAsync(used.Code)));
    }
}

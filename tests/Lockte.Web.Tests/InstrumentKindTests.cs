using Lockte.Calib;
using Lockte.Org;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Web.Tests;

/// <summary>เพรสเซอร์เกจ, เครื่องชั่ง, ตัวควบคุมอุณหภูมิ (#195): the same register and screens as เวอร์เนีย, each kind with its own values (AC-05, AC-08).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class InstrumentKindTests(LocalDbFixture db)
{
    private static string NewCode(string prefix) => $"{prefix}-T{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}/2569";

    private async Task<T> AsQaAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    private Task<IReadOnlyList<InstrumentDto>> ListAsync(InstrumentKind kind) => AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(kind));

    public static TheoryData<InstrumentKind, string, string, string?, string> Kinds => new()
    {
        { InstrumentKind.PressureGauge, "PG", "Pressure Gauge", "Kg/cm²", "bar" },
        { InstrumentKind.Scale, "WG", "เครื่องชั่ง 3 กก.", "กรัม", "กก." },
        { InstrumentKind.TempControl, "TC", "Temp Control", null, "°C" }, // ยี่ห้อว่างทั้งชนิด; TC-BL058/2562 had no unit until fixed (AC-07)
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task Each_kind_is_added_edited_and_deleted_on_its_own_list(InstrumentKind kind, string prefix, string name, string? unit, string editedUnit)
    {
        var code = NewCode(prefix);
        var added = await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(null, kind, code, null, name, new DateOnly(2026, 8, 29), Unit: unit, Limit: "10")));
        Assert.True(added.Succeeded, string.Join(", ", added.Errors));
        var instrument = Assert.Single(await ListAsync(kind), i => i.Code == code);
        Assert.Equal((kind, name, unit, null), (instrument.Kind, instrument.Name, instrument.Unit, instrument.Maker));

        // the list of another kind does not show it, and its choices do not offer it
        foreach (var other in Enum.GetValues<InstrumentKind>().Where(k => k != kind))
            Assert.DoesNotContain(await ListAsync(other), i => i.Code == code);

        var edited = await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(instrument.Id, kind, code, instrument.RowVersion, name, instrument.RegisteredOn, Unit: editedUnit, Limit: "10")));
        Assert.True(edited.Succeeded);
        Assert.Equal(editedUnit, Assert.Single(await ListAsync(kind), i => i.Code == code).Unit);
        Assert.Contains(editedUnit, (await AsQaAsync(s => s.Get<InstrumentDirectory>().GetChoicesAsync(kind))).Units);

        var current = Assert.Single(await ListAsync(kind), i => i.Code == code);
        var before = (await ListAsync(kind)).Count;
        Assert.True((await AsQaAsync(s => s.Get<InstrumentRegistry>().DeleteAsync(current.Id, current.RowVersion))).Succeeded);
        var after = await ListAsync(kind);
        Assert.Equal(before - 1, after.Count);
        Assert.DoesNotContain(after, i => i.Code == code);
    }

    [Fact] // data-model: เครื่องชั่ง `BL(32,33)` = แผนก BL เครื่อง 32 และ 33
    public async Task A_scale_can_have_several_machines_of_its_department()
    {
        int department;
        int[] machines;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, "ฝ่าย" + Guid.NewGuid().ToString("N")[..6], 1, null))).Id!.Value;
            department = (await org.SaveDepartmentAsync(new DepartmentInput(null, "B" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(), "แผนก" + Guid.NewGuid().ToString("N")[..6], "b", 3, section, null, [], null))).Id!.Value;
            machines =
            [
                (await org.SaveMachineAsync(new MachineInput(null, "32" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(), "เครื่อง 32", department, null))).Id!.Value,
                (await org.SaveMachineAsync(new MachineInput(null, "33" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(), "เครื่อง 33", department, null))).Id!.Value,
            ];
        }
        var code = NewCode("WG");
        Assert.True((await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(null, InstrumentKind.Scale, code, null, "เครื่องชั่งดิจิตอล 30 กก.", DepartmentId: department, MachineIds: machines)))).Succeeded);

        var scale = Assert.Single(await ListAsync(InstrumentKind.Scale), i => i.Code == code);
        Assert.Equal(2, scale.MachineIds.Count);
        Assert.Matches(@"^B\w+ \(32\w+,33\w+\)$", scale.DepartmentText);
    }

    [Fact] // #195: the code is unique over the whole register, not per kind
    public async Task The_same_code_cannot_be_used_by_two_kinds()
    {
        var code = NewCode("PG");
        Assert.True((await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(null, InstrumentKind.PressureGauge, code, null)))).Succeeded);
        var other = await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(null, InstrumentKind.TempControl, code, null)));
        Assert.False(other.Succeeded);
    }
}

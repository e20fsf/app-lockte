using Lockte.Org;
using Lockte.Purch;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Web.Tests;

/// <summary>แผนกที่ขอซื้อ + prefix เลขที่ใบขอซื้อ + ผู้จัดซื้อ + เหตุผล/หมายเหตุ (#216, SCR-10).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class RequestSetupTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<int> NewDepartmentAsync()
    {
        await using var scope = db.Scope("hr1");
        var org = scope.Get<OrgRegistry>();
        var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
        return (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
    }

    private async Task<T> AsBuyerAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    private static string NewPrefix() => "X" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    [Fact]
    public async Task A_prefix_is_upper_case_belongs_to_one_department_and_is_not_a_pattern()
    {
        var first = await NewDepartmentAsync();
        var second = await NewDepartmentAsync();
        var prefix = NewPrefix();

        Assert.True((await AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SetPrefixAsync(first, prefix.ToLowerInvariant(), null))).Succeeded);
        Assert.Equal(prefix, await AsBuyerAsync(s => s.Get<RequestSetupDirectory>().GetPrefixAsync(first)));

        var taken = await AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SetPrefixAsync(second, prefix, null));
        Assert.Equal([$"prefix '{prefix}' เป็นของแผนกอื่นแล้ว"], taken.Errors);
        Assert.False((await AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SetPrefixAsync(second, "D6*", null))).Succeeded);
        Assert.Equal(["ไม่พบแผนกที่เลือก"], (await AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SetPrefixAsync(int.MaxValue, "Q", null))).Errors);
    }

    [Fact]
    public async Task Changing_or_clearing_a_prefix_needs_the_loaded_rowversion_and_a_stale_one_conflicts()
    {
        var department = await NewDepartmentAsync();
        await AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SetPrefixAsync(department, NewPrefix(), null));
        var loaded = (await AsBuyerAsync(s => s.Get<RequestSetupDirectory>().GetDepartmentsAsync())).Single(d => d.DepartmentId == department);

        Assert.True((await AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SetPrefixAsync(department, NewPrefix(), loaded.RowVersion))).Succeeded);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SetPrefixAsync(department, NewPrefix(), loaded.RowVersion)));

        var fresh = (await AsBuyerAsync(s => s.Get<RequestSetupDirectory>().GetDepartmentsAsync())).Single(d => d.DepartmentId == department);
        Assert.True((await AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SetPrefixAsync(department, " ", fresh.RowVersion))).Succeeded);
        Assert.Null(await AsBuyerAsync(s => s.Get<RequestSetupDirectory>().GetPrefixAsync(department)));
    }

    [Fact]
    public async Task The_lists_round_trip_and_the_two_legacy_purchasers_are_the_default()
    {
        Assert.Equal([("F", "โรงงาน"), ("O", "สำนักงานสีลม")], RequestSetupDirectory.DefaultLocations.Select(l => (l.Code, l.Name)));
        var reason = Unique("เหตุผล");
        var reasons = (await AsBuyerAsync(s => s.Get<RequestSetupDirectory>().GetReasonsAsync())).Append(reason).Append("  ").ToList();
        Assert.True((await AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SaveReasonsAsync(reasons))).Succeeded);
        Assert.Contains(reason, await AsBuyerAsync(s => s.Get<RequestSetupDirectory>().GetReasonsAsync()));
        Assert.Contains("ผู้จัดซื้อต้องกรอกทั้งรหัสและชื่อ", (await AsBuyerAsync(s => s.Get<RequestSetupRegistry>().SaveLocationsAsync([new("X", " ")]))).Errors);
    }
}

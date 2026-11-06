using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>เลิกใช้แผนก (UAT-32): a retired แผนก leaves every list a user picks from, but not the records already saved.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class DepartmentActiveTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];
    private static int lastCode = 500;

    private async Task<DepartmentDto> NewDepartmentAsync()
    {
        await using var scope = db.Scope("hr1");
        var org = scope.Get<OrgRegistry>();
        var sectionId = (await org.SaveSectionAsync(new SectionInput(null, Unique("ผลิต"), 1, null))).Id!.Value;
        var id = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนกทดสอบ"), "D", 3, sectionId, null, [], null))).Id!.Value;
        return (await scope.Get<OrgDirectory>().GetDepartmentAsync(id))!;
    }

    private async Task<ServiceResult> SetActiveAsync(DepartmentDto d, bool active)
    {
        await using var scope = db.Scope("hr1");
        return await scope.Get<OrgRegistry>().SetDepartmentActiveAsync(d.Id, active, d.RowVersion);
    }

    private async Task<(IReadOnlyList<DepartmentDto> All, IReadOnlyList<DepartmentDto> Active)> ListsAsync()
    {
        await using var scope = db.Scope("hr1");
        var directory = scope.Get<OrgDirectory>();
        return (await directory.GetDepartmentsAsync(), await directory.GetActiveDepartmentsAsync());
    }

    [Fact]
    public async Task A_retired_department_leaves_the_pick_lists_but_stays_in_the_register_and_can_return()
    {
        var department = await NewDepartmentAsync();

        Assert.True((await SetActiveAsync(department, false)).Succeeded);
        var (all, active) = await ListsAsync();
        Assert.Contains(all, d => d.Id == department.Id && !d.IsActive);
        Assert.DoesNotContain(active, d => d.Id == department.Id);

        var retired = all.Single(d => d.Id == department.Id);
        Assert.True((await SetActiveAsync(retired, true)).Succeeded);
        Assert.Contains((await ListsAsync()).Active, d => d.Id == department.Id);
    }

    [Fact]
    public async Task A_department_somebody_still_works_in_cannot_be_retired()
    {
        var department = await NewDepartmentAsync();
        await using (var scope = db.Scope("hr1"))
        {
            var result = await scope.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
            {
                Code = $"U{Interlocked.Increment(ref lastCode):D3}", Name = "พนักงานทดสอบ", DepartmentId = department.Id,
            });
            Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        }

        var refused = await SetActiveAsync(department, false);

        Assert.False(refused.Succeeded);
        Assert.Contains("ยังมีพนักงานทำงานอยู่", refused.Errors.Single());
        Assert.Contains((await ListsAsync()).Active, d => d.Id == department.Id);
    }

    [Fact]
    public async Task Retiring_is_kept_in_the_edit_history()
    {
        var department = await NewDepartmentAsync();
        await SetActiveAsync(department, false);

        await using var scope = db.Scope("hr1");
        var rows = await scope.Get<HistoryService>().SearchAsync(new(RowKey: department.Code));

        var change = Assert.Single(rows, r => r.Action == HistoryActions.Modified);
        Assert.Equal(("ใช้งาน", "ใช่", "ไม่ใช่"), (change.Column, change.OldValue, change.NewValue));
    }

    [Fact]
    public async Task The_purchase_request_department_list_leaves_retired_ones_out()
    {
        var department = await NewDepartmentAsync();
        await using (var scope = db.Scope("hr1"))
            Assert.Contains(await scope.Get<Lockte.Purch.RequestSetupDirectory>().GetDepartmentsAsync(), d => d.DepartmentId == department.Id);
        await SetActiveAsync(department, false);

        await using var after = db.Scope("hr1");
        Assert.DoesNotContain(await after.Get<Lockte.Purch.RequestSetupDirectory>().GetDepartmentsAsync(), d => d.DepartmentId == department.Id);
    }

    /// <summary>
    /// Every page that lists แผนก from module องค์กร must leave the retired ones out of the choice
    /// (the register page itself, where they are brought back, is the only exception).
    /// </summary>
    [Fact]
    public void Every_page_that_picks_a_department_from_the_register_leaves_retired_ones_out()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(repo.FullName, "Lockte.slnx"))) repo = repo.Parent!;
        var pages = Path.Combine(repo.FullName, "src", "Lockte.Web", "Components", "Pages");
        var choice = new Regex(@"@foreach\s*\(\s*var\s+\w+\s+in\s+(?<list>[^)]*)\)");

        var offenders = new List<string>();
        var checkedPages = 0;
        foreach (var file in Directory.EnumerateFiles(pages, "*.razor", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (Path.GetFileName(file) == "Departments.razor" && file.Contains("Org")) continue;
            // Pages on DepartmentPrefixDto get their list from RequestSetupDirectory, which already leaves retired ones out (tested below).
            if (!text.Contains("DepartmentDto")) continue;
            foreach (Match m in choice.Matches(text))
            {
                var list = m.Groups["list"].Value;
                if (!Regex.IsMatch(list, @"^\s*(departments|Departments)\b")) continue;
                checkedPages++;
                if (!list.Contains("IsActive")) offenders.Add($"{Path.GetRelativePath(pages, file)}: {m.Value}");
            }
        }

        Assert.True(checkedPages >= 10, $"expected to find the department choices, found {checkedPages}");
        Assert.Empty(offenders);
    }
}

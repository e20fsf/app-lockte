using Lockte.Hr;

namespace Lockte.Web.Tests;

/// <summary>
/// เครื่องมือย้ายข้อมูล: ลบข้อมูล module พนักงานก่อนโหลดใหม่ — a table that points to an employee (here the โบนัส run and its results)
/// must not stop the delete, or the migration of the customer's newest file fails on a database that has already been used.
/// </summary>
[Collection(MigrationDbCollection.Name)]
public sealed class HrDataResetTests(MigrationDbFixture db)
{
    [Fact]
    public async Task Deleting_everything_also_deletes_a_bonus_run_and_its_results()
    {
        await using var scope = db.Scope("hr1");
        await scope.Get<HrDataReset>().DeleteAllAsync();

        var saved = await scope.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput { Code = "Q901", Name = "นาย รีเซ็ต สมมุติ", StartDate = new DateOnly(2020, 1, 1) });
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        var calculated = await scope.Get<BonusRegistry>().CalculateAsync(new BonusInput(new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 30), null));
        Assert.True(calculated.Succeeded, string.Join(" ", calculated.Errors));
        Assert.Equal(1, calculated.Id);
        Assert.NotNull(await scope.Get<BonusDirectory>().GetLastInputAsync());

        await scope.Get<HrDataReset>().DeleteAllAsync();

        Assert.Null(await scope.Get<EmployeeDirectory>().FindByCodeAsync("Q901"));
        Assert.Null(await scope.Get<BonusDirectory>().GetLastInputAsync());
    }
}

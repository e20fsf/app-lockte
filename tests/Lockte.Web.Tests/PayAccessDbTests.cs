using Lockte.Hr;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Lockte.Web.Audit;
using Microsoft.AspNetCore.Identity;

namespace Lockte.Web.Tests;

/// <summary>
/// ดูข้อมูลค่าจ้างได้ (#277) against the real database: the flag per user and its history, the claim in the login
/// cookie, ข้อมูลค่าจ้าง never leaving the services for a user without it, and hidden values not changing on save.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class PayAccessDbTests(LocalDbFixture db)
{
    private static int lastCode;

    /// <summary>"Y" + 3 digits keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)).</summary>
    private static string NewCode() => $"Y{Interlocked.Increment(ref lastCode):D3}";

    private async Task<int> NewEmployeeAsync(double salary = 15000, double dailyWage = 500, string ttb = "1234567890", string kbank = "0987654321")
    {
        await using var hr = db.Scope("hr1");
        var result = await hr.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "นาย ค่าจ้าง " + Guid.NewGuid().ToString("N")[..4], Position = "พนักงาน",
            Salary = salary, DailyWage = dailyWage, BankAccountTtb = ttb, BankAccountKbank = kbank,
        });
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return result.Id!.Value;
    }

    private async Task<EmployeeDto> LoadAsync(int id, bool canViewPay)
    {
        await using var scope = db.Scope("reader", canViewPay);
        return (await scope.Get<EmployeeDirectory>().GetAsync(id))!;
    }

    // ---- the flag per user ----

    [Fact]
    public async Task New_user_cannot_view_pay_unless_the_admin_ticks_it_and_the_choice_is_listed()
    {
        var without = Users.NewName();
        var with = Users.NewName();
        await using var admin = db.Scope(AppUser.AdminUserName);
        var service = admin.Get<UserAdminService>();

        Assert.True((await service.CreateAsync(without, "ไม่เห็น", "temp1234")).Succeeded);
        Assert.True((await service.CreateAsync(with, "เห็น", "temp1234", canViewPay: true)).Succeeded);

        Assert.False((await Users.FindAsync(service, without)).CanViewPay);
        Assert.True((await Users.FindAsync(service, with)).CanViewPay);
    }

    [Fact]
    public async Task Admin_sets_the_flag_and_the_change_is_in_the_history()
    {
        var userName = Users.NewName();
        await using (var admin = db.Scope(AppUser.AdminUserName))
        {
            var service = admin.Get<UserAdminService>();
            await service.CreateAsync(userName, "สมชาย", "temp1234");
            var user = await Users.FindAsync(service, userName);
            Assert.True((await service.SetCanViewPayAsync(user.Id, true, user.RowVersion)).Succeeded);
            Assert.True((await Users.FindAsync(service, userName)).CanViewPay);
        }

        await using var check = db.Scope();
        var rows = await check.Get<HistoryService>().SearchAsync(new(Table: "ผู้ใช้", RowKey: userName));
        var change = Assert.Single(rows, r => r.Column == "ดูข้อมูลค่าจ้างได้" && r.Action == HistoryActions.Modified);
        Assert.Equal(("ไม่ใช่", "ใช่", AppUser.AdminUserName), (change.OldValue, change.NewValue, change.UserName));
    }

    [Fact]
    public async Task The_admin_account_cannot_be_given_the_right()
    {
        await using var admin = db.Scope(AppUser.AdminUserName);
        var service = admin.Get<UserAdminService>();
        await service.ResetAdminAsync("admin temp pass");
        var row = await Users.FindAsync(service, AppUser.AdminUserName);

        Assert.False((await service.SetCanViewPayAsync(row.Id, true, row.RowVersion)).Succeeded);
    }

    [Fact]
    public async Task Login_cookie_carries_the_claim_only_for_a_staff_user_with_the_flag()
    {
        var allowed = Users.NewName();
        var denied = Users.NewName();
        await using (var admin = db.Scope(AppUser.AdminUserName))
        {
            var service = admin.Get<UserAdminService>();
            await service.CreateAsync(allowed, "เห็น", "temp1234", canViewPay: true);
            await service.CreateAsync(denied, "ไม่เห็น", "temp1234");
            await service.ResetAdminAsync("admin temp pass");
        }

        await using var scope = db.Scope();
        var users = scope.Get<UserManager<AppUser>>();
        var factory = scope.Get<IUserClaimsPrincipalFactory<AppUser>>();
        async Task<bool> ViewsPayAsync(string name) => AccountClaims.CanViewPay(await factory.CreateAsync((await users.FindByNameAsync(name))!));

        Assert.True(await ViewsPayAsync(allowed));
        Assert.False(await ViewsPayAsync(denied));
        Assert.False(await ViewsPayAsync(AppUser.AdminUserName)); // the flag is stored on every existing user, the admin ignores it
    }

    // ---- ข้อมูลค่าจ้าง never leaves the services ----

    [Fact]
    public async Task Employee_without_the_right_is_read_without_pay_fields_but_everything_else_stays()
    {
        var id = await NewEmployeeAsync();

        var seen = await LoadAsync(id, canViewPay: true);
        var hidden = await LoadAsync(id, canViewPay: false);

        Assert.Equal((15000d, 500d, "1234567890", "0987654321"), (seen.Salary, seen.DailyWage, seen.BankAccountTtb, seen.BankAccountKbank));
        Assert.Equal((0d, 0d, null, null), (hidden.Salary, hidden.DailyWage, hidden.BankAccountTtb, hidden.BankAccountKbank));
        Assert.Equal((seen.Code, seen.Name, seen.Position), (hidden.Code, hidden.Name, hidden.Position));
    }

    [Fact]
    public async Task Saving_an_employee_without_the_right_changes_other_fields_and_keeps_the_pay_fields()
    {
        var id = await NewEmployeeAsync();
        var hidden = await LoadAsync(id, canViewPay: false); // what the screen holds: pay fields masked

        await using (var hr = db.Scope("hr2", canViewPay: false))
        {
            var saved = await hr.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
            {
                Id = id, Code = hidden.Code, Name = hidden.Name, Position = "หัวหน้า", RowVersion = hidden.RowVersion,
                Salary = hidden.Salary, DailyWage = hidden.DailyWage, BankAccountTtb = hidden.BankAccountTtb, BankAccountKbank = hidden.BankAccountKbank,
            });
            Assert.True(saved.Succeeded, string.Join(", ", saved.Errors));
        }
        // Even a value typed in by hand (a crafted call) is not taken.
        var again = await LoadAsync(id, canViewPay: false);
        await using (var hr = db.Scope("hr2", canViewPay: false))
            Assert.True((await hr.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
            {
                Id = id, Code = again.Code, Name = again.Name, Position = "หัวหน้า", RowVersion = again.RowVersion,
                Salary = 99999, DailyWage = 9999, BankAccountTtb = "0000000000", BankAccountKbank = "1",
            })).Succeeded);

        var after = await LoadAsync(id, canViewPay: true);
        Assert.Equal("หัวหน้า", after.Position);
        Assert.Equal((15000d, 500d, "1234567890", "0987654321"), (after.Salary, after.DailyWage, after.BankAccountTtb, after.BankAccountKbank));
    }

    [Fact]
    public async Task Adding_an_employee_without_the_right_starts_without_pay_fields()
    {
        int id;
        await using (var hr = db.Scope("hr2", canViewPay: false))
        {
            var result = await hr.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
            {
                Code = NewCode(), Name = "นาย ใหม่", Salary = 12000, DailyWage = 400, BankAccountTtb = "1111111111",
            });
            Assert.True(result.Succeeded);
            id = result.Id!.Value;
        }

        var row = await LoadAsync(id, canViewPay: true);
        Assert.Equal((0d, 0d, null), (row.Salary, row.DailyWage, row.BankAccountTtb));
    }

    [Fact]
    public async Task Leave_saved_without_the_right_uses_the_register_wage_and_shows_no_amounts()
    {
        var id = await NewEmployeeAsync(dailyWage: 450);
        var day = new DateOnly(2026, 3, 2);

        int leaveId;
        await using (var hr = db.Scope("hr2", canViewPay: false))
        {
            // The screen holds a masked wage (0); a crafted 1 must not be stored either.
            var saved = await hr.Get<LeaveRegistry>().SaveAsync(new LeaveInput(null, id, null, day, day, null, null, 1, "1", true, null, 1, null));
            Assert.True(saved.Save.Succeeded, string.Join(", ", saved.Save.Errors));
            leaveId = saved.Save.Id!.Value;
        }

        await using var seeing = db.Scope("hr1", canViewPay: true);
        var row = Assert.Single(await seeing.Get<LeaveDirectory>().ListOfEmployeeAsync(id, 2026));
        Assert.Equal((leaveId, 450d, 450d), (row.Id, row.DailyWage, row.Pay));

        await using var hiding = db.Scope("hr2", canViewPay: false);
        var hidden = Assert.Single(await hiding.Get<LeaveDirectory>().ListOfEmployeeAsync(id, 2026));
        Assert.Equal((0d, 0d), (hidden.DailyWage, hidden.Pay));
        Assert.Equal(0, (await hiding.Get<LeaveDirectory>().GetSummaryAsync(id, 2026)).PaidPay);
    }

    [Fact]
    public async Task Editing_a_leave_without_the_right_keeps_its_wage()
    {
        var id = await NewEmployeeAsync(dailyWage: 450);
        var day = new DateOnly(2026, 4, 6);
        await using (var hr = db.Scope("hr1"))
            Assert.True((await hr.Get<LeaveRegistry>().SaveAsync(new LeaveInput(null, id, null, day, day, null, null, 1, "1", false, null, null, null))).Save.Succeeded);

        LeaveDto row;
        await using (var hr = db.Scope("hr1"))
            row = Assert.Single(await hr.Get<LeaveDirectory>().ListOfEmployeeAsync(id, 2026));
        await using (var hr = db.Scope("hr2", canViewPay: false))
            Assert.True((await hr.Get<LeaveRegistry>().SaveAsync(
                new LeaveInput(row.Id, id, null, day, day, null, null, 1, "1", false, "แก้หมายเหตุ", 12345, row.RowVersion))).Save.Succeeded);

        await using var check = db.Scope("hr1");
        var after = Assert.Single(await check.Get<LeaveDirectory>().ListOfEmployeeAsync(id, 2026));
        Assert.Equal(("แก้หมายเหตุ", 450d), (after.Remark, after.DailyWage));
    }
}

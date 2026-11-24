using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Lockte.Web.Audit;
using Microsoft.AspNetCore.Identity;

namespace Lockte.Web.Tests;

/// <summary>สิทธิ์เข้าโปรแกรม (ADR 0005) against the real database: stored per user, in the history, in the login cookie, seed-users.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class ProgramAccessDbTests(LocalDbFixture db)
{
    [Fact]
    public async Task New_user_opens_no_program_unless_given_and_the_choice_is_listed()
    {
        var none = Users.NewName();
        var qa = Users.NewName();
        await using var admin = db.Scope(AppUser.AdminUserName);
        var service = admin.Get<UserAdminService>();

        Assert.True((await service.CreateAsync(none, "ไม่มี", "temp1234")).Succeeded);
        Assert.True((await service.CreateAsync(qa, "QA", "temp1234", programs: ["calib", "order"])).Succeeded);

        Assert.Empty((await Users.FindAsync(service, none)).Programs);
        Assert.Equal(["order", "calib"], (await Users.FindAsync(service, qa)).Programs);
    }

    [Fact]
    public async Task Admin_sets_programs_and_the_change_is_in_the_history()
    {
        var userName = Users.NewName();
        await using (var admin = db.Scope(AppUser.AdminUserName))
        {
            var service = admin.Get<UserAdminService>();
            await service.CreateAsync(userName, "สมชาย", "temp1234", programs: ["order"]);
            var user = await Users.FindAsync(service, userName);
            Assert.True((await service.SetProgramsAsync(user.Id, ["order", "hr"], user.RowVersion)).Succeeded);
            Assert.Equal(["hr", "order"], (await Users.FindAsync(service, userName)).Programs);
        }

        await using var check = db.Scope();
        var rows = await check.Get<HistoryService>().SearchAsync(new(Table: "ผู้ใช้", RowKey: userName));
        var change = Assert.Single(rows, r => r.Column == "เข้าโปรแกรมได้" && r.Action == HistoryActions.Modified);
        Assert.Equal(("order", "hr,order", AppUser.AdminUserName), (change.OldValue, change.NewValue, change.UserName));
    }

    [Fact]
    public async Task The_admin_account_cannot_be_given_programs()
    {
        await using var admin = db.Scope(AppUser.AdminUserName);
        var service = admin.Get<UserAdminService>();
        await service.ResetAdminAsync("admin temp pass");
        var row = await Users.FindAsync(service, AppUser.AdminUserName);

        Assert.False((await service.SetProgramsAsync(row.Id, ["hr"], row.RowVersion)).Succeeded);
        Assert.Empty(row.Programs);
    }

    [Fact]
    public async Task Login_cookie_carries_one_claim_per_program()
    {
        var userName = Users.NewName();
        await using (var admin = db.Scope(AppUser.AdminUserName))
            await admin.Get<UserAdminService>().CreateAsync(userName, "นก", "temp1234", programs: ["hr", "cash"]);

        await using var scope = db.Scope();
        var user = (await scope.Get<UserManager<AppUser>>().FindByNameAsync(userName))!;
        var principal = await scope.Get<IUserClaimsPrincipalFactory<AppUser>>().CreateAsync(user);

        Assert.True(AccountClaims.CanUseProgram(principal, "hr"));
        Assert.True(AccountClaims.CanUseProgram(principal, "cash"));
        Assert.False(AccountClaims.CanUseProgram(principal, "order"));
    }

    [Fact]
    public async Task Seed_creates_missing_users_with_their_programs_and_leaves_existing_ones_alone()
    {
        var existing = Users.NewName();
        var missing = Users.NewName();
        await using var system = db.Scope(SystemUser.Name);
        var service = system.Get<UserAdminService>();
        await service.CreateAsync(existing, "เดิม", "temp1234", programs: ["cash"]);

        var result = await service.CreateMissingAsync(
            [new InitialUser(existing, "ชื่อใหม่", ["hr"]), new InitialUser(missing, "บอม", ["order", "purch"])], "start1234");

        Assert.Equal([missing], result.Created);
        Assert.Equal([existing], result.Skipped);
        Assert.Empty(result.Errors);

        var kept = await Users.FindAsync(service, existing);
        Assert.Equal("เดิม", kept.DisplayName);
        Assert.Equal(["cash"], kept.Programs);

        var made = await Users.FindAsync(service, missing);
        Assert.Equal(("บอม", true, false), (made.DisplayName, made.MustChangePassword, made.CanViewPay));
        Assert.Equal(["order", "purch"], made.Programs);
    }

    [Fact]
    public async Task Seed_reports_a_password_the_rules_refuse()
    {
        await using var system = db.Scope(SystemUser.Name);
        var result = await system.Get<UserAdminService>().CreateMissingAsync([new InitialUser(Users.NewName(), "สั้น", [])], "x");

        Assert.Empty(result.Created);
        Assert.NotEmpty(result.Errors);
    }
}

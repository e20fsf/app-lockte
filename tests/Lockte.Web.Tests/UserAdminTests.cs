using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;

namespace Lockte.Web.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class UserAdminTests(LocalDbFixture db)
{
    [Fact]
    public async Task Created_user_is_listed_with_display_name_and_who_created_it()
    {
        var userName = Users.NewName();
        await using var admin = db.Scope(AppUser.AdminUserName);
        var service = admin.Get<UserAdminService>();

        Assert.True((await service.CreateAsync($"  {userName} ", " สมชาย ใจดี ", "temp1234")).Succeeded);

        var user = await Users.FindAsync(service, userName);
        Assert.Equal("สมชาย ใจดี", user.DisplayName);
        Assert.True(user.IsActive);
        Assert.True(user.MustChangePassword);
        Assert.False(user.IsAdmin);
        Assert.Equal(AppUser.AdminUserName, user.CreatedBy);
        Assert.Equal(AppUser.AdminUserName, user.UpdatedBy);
        Assert.InRange(user.CreatedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.NotEmpty(user.RowVersion);
    }

    [Fact]
    public async Task Last_change_records_who_and_when_but_keeps_the_creator()
    {
        var userName = Users.NewName();
        await using (var admin = db.Scope(AppUser.AdminUserName))
            await admin.Get<UserAdminService>().CreateAsync(userName, "สมชาย", "temp1234");

        await using (var self = db.Scope(userName))
        {
            var accounts = self.Get<AccountService>();
            var login = await accounts.CheckLoginAsync(userName, "temp1234");
            await accounts.ChangePasswordAsync(login.UserId!, "temp1234", "my own pass");
        }

        await using var check = db.Scope();
        var row = await Users.FindAsync(check.Get<UserAdminService>(), userName);
        Assert.Equal(AppUser.AdminUserName, row.CreatedBy);
        Assert.Equal(userName, row.UpdatedBy);
        Assert.True(row.UpdatedAt >= row.CreatedAt);
    }

    [Fact]
    public async Task Failed_logins_are_not_edits_and_do_not_change_last_edited_by()
    {
        var userName = Users.NewName();
        await using (var admin = db.Scope(AppUser.AdminUserName))
            await admin.Get<UserAdminService>().CreateAsync(userName, "สมชาย", "temp1234");

        await using (var anonymous = db.Scope("anonymous"))
            await anonymous.Get<AccountService>().CheckLoginAsync(userName, "wrong password");

        await using var check = db.Scope();
        Assert.Equal(AppUser.AdminUserName, (await Users.FindAsync(check.Get<UserAdminService>(), userName)).UpdatedBy);
    }

    [Fact]
    public async Task Duplicate_user_name_is_refused_in_Thai()
    {
        var userName = Users.NewName();
        await using var admin = db.Scope(AppUser.AdminUserName);
        var service = admin.Get<UserAdminService>();
        await service.CreateAsync(userName, "คนแรก", "temp1234");

        var result = await service.CreateAsync(userName.ToUpperInvariant(), "คนที่สอง", "temp1234");

        Assert.False(result.Succeeded);
        Assert.Contains("มีชื่อ user", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task Admin_name_is_reserved_and_admin_cannot_be_deactivated()
    {
        await using (var server = db.Scope(SystemUser.Name))
            await server.Get<UserAdminService>().ResetAdminAsync("admin pass 1");
        await using var admin = db.Scope(AppUser.AdminUserName);
        var service = admin.Get<UserAdminService>();

        Assert.False((await service.CreateAsync("Admin", "ปลอม", "temp1234")).Succeeded);
        var adminRow = await Users.FindAsync(service, AppUser.AdminUserName);
        Assert.False((await service.SetActiveAsync(adminRow.Id, false, adminRow.RowVersion)).Succeeded);
    }

    [Fact]
    public async Task Saving_over_a_change_made_after_the_list_was_loaded_reports_a_conflict()
    {
        var userName = Users.NewName();
        UserSummary loadedByFirstScreen;
        await using (var admin = db.Scope(AppUser.AdminUserName))
        {
            var service = admin.Get<UserAdminService>();
            await service.CreateAsync(userName, "สมชาย", "temp1234");
            loadedByFirstScreen = await Users.FindAsync(service, userName);
        }

        await using (var otherScreen = db.Scope(AppUser.AdminUserName))
        {
            var service = otherScreen.Get<UserAdminService>();
            var fresh = await Users.FindAsync(service, userName);
            Assert.True((await service.SetTemporaryPasswordAsync(fresh.Id, "other temp", fresh.RowVersion)).Succeeded);
        }

        await using var firstScreen = db.Scope(AppUser.AdminUserName);
        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            firstScreen.Get<UserAdminService>().SetActiveAsync(loadedByFirstScreen.Id, false, loadedByFirstScreen.RowVersion));
        Assert.Contains("ผู้ใช้อื่นบันทึกไปก่อน", ex.Message);

        await using var check = db.Scope();
        Assert.True((await Users.FindAsync(check.Get<UserAdminService>(), userName)).IsActive);
    }
}

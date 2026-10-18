using Lockte.Web.Accounts;

namespace Lockte.Web.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class PasswordTests(LocalDbFixture db)
{
    private async Task<(string UserName, string Id)> NewUserAsync(string tempPassword = "temp1234")
    {
        var userName = Users.NewName();
        await using var admin = db.Scope(AppUser.AdminUserName);
        var service = admin.Get<UserAdminService>();
        Assert.True((await service.CreateAsync(userName, "สมหญิง", tempPassword)).Succeeded);
        return (userName, (await Users.FindAsync(service, userName)).Id);
    }

    private async Task<LoginResult> LoginAsync(string userName, string password)
    {
        await using var scope = db.Scope();
        return await scope.Get<AccountService>().CheckLoginAsync(userName, password);
    }

    private async Task<AccountResult> ChangeAsync(string userId, string current, string next)
    {
        await using var scope = db.Scope();
        return await scope.Get<AccountService>().ChangePasswordAsync(userId, current, next);
    }

    [Fact]
    public async Task New_user_must_change_the_temporary_password_at_first_login()
    {
        var (userName, id) = await NewUserAsync();

        var first = await LoginAsync(userName, "temp1234");
        Assert.Equal(LoginStatus.MustChangePassword, first.Status);
        Assert.True(first.CanSignIn); // only into the change-password page

        Assert.True((await ChangeAsync(id, "temp1234", "chosen by me")).Succeeded);

        Assert.Equal(LoginStatus.Succeeded, (await LoginAsync(userName, "chosen by me")).Status);
        Assert.Equal(LoginStatus.Invalid, (await LoginAsync(userName, "temp1234")).Status);
    }

    [Fact]
    public async Task Temporary_password_set_by_admin_forces_a_change_and_lifts_a_lockout()
    {
        var (userName, id) = await NewUserAsync();
        await ChangeAsync(id, "temp1234", "forgotten pass");
        for (var i = 0; i < 5; i++) await LoginAsync(userName, "wrong password");
        Assert.Equal(LoginStatus.LockedOut, (await LoginAsync(userName, "forgotten pass")).Status);

        await using (var admin = db.Scope(AppUser.AdminUserName))
        {
            var service = admin.Get<UserAdminService>();
            var user = await Users.FindAsync(service, userName);
            Assert.NotNull(user.LockedUntil);
            Assert.True((await service.SetTemporaryPasswordAsync(id, "new temp 1", user.RowVersion)).Succeeded);
            Assert.Null((await Users.FindAsync(service, userName)).LockedUntil);
        }

        Assert.Equal(LoginStatus.MustChangePassword, (await LoginAsync(userName, "new temp 1")).Status);
        Assert.True((await ChangeAsync(id, "new temp 1", "remembered now")).Succeeded);
        Assert.Equal(LoginStatus.Succeeded, (await LoginAsync(userName, "remembered now")).Status);
    }

    [Theory]
    [InlineData("abcdefgh")]   // 8 lowercase letters: no complexity rule
    [InlineData("11111111")]
    [InlineData("รหัสภาษาไทย")]
    public async Task Any_password_of_8_or_more_characters_is_accepted(string password)
    {
        var (_, id) = await NewUserAsync();

        Assert.True((await ChangeAsync(id, "temp1234", password)).Succeeded);
    }

    [Fact]
    public async Task Password_shorter_than_8_is_refused_in_Thai()
    {
        var (userName, id) = await NewUserAsync();

        var result = await ChangeAsync(id, "temp1234", "1234567");

        Assert.False(result.Succeeded);
        Assert.Contains("อย่างน้อย 8 ตัว", Assert.Single(result.Errors));
        Assert.Equal(LoginStatus.MustChangePassword, (await LoginAsync(userName, "temp1234")).Status);
    }

    [Fact]
    public async Task Change_needs_the_current_password()
    {
        var (_, id) = await NewUserAsync();

        var result = await ChangeAsync(id, "not my password", "whatever123");

        Assert.False(result.Succeeded);
        Assert.Equal("รหัสผ่านเดิมไม่ถูกต้อง", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task Admin_cannot_create_a_user_with_a_short_temporary_password()
    {
        await using var admin = db.Scope(AppUser.AdminUserName);

        var result = await admin.Get<UserAdminService>().CreateAsync(Users.NewName(), "สั้น", "1234");

        Assert.False(result.Succeeded);
    }
}

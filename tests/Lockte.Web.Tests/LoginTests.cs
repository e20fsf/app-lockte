using Lockte.Web.Accounts;

namespace Lockte.Web.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class LoginTests(LocalDbFixture db)
{
    private const string TempPassword = "temp1234";
    private const string OwnPassword = "my own pass";

    /// <summary>A user created by admin who has already changed the temporary password.</summary>
    private async Task<string> ReadyUserAsync()
    {
        var userName = Users.NewName();
        await using (var admin = db.Scope(AppUser.AdminUserName))
            Assert.True((await admin.Get<UserAdminService>().CreateAsync(userName, "สมชาย", TempPassword)).Succeeded);
        await using var scope = db.Scope(userName);
        var accounts = scope.Get<AccountService>();
        var login = await accounts.CheckLoginAsync(userName, TempPassword);
        Assert.True((await accounts.ChangePasswordAsync(login.UserId!, TempPassword, OwnPassword)).Succeeded);
        return userName;
    }

    private async Task<LoginResult> LoginAsync(string userName, string password)
    {
        await using var scope = db.Scope();
        return await scope.Get<AccountService>().CheckLoginAsync(userName, password);
    }

    [Fact]
    public async Task Correct_password_logs_in()
    {
        var userName = await ReadyUserAsync();

        var result = await LoginAsync(userName, OwnPassword);

        Assert.Equal(LoginStatus.Succeeded, result.Status);
        Assert.True(result.CanSignIn);
    }

    [Fact]
    public async Task User_name_is_not_case_sensitive()
    {
        var userName = await ReadyUserAsync();

        Assert.Equal(LoginStatus.Succeeded, (await LoginAsync(userName.ToUpperInvariant(), OwnPassword)).Status);
    }

    [Fact]
    public async Task Wrong_password_or_unknown_user_is_refused()
    {
        var userName = await ReadyUserAsync();

        Assert.Equal(LoginStatus.Invalid, (await LoginAsync(userName, "wrong password")).Status);
        Assert.Equal(LoginStatus.Invalid, (await LoginAsync(Users.NewName(), OwnPassword)).Status);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_for_15_minutes()
    {
        var userName = await ReadyUserAsync();

        for (var i = 1; i <= 4; i++)
            Assert.Equal(LoginStatus.Invalid, (await LoginAsync(userName, "wrong password")).Status);
        var fifth = await LoginAsync(userName, "wrong password");
        var withRightPassword = await LoginAsync(userName, OwnPassword);

        Assert.Equal(LoginStatus.LockedOut, fifth.Status);
        Assert.Equal(LoginStatus.LockedOut, withRightPassword.Status);
        Assert.False(withRightPassword.CanSignIn);
        Assert.InRange(withRightPassword.LockedUntil!.Value - DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15.5));
    }

    [Fact]
    public async Task A_successful_login_resets_the_failure_count()
    {
        var userName = await ReadyUserAsync();

        for (var i = 1; i <= 4; i++) await LoginAsync(userName, "wrong password");
        await LoginAsync(userName, OwnPassword);
        for (var i = 1; i <= 4; i++) await LoginAsync(userName, "wrong password");

        Assert.Equal(LoginStatus.Succeeded, (await LoginAsync(userName, OwnPassword)).Status);
    }

    [Fact]
    public async Task Deactivated_user_cannot_log_in()
    {
        var userName = await ReadyUserAsync();
        await using (var admin = db.Scope(AppUser.AdminUserName))
        {
            var service = admin.Get<UserAdminService>();
            var user = await Users.FindAsync(service, userName);
            Assert.True((await service.SetActiveAsync(user.Id, false, user.RowVersion)).Succeeded);
        }

        Assert.Equal(LoginStatus.Disabled, (await LoginAsync(userName, OwnPassword)).Status);
        // Someone without the password learns nothing about the account.
        Assert.Equal(LoginStatus.Invalid, (await LoginAsync(userName, "wrong password")).Status);
    }

    [Fact]
    public async Task Reactivated_user_can_log_in_again()
    {
        var userName = await ReadyUserAsync();
        await using (var admin = db.Scope(AppUser.AdminUserName))
        {
            var service = admin.Get<UserAdminService>();
            var user = await Users.FindAsync(service, userName);
            await service.SetActiveAsync(user.Id, false, user.RowVersion);
            user = await Users.FindAsync(service, userName);
            Assert.True((await service.SetActiveAsync(user.Id, true, user.RowVersion)).Succeeded);
        }

        Assert.Equal(LoginStatus.Succeeded, (await LoginAsync(userName, OwnPassword)).Status);
    }
}

using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;

namespace Lockte.Web.Tests;

/// <summary>The server command <c>Lockte.Web reset-admin</c> goes through <see cref="UserAdminService.ResetAdminAsync"/>.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class ResetAdminTests(LocalDbFixture db)
{
    private async Task<AccountResult> ResetAsync(string password)
    {
        await using var server = db.Scope(SystemUser.Name);
        return await server.Get<UserAdminService>().ResetAdminAsync(password);
    }

    private async Task<LoginResult> LoginAsync(string password)
    {
        await using var scope = db.Scope();
        return await scope.Get<AccountService>().CheckLoginAsync(AppUser.AdminUserName, password);
    }

    [Fact]
    public async Task Reset_gets_a_locked_out_admin_back_in_with_a_temporary_password()
    {
        Assert.True((await ResetAsync("first admin pass")).Succeeded);
        await using (var scope = db.Scope(AppUser.AdminUserName))
        {
            var admin = await Users.FindAsync(scope.Get<UserAdminService>(), AppUser.AdminUserName);
            Assert.True(admin.IsAdmin);
            await scope.Get<AccountService>().ChangePasswordAsync(admin.Id, "first admin pass", "forgotten admin pass");
        }
        for (var i = 0; i < 5; i++) await LoginAsync("guessing wrong");
        Assert.Equal(LoginStatus.LockedOut, (await LoginAsync("forgotten admin pass")).Status);

        Assert.True((await ResetAsync("reset by server")).Succeeded);

        Assert.Equal(LoginStatus.MustChangePassword, (await LoginAsync("reset by server")).Status);
    }

    [Fact]
    public async Task Reset_refuses_a_short_password()
    {
        var result = await ResetAsync("short");

        Assert.False(result.Succeeded);
    }
}

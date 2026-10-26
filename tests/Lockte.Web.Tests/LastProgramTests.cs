using Lockte.Web.Accounts;
using Lockte.Web.Audit;
using Lockte.Web.Menus;

namespace Lockte.Web.Tests;

/// <summary>จำโปรแกรมที่ใช้ล่าสุดต่อผู้ใช้ (#103): the next login opens it straight away.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class LastProgramTests(LocalDbFixture db)
{
    private async Task<string> NewUserIdAsync(string userName)
    {
        await using var admin = db.Scope(AppUser.AdminUserName);
        var service = admin.Get<UserAdminService>();
        await service.CreateAsync(userName, "ผู้ใช้สมมุติ", "temp1234");
        return (await Users.FindAsync(service, userName)).Id;
    }

    private async Task<string?> LastAsync(string userId)
    {
        await using var scope = db.Scope();
        return await scope.Get<LastProgramService>().GetAsync(userId);
    }

    private async Task SetAsync(string userName, string userId, string program)
    {
        await using var scope = db.Scope(userName);
        await scope.Get<LastProgramService>().SetAsync(userId, program);
    }

    [Fact]
    public async Task A_new_user_has_none_and_then_the_last_program_opened_is_kept_per_user()
    {
        var userName = Users.NewName();
        var userId = await NewUserIdAsync(userName);
        var otherName = Users.NewName();
        var otherId = await NewUserIdAsync(otherName);

        Assert.Null(await LastAsync(userId));

        await SetAsync(userName, userId, OrderMenu.Menu.Key);
        await SetAsync(otherName, otherId, EmployeeMenu.Menu.Key);
        Assert.Equal(OrderMenu.Menu.Key, await LastAsync(userId));

        await SetAsync(userName, userId, EmployeeMenu.Menu.Key);
        Assert.Equal(EmployeeMenu.Menu.Key, await LastAsync(userId));
        Assert.Equal(EmployeeMenu.Menu.Key, await LastAsync(otherId));
    }

    [Fact]
    public async Task Switching_programs_is_not_an_edit_in_the_history()
    {
        var userName = Users.NewName();
        var userId = await NewUserIdAsync(userName);

        await SetAsync(userName, userId, OrderMenu.Menu.Key);
        await SetAsync(userName, userId, EmployeeMenu.Menu.Key);

        await using var scope = db.Scope();
        Assert.Empty(await scope.Get<HistoryService>().SearchAsync(new(UserName: userName)));
    }
}

using System.Text;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;

namespace Lockte.Web;

/// <summary>
/// <c>Lockte.Web.exe seed-users [--password &lt;password&gt;]</c> — creates the users of <see cref="InitialUsers.Sheet"/>
/// (user01 … user18) that do not exist yet, each with its programs and the same temporary password, which every user must
/// change at first login. Users that exist are not touched, so running it again is safe. Recorded as "system".
/// </summary>
internal static class SeedUsersCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var password = args is ["--password", var given] ? given : ResetAdminCommand.PromptPassword("รหัสชั่วคราวของ user ใหม่: ");
        if (password is null) return 1;

        await LockteDatabase.MigrateAsync(services);
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentUser>().OverrideUserName = SystemUser.Name;

        var result = await scope.ServiceProvider.GetRequiredService<UserAdminService>().CreateMissingAsync(InitialUsers.Sheet, password);
        foreach (var name in result.Created) Console.WriteLine($"สร้าง {name} แล้ว");
        if (result.Skipped.Count > 0) Console.WriteLine($"มีอยู่แล้ว ไม่แก้: {string.Join(", ", result.Skipped)}");
        foreach (var error in result.Errors) Console.Error.WriteLine(error);
        return result.Errors.Count > 0 ? 1 : 0;
    }
}

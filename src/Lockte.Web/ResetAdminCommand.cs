using System.Text;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web;

/// <summary>
/// <c>Lockte.Web.exe reset-admin [--password &lt;password&gt;]</c> — run on the server by the system administrator
/// when admin forgot the password (also creates admin on a new install). The password is temporary:
/// admin must change it at the next login.
/// </summary>
internal static class ResetAdminCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var password = args is ["--password", var given] ? given : Prompt();
        if (password is null) return 1;

        await using var scope = services.CreateAsyncScope();
        // No signed-in user on the server console: the change is recorded as "system".
        scope.ServiceProvider.GetRequiredService<CurrentUser>().OverrideUserName = SystemUser.Name;
        await scope.ServiceProvider.GetRequiredService<AccountsDbContext>().Database.MigrateAsync();

        var result = await scope.ServiceProvider.GetRequiredService<UserAdminService>().ResetAdminAsync(password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) Console.Error.WriteLine(error);
            return 1;
        }
        Console.WriteLine($"ตั้งรหัสชั่วคราวของ '{AppUser.AdminUserName}' แล้ว — ต้องเปลี่ยนรหัสตอน login ครั้งถัดไป");
        return 0;
    }

    private static string? Prompt()
    {
        var first = ReadHidden("รหัสชั่วคราวใหม่ของ admin: ");
        var second = ReadHidden("พิมพ์อีกครั้ง: ");
        if (first == second) return first;
        Console.Error.WriteLine("รหัสสองครั้งไม่ตรงกัน");
        return null;
    }

    private static string ReadHidden(string label)
    {
        Console.Write(label);
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
        var text = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (text.Length > 0) text.Length--; }
            else if (!char.IsControl(key.KeyChar)) text.Append(key.KeyChar);
        }
        Console.WriteLine();
        return text.ToString();
    }
}

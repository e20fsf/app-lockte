using Lockte.Web.Accounts;

namespace Lockte.Web.Tests;

internal static class Users
{
    /// <summary>Tests share one database, so every test makes its own users.</summary>
    public static string NewName() => "u" + Guid.NewGuid().ToString("N")[..10];

    public static async Task<UserSummary> FindAsync(UserAdminService service, string userName)
        => (await service.ListAsync()).Single(u => u.UserName == userName);
}

using System.Text;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;

namespace Lockte.Web.Seeding;

/// <summary>
/// <c>Lockte.Web.exe seed-purch-fake</c> — fills the database named by the connection string with the invented data of the วัตถุดิบ program (#269). It adds to what is there and refuses a database that
/// already holds the set, so a test server is made again by dropping the database first (tools/uat/Reset-PurchFake.ps1 does it, and only for a database whose name ends Uat/Test/Dev).
/// </summary>
internal static class SeedPurchFakeCommand
{
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        Console.OutputEncoding = Encoding.UTF8;
        await LockteDatabase.MigrateAsync(services);
        var seeder = new PurchFakeDataSeeder(() =>
        {
            var scope = services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<CurrentUser>().OverrideUserName = SystemUser.Name;
            return scope;
        });
        try
        {
            var made = await seeder.SeedAsync();
            Console.WriteLine($"เติมข้อมูลปลอมแล้ว: ผู้ขาย {made.Suppliers}, วัตถุดิบ {made.Materials}, ใบขอซื้อ {made.Requests}, ใบสั่งซื้อ {made.Orders}, การรับของ {made.Receipts}, ใบเบิก {made.Issues}, ผลประเมิน {made.Evaluations}, ตัดสต็อกวันที่ {made.CutDate:yyyy-MM-dd}");
            return 0;
        }
        catch (InvalidOperationException e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }
}

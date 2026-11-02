using System.Text;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;

namespace Lockte.Web.Seeding;

/// <summary>
/// <c>Lockte.Web.exe seed-cash-fake</c> — fills the database named by the connection string with the invented figures of the เงินสดย่อย program (#247). It adds to what is there and refuses a
/// database that already holds the set, so a test server is made again by dropping the database first (tools/uat/Reset-CashFake.ps1 does it, and only for a database whose name ends Uat/Test/Dev).
/// </summary>
internal static class SeedCashFakeCommand
{
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        Console.OutputEncoding = Encoding.UTF8;
        await LockteDatabase.MigrateAsync(services);
        var seeder = new CashFakeDataSeeder(() =>
        {
            var scope = services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<CurrentUser>().OverrideUserName = SystemUser.Name;
            return scope;
        });
        try
        {
            var made = await seeder.SeedAsync();
            Console.WriteLine($"เติมข้อมูลปลอมแล้ว: ประเภทค่าใช้จ่าย {made.ExpenseTypes}, บรรทัดค่าใช้จ่าย {made.ExpenseLines}, ผู้ซื้อ {made.Buyers}, ชนิดของเก่า {made.ScrapItems}, ใบเสร็จ {made.Receipts} (นำส่งแล้ว {made.Remitted})");
            return 0;
        }
        catch (InvalidOperationException e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }
}

using System.Text;
using Lockte.Hr;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Web;

/// <summary>
/// <c>Lockte.Web.exe round-pay-data [--apply]</c> (#392) — run once on the server by the system administrator: rounds the stored
/// เงินเดือน to whole baht (ค่าแรง/วัน keeps its satang), half up. Without <c>--apply</c> it only reports how many rows would change.
/// </summary>
internal static class RoundPayDataCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args is not ([] or ["--apply"]))
        {
            Console.Error.WriteLine("ใช้: round-pay-data [--apply]   (ไม่ใส่ --apply = ตรวจดูเฉย ๆ ไม่แก้ข้อมูล)");
            return 2;
        }
        var apply = args.Length == 1;

        await LockteDatabase.MigrateAsync(services);
        await using var scope = services.CreateAsyncScope();
        var rounding = scope.ServiceProvider.GetRequiredService<LegacyPayRounding>();
        var counts = apply ? await rounding.ApplyAsync() : await rounding.DryRunAsync();

        Console.WriteLine(apply ? "ปัดเป็นจำนวนเต็มแล้ว:" : "ตรวจดู (ยังไม่แก้ข้อมูล) แถวที่จะเปลี่ยน:");
        foreach (var count in counts) Console.WriteLine($"  {count.Column,-24} {count.Rows,6:N0} แถว");
        if (!apply) Console.WriteLine("รันอีกครั้งด้วย --apply เพื่อปัดจริง");
        return 0;
    }
}

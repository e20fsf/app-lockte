using System.Globalization;
using Lockte.Hr;
using Lockte.Migration;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Tests;

/// <summary>The comparer of the golden master on fictional rows (no database, no real data).</summary>
public sealed class PayBaselineTests
{
    private static readonly DateOnly From = new(2026, 8, 27);

    private static BaselinePay Row(string code, double payW = 5600, double payOt = 263, double w = 14, double ot = 3.5)
        => new(code, From, new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 16), w, 0, payW, payOt, 0, ot);

    private static EmployeePay Pay(string code, decimal payW = 5600, decimal payOt = 263, double w = 14, double ot = 3.5)
        => new(1, code, "นาย ทดสอบ", 400, w, 0, ot, 0, payW, payOt, 0, 0, 2);

    [Fact]
    public void Equal_results_have_no_mismatch_and_long_fields_allow_their_rounding()
        => Assert.Empty(PayBaseline.Compare(From, [Row("A001", w: 14, ot: 4)], [Pay("A001", w: 13.9375, ot: 3.5)]));

    [Fact] // AC-03: รายคนรายช่อง with both values, and an employee on one side only
    public void Every_difference_is_listed_with_both_values()
    {
        var mismatches = PayBaseline.Compare(From, [Row("A001"), Row("A002")], [Pay("A001", payOt: 262), Pay("A003")]);

        Assert.Equal(
        [
            new PayMismatch(From, "A001", "PAY_OT", "263", "262"),
            new PayMismatch(From, "A002", "แถว", "มี", "ไม่มี"),
            new PayMismatch(From, "A003", "แถว", "ไม่มี", "มี"),
        ], mismatches);
    }

    [Fact]
    public void The_baseline_csv_is_read_as_exported()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "﻿" + PayBaseline.Header + "\nd385,2026-08-27,2026-09-11,2026-09-16,14,0,5600,262.5,0,3\n");

        var row = Assert.Single(PayBaseline.Read(path));

        Assert.Equal(new BaselinePay("D385", From, new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 16), 14, 0, 5600, 262.5, 0, 3), row);
        File.Delete(path);
    }
}

/// <summary>Runs only when <see cref="PayGoldenMasterTests"/>' variables are set (real data stays on the team's PC).</summary>
public sealed class GoldenMasterFactAttribute : FactAttribute
{
    public GoldenMasterFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(PayGoldenMasterTests.SqlVariable) is null
            || Environment.GetEnvironmentVariable(PayGoldenMasterTests.BaselineVariable) is null)
            Skip = $"golden master: ตั้ง {PayGoldenMasterTests.SqlVariable} และ {PayGoldenMasterTests.BaselineVariable} เพื่อรัน";
    }
}

/// <summary>
/// Golden master (#53, AC-03/AC-04): on a database the migration tool filled from <c>source/พนักงาน.accdb</c> (a copy to
/// throw away — this writes งวด and ผลคำนวณ into it), every closed period of the baseline from
/// <see cref="FromVariable"/> on (default 2026-01-12: เวลาทำงาน is migrated from 2025, การลา from 2026, and the งวด 2 ช่วง
/// at new year is another formula) is set up if missing, calculated (เวลาทำงาน then ค่าแรง) and compared รายคนรายช่อง.
/// The mismatches go to <see cref="OutVariable"/> (default the baseline's folder). ค่าแรง/วัน and ลาออก are today's, so a
/// period before a pay rise differs for the people it raised: AC-04 asks that every difference be explained.
/// </summary>
public sealed class PayGoldenMasterTests
{
    /// <summary>Connection string of the migrated database, with Database=.</summary>
    public const string SqlVariable = "LOCKTE_GOLDEN_SQL";
    /// <summary>The CSV exported from สำเนาของ ค่าแรง (<see cref="PayBaseline.Header"/>).</summary>
    public const string BaselineVariable = "LOCKTE_PAY_BASELINE";
    public const string FromVariable = "LOCKTE_GOLDEN_FROM";
    public const string OutVariable = "LOCKTE_GOLDEN_OUT";

    [GoldenMasterFact]
    public async Task Closed_periods_recalculate_to_the_legacy_pay()
    {
        var baselinePath = Environment.GetEnvironmentVariable(BaselineVariable)!;
        var from = Environment.GetEnvironmentVariable(FromVariable) is { } f
            ? DateOnly.ParseExact(f, "yyyy-MM-dd", CultureInfo.InvariantCulture) : new DateOnly(2026, 1, 12);
        var connection = Environment.GetEnvironmentVariable(SqlVariable)!;
        await using var services = new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .AddSingleton<ICurrentUser, MigrationUser>()
            .AddOrgModule(connection)
            .AddHrModule(connection)
            .BuildServiceProvider(validateScopes: true);
        Assert.True(await HrModule.IsUpToDateAsync(services), "ฐานข้อมูลยังไม่เป็นรุ่นล่าสุด — เปิดเว็บแอปกับฐานข้อมูลนี้ก่อน");

        var mismatches = new List<PayMismatch>();
        var periods = PayBaseline.Read(baselinePath).Where(b => b.From >= from).GroupBy(b => (b.From, b.To, b.PayDate)).OrderBy(g => g.Key.From);
        var compared = 0;
        foreach (var period in periods)
        {
            await using var scope = services.CreateAsyncScope();
            var directory = scope.ServiceProvider.GetRequiredService<PayPeriodDirectory>();
            var registry = scope.ServiceProvider.GetRequiredService<PayPeriodRegistry>();
            var id = (await directory.ListAsync()).FirstOrDefault(p => p.From == period.Key.From)?.Id;
            if (id is null)
            {
                var saved = await registry.SaveAsync(new PayPeriodInput(null, period.Key.From, period.Key.To, period.Key.PayDate ?? period.Key.To, null));
                Assert.True(saved.Succeeded, string.Join(", ", saved.Errors));
                id = saved.Id;
            }
            foreach (var step in new Func<Task<ServiceResult>>[] { () => registry.CalculateTimeAsync(id!.Value), () => registry.CalculatePayAsync(id!.Value) })
            {
                var result = await step();
                Assert.True(result.Succeeded, $"{period.Key.From}: {string.Join(", ", result.Errors)}");
            }
            mismatches.AddRange(PayBaseline.Compare(period.Key.From, period, await directory.GetPayAsync(id!.Value)));
            compared += period.Count();
        }

        var outFolder = Environment.GetEnvironmentVariable(OutVariable) ?? Path.GetDirectoryName(Path.GetFullPath(baselinePath))!;
        var outFile = Path.Combine(outFolder, "pay-golden-master-mismatches.csv");
        PayBaseline.Write(outFile, mismatches);
        Assert.True(compared > 0, $"baseline ไม่มีงวดตั้งแต่ {from:yyyy-MM-dd}");
        Assert.True(mismatches.Count == 0,
            $"ไม่ตรง {mismatches.Count} ช่อง จาก {compared} แถว ({mismatches.Select(m => (m.From, m.Code)).Distinct().Count()} คน-งวด) — ดู {outFile}");
    }
}

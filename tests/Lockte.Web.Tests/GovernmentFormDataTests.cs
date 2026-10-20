using Lockte.Hr;

namespace Lockte.Web.Tests;

/// <summary>What แบบ คร.2 and ทะเบียนผู้ประกันตน read from ทะเบียนพนักงาน (#64, BR-072, BR-073).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class GovernmentFormDataTests(LocalDbFixture db)
{
    private static int lastCode;

    /// <summary>Own codes and names: the test database is shared by every test of the run.</summary>
    private static string NewCode() => $"J{Interlocked.Increment(ref lastCode):D3}";

    private async Task<int> AddAsync(EmployeeInput input)
    {
        await using var scope = db.Scope("hr1");
        var result = await scope.Get<EmployeeRegistry>().SaveAsync(input with { Code = NewCode() });
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return result.Id!.Value;
    }

    private async Task<IReadOnlyList<InsuredPersonLine>> RegisterAsync(string tag)
    {
        await using var scope = db.Scope("hr1");
        return (await scope.Get<GovernmentFormDirectory>().ListInsuredPersonsAsync()).Where(l => l.Name.EndsWith(tag)).ToList();
    }

    [Fact] // ทุกแถวรวมคนลาออก เรียงเลขบัตร (ไม่มีเลขบัตรขึ้นก่อน) แล้ววันเริ่มงาน · ค่าจ้าง = เงินเดือน ถ้า ≠ 0 ไม่อย่างนั้นค่าแรง/วัน
    public async Task The_register_lists_every_employment_by_id_then_start_with_salary_or_daily_wage()
    {
        var tag = " " + Guid.NewGuid().ToString("N")[..6];
        var id = "9" + Random.Shared.NextInt64(100_000_000_000, 999_999_999_999);
        await AddAsync(new() { Name = "นายกลับมา" + tag, NationalId = id, StartDate = new(2024, 10, 18), DailyWage = 372, Position = "พนักงาน" });
        await AddAsync(new() { Name = "นายกลับมา" + tag, NationalId = id, StartDate = new(2021, 1, 6), DailyWage = 331, ResignDate = new(2021, 1, 7) });
        await AddAsync(new() { Name = "นายไม่มีบัตร" + tag, StartDate = new(2025, 5, 1), DailyWage = 363 });
        await AddAsync(new() { Name = "นางสาวรายเดือน" + tag, NationalId = "1" + id[1..], BirthDate = new(1990, 2, 16), StartDate = new(2020, 1, 1),
            Salary = 15_000, DailyWage = 500 });

        var lines = await RegisterAsync(tag);

        Assert.Equal(["นายไม่มีบัตร", "นางสาวรายเดือน", "นายกลับมา", "นายกลับมา"], lines.Select(l => l.Name.Replace(tag, "")));
        Assert.Equal([363, 15_000, 331, 372], lines.Select(l => l.Wage));
        Assert.Equal(new DateOnly(2021, 1, 7), lines[2].ResignDate);
        Assert.Equal(("ไทย", new DateOnly(1990, 2, 16)), (lines[1].Nationality, lines[1].BirthDate));
    }

    [Fact] // แบบ คร.2: ข้อมูลของคนที่เลือก + จำนวนลูกจ้างที่ยังไม่ลาออก
    public async Task Kor2_reads_the_chosen_employee_and_counts_those_still_working()
    {
        var before = await Kor2Async(await AddAsync(new() { Name = "นายนับ ก่อน", StartDate = new(2026, 1, 1) }));
        await AddAsync(new() { Name = "นายลาออก แล้ว", StartDate = new(2026, 1, 1), ResignDate = new(2026, 2, 1) });
        var id = await AddAsync(new()
        {
            Name = "นายเด็ก ทดสอบ", BirthDate = new(2009, 3, 31), NationalId = "0000000000001", RegisteredAddress = "10 หมู่ที่ 1",
            StartDate = new(2026, 1, 16), Position = "พนักงาน", DailyWage = 363,
        });

        var kor2 = (await Kor2Async(id))!;

        Assert.Equal(("นายเด็ก ทดสอบ", new DateOnly(2009, 3, 31), "0000000000001", "10 หมู่ที่ 1", new DateOnly(2026, 1, 16), "พนักงาน", 363.0),
            (kor2.Name, kor2.BirthDate, kor2.NationalId, kor2.RegisteredAddress, kor2.StartDate, kor2.Position, kor2.DailyWage));
        Assert.True(kor2.WorkingEmployees >= before!.WorkingEmployees + 1);
        Assert.Null(await Kor2Async(int.MaxValue));
    }

    private async Task<Kor2Employee?> Kor2Async(int id)
    {
        await using var scope = db.Scope("hr1");
        return await scope.Get<GovernmentFormDirectory>().GetKor2Async(id);
    }
}

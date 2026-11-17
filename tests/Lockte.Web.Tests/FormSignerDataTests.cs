using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>
/// UAT-29 ผู้ลงนามบนแบบฟอร์ม (#409) through the public services: the names HR saves on the settings page, kept in the database with a
/// history, and the ผู้จัดการฝ่าย / ผู้จัดการโรงงาน of a แผนก on ใบลาออก. Every name here is made up.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class FormSignerDataTests(LocalDbFixture db)
{
    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private Task<FormSignersDto> LoadAsync() => AsHrAsync(s => s.Get<FormSignerSettings>().GetAsync());

    private Task<ServiceResult> SaveAsync(FormSignersInput input) => AsHrAsync(s => s.Get<FormSignerSettings>().SaveAsync(input));

    [Fact] // saved trimmed, kept, a blank is saved as none; a stale rowversion is a conflict; the history records who changed what
    public async Task The_signers_are_saved_with_a_history_and_guarded_by_the_rowversion()
    {
        var unique = Guid.NewGuid().ToString("N")[..6];
        var before = await LoadAsync();

        var first = await SaveAsync(new FormSignersInput(" นาง ", " สมมุติ" + unique + " ", "ผู้จัดการ", "ผจก.โรงงาน " + unique, null, before.RowVersion));
        Assert.True(first.Succeeded, string.Join(", ", first.Errors));
        var saved = await LoadAsync();

        Assert.Equal(("นาง", "สมมุติ" + unique, "ผู้จัดการ", "ผจก.โรงงาน " + unique, null), (saved.ManagerTitle, saved.ManagerFirstName, saved.ManagerLastName,
            saved.FactoryManagerName, saved.PersonnelOfficerName));

        var second = await SaveAsync(new FormSignersInput("นาง", "สมมุติ" + unique, "ผู้จัดการ", "  ", "เจ้าหน้าที่" + unique, saved.RowVersion));
        Assert.True(second.Succeeded, string.Join(", ", second.Errors));
        var renamed = await LoadAsync();
        Assert.Equal((null, "เจ้าหน้าที่" + unique), (renamed.FactoryManagerName, renamed.PersonnelOfficerName));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            SaveAsync(new FormSignersInput("นาย", "เก่า", "เก่า", null, null, saved.RowVersion)));

        await using var scope = db.Scope(AppUser.AdminUserName);
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "ผู้ลงนามบนแบบฟอร์ม"));
        Assert.Contains(rows, r => r.Column == "ชื่อผู้จัดการสถานประกอบกิจการ" && r.NewValue == "สมมุติ" + unique && r.UserName == "hr1");
        Assert.Contains(rows, r => r.Column == "ชื่อเจ้าหน้าที่บุคคล" && r.NewValue == "เจ้าหน้าที่" + unique);
    }

    [Fact] // UAT-38: both addresses of แบบ คร.2 are saved trimmed with the names, a blank field as none, and the history names the field
    public async Task The_addresses_are_saved_with_the_signers_and_a_history()
    {
        var unique = Guid.NewGuid().ToString("N")[..6];
        var before = await LoadAsync();
        var input = new FormSignersInput(before.ManagerTitle, before.ManagerFirstName, before.ManagerLastName, before.FactoryManagerName,
            before.PersonnelOfficerName, before.RowVersion)
        {
            Office = new("9", null, null, "ถนนสมมุติ", "แขวงสมมุติ", "เขตสมมุติ", "กรุงเทพฯ", " 0200000000 "),
            Workplace = new(" 12 ", "3", "  ", "สมมุติสาย " + unique, "ตำบลสมมุติ", "อำเภอสมมุติ", "จังหวัดสมมุติ", null),
        };

        var result = await SaveAsync(input);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        var saved = await LoadAsync();

        Assert.Equal(new FormAddress("9", null, null, "ถนนสมมุติ", "แขวงสมมุติ", "เขตสมมุติ", "กรุงเทพฯ", "0200000000"), saved.Office);
        Assert.Equal(new FormAddress("12", "3", null, "สมมุติสาย " + unique, "ตำบลสมมุติ", "อำเภอสมมุติ", "จังหวัดสมมุติ", null), saved.Workplace);

        var tooLong = await SaveAsync(input with { RowVersion = saved.RowVersion, Workplace = input.Workplace with { Road = new string('ก', FormSignerSettings.AddressLength + 1) } });
        Assert.False(tooLong.Succeeded);

        await using var scope = db.Scope(AppUser.AdminUserName);
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "ผู้ลงนามบนแบบฟอร์ม"));
        Assert.Contains(rows, r => r.Column == "ถนน สถานที่ทำงาน" && r.NewValue == "สมมุติสาย " + unique && r.UserName == "hr1");
    }

    [Fact] // a name longer than the column is refused with a message, not a database error
    public async Task A_name_that_is_too_long_is_refused()
    {
        var before = await LoadAsync();

        var result = await SaveAsync(new FormSignersInput(new string('ก', FormSignerSettings.TitleLength + 1), new string('ข', FormSignerSettings.NameLength + 1),
            null, null, null, before.RowVersion));

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Errors.Count);
    }

    /// <summary>A แผนก working กะ A with names and กะ B with none.</summary>
    private Task<(int DepartmentId, int ShiftB)> NewDepartmentAsync(string? managerA, string? factoryA) => AsHrAsync(async s =>
    {
        var org = s.Get<OrgRegistry>();
        var directory = s.Get<OrgDirectory>();
        var unique = Guid.NewGuid().ToString("N")[..6];
        var section = (await org.SaveSectionAsync(new SectionInput(null, "ฝ่าย" + unique, 1, null))).Id!.Value;
        var a = (await directory.FindShiftByCodeAsync("A"))!.Id;
        var b = (await directory.FindShiftByCodeAsync("B"))!.Id;
        var saved = await org.SaveDepartmentAsync(new DepartmentInput(null, "F" + unique, "ประกอบ" + unique, "f", 1, section, null,
            [new DepartmentShiftInput(a, "หัวหน้า เอ", managerA, factoryA), new DepartmentShiftInput(b, "หัวหน้า บี", null, null)], null));
        Assert.True(saved.Succeeded, string.Join(", ", saved.Errors));
        return (saved.Id!.Value, b);
    });

    [Fact] // UAT-29: the แผนก's manager names print for an employee of a กะ that has none of its own; the กะ's own win when set
    public async Task The_resignation_letter_falls_back_to_the_departments_other_shift_for_the_managers()
    {
        var (department, shiftB) = await NewDepartmentAsync("ผจก.ฝ่าย เอ", "ผจก.โรงงาน เอ");
        var code = "F" + Interlocked.Increment(ref lastCode).ToString("D3");
        var saved = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = code, Name = "นางสาว ทองใส", DepartmentId = department, ShiftId = shiftB, StartDate = new DateOnly(1992, 10, 26),
        }));
        Assert.True(saved.Succeeded, string.Join(", ", saved.Errors));

        var letter = (await AsHrAsync(s => s.Get<HrDocumentDirectory>().GetResignationLetterAsync(saved.Id!.Value, new DateOnly(2026, 10, 2), null)))!;

        Assert.Equal(("B", "หัวหน้า บี", "ผจก.ฝ่าย เอ", "ผจก.โรงงาน เอ"), (letter.ShiftCode, letter.SupervisorName, letter.ManagerName, letter.FactoryManagerName));
    }

    private static int lastCode = 700;
}

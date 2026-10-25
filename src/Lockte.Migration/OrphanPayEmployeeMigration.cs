using System.Globalization;
using Lockte.Hr;
using Lockte.Org;

namespace Lockte.Migration;

/// <summary>
/// พนักงานตัวแทนของรหัสกำพร้าที่มีผลค่าแรง (#51, DQ-6): a code in ค่าแรง / สำเนาของ ค่าแรง that is no longer in EMPLO (the
/// employee was deleted, BR-043) but was paid in a period that moves. Access printed such a row in RPT-09 because it did not
/// join EMPLO, so it is loaded as a stand-in who has resigned, and the totals match. Run after <see cref="MasterDataMigration"/>
/// and before <see cref="PayHistoryMigration"/>. The stand-in has no name (the code is the only thing known), the resign date
/// of its last period's TO_DATE, ประเภทรายวัน and the แผนก / ฝ่าย of its code's first letter (blank when none or several match).
/// The stand-in is marked (<c>IsMigrationStub</c>) and hidden from every list, form and government register, as the legacy had no row for it (decided 2026-10-25); only the pay results count it. Rows of other tables of the same code (ADD_EW, LEAVE_ADD, ใบเตือนADD) are not loaded with it, as before. Nothing is written
/// to the change history.
/// </summary>
public sealed class OrphanPayEmployeeMigration(HrStubEmployeeImport import, OrgDirectory org)
{
    private const string Orphan = "DQ-6";
    private const string StandInNote = "พนักงานตัวแทนจากการย้ายข้อมูล: รหัสนี้ถูกลบจากทะเบียนเดิมแต่มีผลค่าแรง (#51)";
    public const string Created = "สร้างเป็นพนักงานที่ลาออกแล้ว (ตัวแทน)";
    public const string CountTable = "รหัสกำพร้าใน ค่าแรง + สำเนาของ ค่าแรง";

    /// <summary>The name of a stand-in: unknown, with its code.</summary>
    public static string NameOf(string code) => $"(ไม่ทราบชื่อ) {code}";

    public async Task RunAsync(LegacyDatabase legacy, MigrationReport report)
    {
        var known = (await import.GetEmployeeIdsAsync()).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphans = new SortedDictionary<string, OrphanPay>(StringComparer.Ordinal);
        foreach (var table in new[] { PayHistoryMigration.CurrentTable, PayHistoryMigration.HistoryTable })
        {
            foreach (var row in legacy[table])
            {
                if (row.Date("FROM_DATE") is not { } from || row.Date("TO_DATE") is not { } to || row.Date("PAY_D") is null
                    || EmployeeFormRules.NormalizeCode(row.Text("ID")) is not { } code || known.Contains(code)) continue;
                var last = DateOnly.FromDateTime(to >= from ? to : from);
                orphans[code] = orphans.TryGetValue(code, out var seen) ? seen with { Rows = seen.Rows + 1, Resigned = Max(seen.Resigned, last) } : new OrphanPay(1, last);
            }
        }

        var stubs = new List<StubEmployeeImport>();
        foreach (var (code, orphan) in orphans)
        {
            var key = $"{code} ({orphan.Rows} แถว, ลาออก {orphan.Resigned.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})";
            if (code.Length > EmployeeRegistry.CodeLength)
            {
                report.Add("EMPLO", key, Orphan, $"รหัสยาวเกิน {EmployeeRegistry.CodeLength} ตัว", MigrationReport.NotLoaded);
                continue;
            }
            var departments = await org.FindDepartmentsByEmployeeIdPrefixAsync(code);
            var department = departments.Count == 1 ? departments[0] : null;
            stubs.Add(new StubEmployeeImport(code, NameOf(code), department?.Id, department?.SectionId, EmployeeTypes.Daily, orphan.Resigned, StandInNote));
            report.Add("EMPLO", key, Orphan, "รหัสไม่อยู่ในทะเบียนพนักงานที่ย้ายแล้ว แต่มีผลค่าแรงในงวดที่ย้าย (แถวกำพร้า)",
                department is null ? $"{Created} · ไม่มีแผนก" : $"{Created} · แผนก {department.Name}");
        }
        await import.AddAsync(stubs);
        report.Tables.Add(new TableCount(CountTable, "hr.Employees (ตัวแทน)", orphans.Count, 0, orphans.Count - stubs.Count, stubs.Count,
            await import.CountAsync()));
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private sealed record OrphanPay(int Rows, DateOnly Resigned);
}

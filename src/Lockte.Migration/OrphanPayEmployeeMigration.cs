using System.Globalization;
using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Migration;

/// <summary>
/// พนักงานตัวแทนของรหัสกำพร้าที่มีผลค่าแรง (#51, DQ-6): a code in ค่าแรง / สำเนาของ ค่าแรง that is no longer in EMPLO (the
/// employee was deleted, BR-043) but was paid in a period that moves. Access printed such a row in RPT-09 because it did not
/// join EMPLO, so it is loaded as a stand-in who has resigned, and the totals match. Run after <see cref="MasterDataMigration"/>
/// and before <see cref="PayHistoryMigration"/>. The stand-in has no name (the code is the only thing known), the resign date
/// of its last period's TO_DATE, ประเภทรายวัน and the แผนก / ฝ่าย of its code's first letter (blank when none or several match).
/// Rows of other tables of the same code (ADD_EW, LEAVE_ADD, ใบเตือนADD) are not loaded with it, as before. Nothing is written
/// to the change history.
/// </summary>
public sealed class OrphanPayEmployeeMigration(EmployeeRegistry registry, EmployeeDirectory employees, OrgDirectory org, AuditScope audit)
{
    private const string Orphan = "DQ-6";
    private const string StandInNote = "พนักงานตัวแทนจากการย้ายข้อมูล: รหัสนี้ถูกลบจากทะเบียนเดิมแต่มีผลค่าแรง (#51)";
    public const string Created = "สร้างเป็นพนักงานที่ลาออกแล้ว (ตัวแทน)";
    public const string CountTable = "รหัสกำพร้าใน ค่าแรง + สำเนาของ ค่าแรง";

    /// <summary>The name of a stand-in: unknown, with its code.</summary>
    public static string NameOf(string code) => $"(ไม่ทราบชื่อ) {code}";

    public async Task RunAsync(LegacyDatabase legacy, MigrationReport report)
    {
        var known = (await employees.SearchAsync()).Select(e => e.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
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

        var created = 0;
        using (audit.SuppressRowChanges())
        {
            foreach (var (code, orphan) in orphans)
            {
                var key = $"{code} ({orphan.Rows} แถว, ลาออก {orphan.Resigned.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})";
                var departments = code.Length <= EmployeeRegistry.CodeLength ? await org.FindDepartmentsByEmployeeIdPrefixAsync(code) : [];
                var department = departments.Count == 1 ? departments[0] : null;
                var saved = await registry.SaveAsync(new EmployeeInput
                {
                    Code = code, Name = NameOf(code), DepartmentId = department?.Id, SectionId = department?.SectionId,
                    EmployeeType = EmployeeTypes.Daily, ResignDate = orphan.Resigned, Memo = StandInNote,
                });
                if (saved.Succeeded)
                {
                    created++;
                    report.Add("EMPLO", key, Orphan, "รหัสไม่อยู่ในทะเบียนพนักงานที่ย้ายแล้ว แต่มีผลค่าแรงในงวดที่ย้าย (แถวกำพร้า)",
                        department is null ? $"{Created} · ไม่มีแผนก" : $"{Created} · แผนก {department.Name}");
                }
                else report.Add("EMPLO", key, Orphan, string.Join("; ", saved.Errors), MigrationReport.NotLoaded);
            }
        }
        var now = (await employees.SearchAsync()).Select(e => e.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inDatabase = orphans.Keys.Count(now.Contains);
        report.Tables.Add(new TableCount(CountTable, "hr.Employees (ตัวแทน)", orphans.Count, 0, orphans.Count - created, created, inDatabase));
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private sealed record OrphanPay(int Rows, DateOnly Resigned);
}

using System.Globalization;
using Lockte.Hr;
using Lockte.Org;

namespace Lockte.Migration;

/// <summary>
/// ย้ายข้อมูลใบเตือน (#51, #59): ใบเตือน (the current round) and ใบเตือนADD (every round before, BR-040) into one table,
/// after <see cref="MasterDataMigration"/> has loaded the employees and แผนก. Every year is moved (345 rows): BR-067 and
/// RPT-30 choose them by date. A row without a date or an employee — the blank rows the form left (18 of 44) — is not
/// loaded; DEPART is matched to a แผนก by name (blank when there is none), CODE taken as it is (blank allowed, DQ-9).
/// Nothing is written to the change history.
/// </summary>
public sealed class WarningMigration(HrWarningImport import, EmployeeDirectory employees, OrgDirectory org)
{
    private const string Other = "NF-06";
    private const string Orphan = "DQ-6";
    private const string NewTable = "hr.Warnings";

    /// <summary>The legacy tables, the current one first.</summary>
    public static IReadOnlyList<string> Tables { get; } = ["ใบเตือน", "ใบเตือนADD"];

    public async Task RunAsync(LegacyDatabase legacy, MigrationReport report)
    {
        var employeeIds = (await employees.SearchAsync()).ToDictionary(e => e.Code, e => e.Id, StringComparer.OrdinalIgnoreCase);
        var departments = (await org.GetDepartmentsAsync()).GroupBy(d => d.Name).ToDictionary(g => g.Key, g => g.First().Id);
        var rows = new List<WarningImport>();
        var (source, rejected) = (0, 0);
        foreach (var table in Tables)
        {
            foreach (var row in legacy[table])
            {
                source++;
                var code = EmployeeFormRules.NormalizeCode(row.Text("ID"));
                if (row.Date("DATE") is not { } date || code is null)
                {
                    rejected++;
                    report.Add(table, $"{row.Date("DATE")?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} {code}".Trim(), Other,
                        "ไม่มีวันที่หรือรหัสพนักงาน (แถวว่างที่ฟอร์มทิ้งไว้)", MigrationReport.NotLoaded);
                    continue;
                }
                var key = $"{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} {code}";
                if (!employeeIds.TryGetValue(code, out var employeeId))
                {
                    rejected++;
                    report.Add(table, key, Orphan, "ไม่พบรหัสพนักงานนี้ในทะเบียนพนักงานที่ย้ายแล้ว (แถวกำพร้า)", MigrationReport.NotLoaded);
                    continue;
                }
                int? departmentId = null;
                if (row.Text("DEPART") is { } depart && !departments.TryGetValue(depart, out var id))
                    report.Add(table, key, Other, $"แผนก '{depart}' ไม่พบในแผนกที่ย้ายแล้ว", MigrationReport.LoadedBlank);
                else if (row.Text("DEPART") is { } name) departmentId = departments[name];
                var level = row.Text("CODE");
                if (level is not null && !WarningLevels.Names.ContainsKey(level))
                {
                    report.Add(table, key, Other, $"ระดับ '{level}' ไม่อยู่ใน T-ชื่อใบเตือน", MigrationReport.LoadedBlank);
                    level = null;
                }
                var remark = row.Text("REMARK");
                if (remark?.Length > WarningRegistry.RemarkLength) remark = remark[..WarningRegistry.RemarkLength];
                rows.Add(new WarningImport(DateOnly.FromDateTime(date), employeeId, departmentId, level, remark));
            }
        }
        await import.AddWarningsAsync(rows);
        report.Tables.Add(new TableCount(string.Join(" + ", Tables), NewTable, source, 0, rejected, rows.Count, await import.CountWarningsAsync()));
    }
}

using System.Text;
using System.Text.RegularExpressions;
using Lockte.Calib.Data;
using Lockte.Org;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Calib;

/// <summary>ชนิดเครื่องมือวัด: the six legacy tables (data-model) as values of one column.</summary>
public enum InstrumentKind
{
    /// <summary>สอบเทียบภายนอก (ตาราง ภายนอก).</summary>
    External = 1,
    /// <summary>เวอร์เนีย/ไฮเกจ/ไมโครมิเตอร์ (ตาราง CALIPER).</summary>
    Caliper = 2,
    /// <summary>เพรสเซอร์เกจ.</summary>
    PressureGauge = 3,
    /// <summary>เครื่องชั่ง.</summary>
    Scale = 4,
    /// <summary>JIG.</summary>
    Jig = 5,
    /// <summary>ตัวควบคุมอุณหภูมิ.</summary>
    TempControl = 6,
}

/// <summary>An employee of module hr as calibration needs it (ผู้สอบเทียบ); the register is read through <see cref="ICalibEmployeeLookup"/> (ADR 0002).</summary>
public sealed record CalibEmployeeRef(int Id, string Code, string Name);

/// <summary>Implemented by module พนักงานและค่าแรง; this module does not refer to it.</summary>
public interface ICalibEmployeeLookup
{
    Task<CalibEmployeeRef?> FindAsync(int employeeId);
    Task<IReadOnlyList<CalibEmployeeRef>> SearchAsync(string? text, int take);
}

/// <summary>เครื่องมือวัด as other modules, the screens and the reports read it. <see cref="DepartmentText"/> is the legacy's <c>DC (D02)</c>.</summary>
public sealed record InstrumentDto(
    int Id,
    InstrumentKind Kind,
    string Code,
    string? Name,
    DateOnly? RegisteredOn,
    string? Maker,
    int? DepartmentId,
    string? DepartmentCode,
    string? DepartmentName,
    IReadOnlyList<int> MachineIds,
    IReadOnlyList<string> MachineCodes,
    string? Range,
    string? Resolution,
    string? Limit,
    string? Unit,
    string? StandardUsed,
    int? CalibratorEmployeeId,
    string? CalibratorName,
    string? Axis,
    string? ManualNo,
    string? Note,
    byte[] RowVersion)
{
    /// <summary>แผนกที่ใช้งาน as the registers print it: the department code, then its machines in brackets — <c>DC (D02)</c>, <c>BL (32,33)</c>.</summary>
    public string DepartmentText
        => (DepartmentCode ?? DepartmentName ?? "") + (MachineCodes.Count > 0 ? $" ({string.Join(",", MachineCodes)})" : "");
}

/// <summary>The fields of SCR-02/03. <see cref="Id"/> null = new instrument; editing needs <see cref="RowVersion"/> as loaded.</summary>
public sealed record InstrumentInput(
    int? Id,
    InstrumentKind Kind,
    string Code,
    byte[]? RowVersion,
    string? Name = null,
    DateOnly? RegisteredOn = null,
    string? Maker = null,
    int? DepartmentId = null,
    IReadOnlyList<int>? MachineIds = null,
    string? Range = null,
    string? Resolution = null,
    string? Limit = null,
    string? Unit = null,
    string? StandardUsed = null,
    int? CalibratorEmployeeId = null,
    string? Axis = null,
    string? ManualNo = null,
    string? Note = null);

/// <summary>Values already used in one kind, for the dropdowns that suggest them (BR-005), each A–Z.</summary>
public sealed record InstrumentChoices(
    IReadOnlyList<string> Names,
    IReadOnlyList<string> Makers,
    IReadOnlyList<string> Units,
    IReadOnlyList<string> Axes,
    IReadOnlyList<string> ManualNos,
    IReadOnlyList<string> Limits,
    IReadOnlyList<string> StandardsUsed);

/// <summary>
/// เพิ่ม/แก้/ลบ ทะเบียนเครื่องมือวัด (SCR-02, SCR-03, BR-001…004). Unlike the legacy the code is required and cannot repeat
/// (#17, Q-22), where the legacy silently deleted code-less rows; a delete is a button with a confirmation. A newer save by
/// someone else throws <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class InstrumentRegistry(CalibDbContext db, OrgDirectory org, ICalibEmployeeLookup employees)
{
    public const int CodeLength = 100;
    public const int TextLength = 255;
    public const int ShortTextLength = 100;

    public async Task<ServiceResult> SaveAsync(InstrumentInput input)
    {
        var code = Clean(input.Code);
        var errors = new List<string>();
        if (!Enum.IsDefined(input.Kind)) errors.Add("ไม่รู้จักชนิดเครื่องมือวัด");
        if (code is null) errors.Add("กรุณากรอกหมายเลขเครื่อง");
        else if (code.Length > CodeLength) errors.Add($"หมายเลขเครื่องยาวได้ไม่เกิน {CodeLength} ตัว");
        else
        {
            var upper = code.ToUpperInvariant();
            var other = await db.Instruments.AsNoTracking().Where(i => i.Code.ToUpper() == upper && i.Id != input.Id)
                .Select(i => new { i.Code, i.Kind }).FirstOrDefaultAsync();
            if (other is not null) errors.Add($"หมายเลขเครื่อง '{code}' ซ้ำกับเครื่อง {other.Code} ({KindName(other.Kind)})");
        }

        var texts = new (string? Value, string Label, int Max)[]
        {
            (input.Name, "ชื่อเครื่องมือวัด", TextLength), (input.Maker, "ยี่ห้อ/ผู้ผลิต", TextLength), (input.Range, "พิกัดใช้งาน", TextLength),
            (input.Resolution, "ค่าความละเอียด", ShortTextLength), (input.Limit, "ขีดจำกัดที่ยอมรับได้", ShortTextLength), (input.Unit, "หน่วย", ShortTextLength),
            (input.StandardUsed, "หมายเลขเครื่องมือที่ใช้", TextLength), (input.Axis, "แกน (mm)", TextLength), (input.ManualNo, "คู่มือวิธีการที่ใช้เลขที่", ShortTextLength),
            (input.Note, "หมายเหตุ", TextLength),
        };
        errors.AddRange(texts.Where(t => t.Value?.Trim().Length > t.Max).Select(t => $"{t.Label}ยาวได้ไม่เกิน {t.Max} ตัว"));

        var machineIds = (input.MachineIds ?? []).Distinct().ToList();
        if (input.DepartmentId is { } departmentId)
        {
            if (await org.GetDepartmentAsync(departmentId) is null) errors.Add("ไม่พบแผนกที่เลือก");
            else if (machineIds.Count > 0)
            {
                var ofDepartment = (await org.GetMachinesAsync()).Where(m => m.DepartmentId == departmentId).Select(m => m.Id).ToHashSet();
                if (machineIds.Any(id => !ofDepartment.Contains(id))) errors.Add("เครื่องจักรที่เลือกต้องเป็นของแผนกที่ใช้งาน");
            }
        }
        else if (machineIds.Count > 0) errors.Add("เลือกเครื่องจักรได้เมื่อเลือกแผนกแล้ว");
        if (input.CalibratorEmployeeId is { } employeeId && await employees.FindAsync(employeeId) is null) errors.Add("ไม่พบผู้สอบเทียบที่เลือกในทะเบียนพนักงาน");
        if (errors.Count > 0) return new ServiceResult(false, errors);

        var instrument = await LoadAsync(input.Id, input.RowVersion);
        if (input.Id is not null && instrument is null) return ServiceResult.Fail("ไม่พบเครื่องมือวัดนี้");
        instrument ??= db.Instruments.Add(new Instrument { Kind = input.Kind }).Entity;
        if (instrument.Kind != input.Kind) return ServiceResult.Fail("เปลี่ยนชนิดของเครื่องมือวัดไม่ได้");
        instrument.Code = code!;
        instrument.Name = Clean(input.Name);
        instrument.RegisteredOn = input.RegisteredOn;
        instrument.Maker = Clean(input.Maker);
        instrument.DepartmentId = input.DepartmentId;
        instrument.Range = Clean(input.Range);
        instrument.Resolution = Clean(input.Resolution);
        instrument.Limit = Clean(input.Limit);
        instrument.Unit = Clean(input.Unit);
        instrument.StandardUsed = Clean(input.StandardUsed);
        instrument.CalibratorEmployeeId = input.CalibratorEmployeeId;
        instrument.Axis = Clean(input.Axis);
        instrument.ManualNo = Clean(input.ManualNo);
        instrument.Note = Clean(input.Note);

        foreach (var old in instrument.Machines.Where(m => !machineIds.Contains(m.MachineId)).ToList()) db.InstrumentMachines.Remove(old);
        foreach (var id in machineIds.Where(id => instrument.Machines.All(m => m.MachineId != id)))
            instrument.Machines.Add(new InstrumentMachine { MachineId = id });
        await db.SaveChangesAsync();
        return ServiceResult.Ok(instrument.Id);
    }

    /// <summary>
    /// ลบรายการนี้ (BR-004 as a button with a confirmation; ประวัติการแก้ไข keeps who deleted it). Deleting a row that is already gone succeeds.
    /// </summary>
    public async Task<ServiceResult> DeleteAsync(int id, byte[] loadedRowVersion)
    {
        if (await LoadAsync(id, loadedRowVersion) is not { } instrument) return ServiceResult.Ok();
        db.InstrumentMachines.RemoveRange(instrument.Machines);
        db.Instruments.Remove(instrument);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private async Task<Instrument?> LoadAsync(int? id, byte[]? loadedRowVersion)
    {
        if (id is not { } instrumentId) return null;
        var instrument = await db.Instruments.Include(i => i.Machines).FirstOrDefaultAsync(i => i.Id == instrumentId);
        instrument?.ExpectRowVersion(loadedRowVersion
            ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(loadedRowVersion)));
        return instrument;
    }

    internal static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    internal static string KindName(InstrumentKind kind) => kind switch
    {
        InstrumentKind.External => "สอบเทียบภายนอก",
        InstrumentKind.Caliper => "เวอร์เนีย/ไฮเกจ/ไมโครมิเตอร์",
        InstrumentKind.PressureGauge => "เพรสเซอร์เกจ",
        InstrumentKind.Scale => "เครื่องชั่ง",
        InstrumentKind.Jig => "JIG",
        InstrumentKind.TempControl => "ตัวควบคุมอุณหภูมิ",
        _ => kind.ToString(),
    };
}

/// <summary>The public, read-only face of ทะเบียนเครื่องมือวัด (ADR 0002). Writes go through <see cref="InstrumentRegistry"/>.</summary>
public sealed class InstrumentDirectory(CalibDbContext db, OrgDirectory org, ICalibEmployeeLookup employees)
{
    /// <summary>ชื่อของชนิด as the menus and headings spell it.</summary>
    public static string KindName(InstrumentKind kind) => InstrumentRegistry.KindName(kind);

    /// <summary>All instruments of one kind, by หมายเลขเครื่อง.</summary>
    public async Task<IReadOnlyList<InstrumentDto>> ListAsync(InstrumentKind kind)
        => await ToDtosAsync(await Query().Where(i => i.Kind == kind).OrderBy(i => i.Code).ToListAsync());

    public async Task<InstrumentDto?> GetAsync(int id)
        => await Query().FirstOrDefaultAsync(i => i.Id == id) is { } i ? (await ToDtosAsync([i])).Single() : null;

    /// <summary>The instrument with exactly this code (not case sensitive), of any kind.</summary>
    public async Task<InstrumentDto?> FindByCodeAsync(string code)
    {
        var upper = code.Trim().ToUpperInvariant();
        return await Query().FirstOrDefaultAsync(i => i.Code.ToUpper() == upper) is { } i ? (await ToDtosAsync([i])).Single() : null;
    }

    /// <summary>
    /// The instruments of one kind whose code matches <paramref name="pattern"/> the way Access's <c>Like</c> did (BR-010 ข้อ 4):
    /// <c>*</c> any characters, <c>?</c> one character, <c>#</c> one digit, case ignored, no wildcard = the whole code. By หมายเลขเครื่อง.
    /// A blank pattern matches nothing.
    /// </summary>
    public async Task<IReadOnlyList<InstrumentDto>> FindByPatternAsync(InstrumentKind kind, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return [];
        var like = InstrumentPattern.ToRegex(pattern.Trim());
        var rows = await Query().Where(i => i.Kind == kind).OrderBy(i => i.Code).ToListAsync();
        return await ToDtosAsync(rows.Where(i => like.IsMatch(i.Code)).ToList());
    }

    /// <summary>The values already used in one kind, for the suggestions of BR-005.</summary>
    public async Task<InstrumentChoices> GetChoicesAsync(InstrumentKind kind)
    {
        var rows = await db.Instruments.AsNoTracking().Where(i => i.Kind == kind)
            .Select(i => new { i.Name, i.Maker, i.Unit, i.Axis, i.ManualNo, i.Limit, i.StandardUsed }).ToListAsync();
        static IReadOnlyList<string> Distinct(IEnumerable<string?> values)
            => values.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCulture).ToList();
        return new InstrumentChoices(Distinct(rows.Select(r => r.Name)), Distinct(rows.Select(r => r.Maker)), Distinct(rows.Select(r => r.Unit)),
            Distinct(rows.Select(r => r.Axis)), Distinct(rows.Select(r => r.ManualNo)), Distinct(rows.Select(r => r.Limit)), Distinct(rows.Select(r => r.StandardUsed)));
    }

    /// <summary>ผู้สอบเทียบ to choose from: employees of the register matching <paramref name="text"/>.</summary>
    public Task<IReadOnlyList<CalibEmployeeRef>> SearchCalibratorsAsync(string? text, int take = 50) => employees.SearchAsync(text, take);

    private IQueryable<Instrument> Query() => db.Instruments.AsNoTracking().Include(i => i.Machines);

    private async Task<IReadOnlyList<InstrumentDto>> ToDtosAsync(IReadOnlyList<Instrument> rows)
    {
        if (rows.Count == 0) return [];
        var departments = (await org.GetDepartmentsAsync()).ToDictionary(d => d.Id);
        var machines = (await org.GetMachinesAsync()).ToDictionary(m => m.Id);
        var names = new Dictionary<int, string>();
        foreach (var id in rows.Select(r => r.CalibratorEmployeeId).OfType<int>().Distinct())
            if (await employees.FindAsync(id) is { } e) names[id] = e.Name;
        return rows.Select(i =>
        {
            var department = i.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null;
            var machineRows = i.Machines.Select(m => machines.GetValueOrDefault(m.MachineId)).OfType<MachineDto>().OrderBy(m => m.Code, StringComparer.CurrentCulture).ToList();
            return new InstrumentDto(i.Id, i.Kind, i.Code, i.Name, i.RegisteredOn, i.Maker, i.DepartmentId, department?.Code, department?.Name,
                machineRows.Select(m => m.Id).ToList(), machineRows.Select(m => m.Code).ToList(), i.Range, i.Resolution, i.Limit, i.Unit, i.StandardUsed,
                i.CalibratorEmployeeId, i.CalibratorEmployeeId is { } e ? names.GetValueOrDefault(e) : null, i.Axis, i.ManualNo, i.Note, i.RowVersion);
        }).ToList();
    }
}

/// <summary>Access's <c>Like</c> for หมายเลขเครื่อง (BR-010): <c>*</c> <c>?</c> <c>#</c>, everything else literal, case ignored.</summary>
public static class InstrumentPattern
{
    public static Regex ToRegex(string pattern)
    {
        var sb = new StringBuilder("^");
        foreach (var c in pattern)
            sb.Append(c switch { '*' => ".*", '?' => ".", '#' => "[0-9]", _ => Regex.Escape(c.ToString()) });
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    }

    public static bool IsMatch(string pattern, string code) => ToRegex(pattern).IsMatch(code);
}

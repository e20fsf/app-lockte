namespace Lockte.Hr;

/// <summary>
/// One employee as the onboarding documents print them (RPT-56 แบบฟอร์มรับพนักงาน / สัญญาจ้าง, RPT-22 ใบอนุมัติผ่านทดลองงาน,
/// BR-072, BR-071): the legacy query <c>แบบฟอร์มการรับพนักงาน10</c> — EMPLO with the names its แผนก row prints for the
/// employee's กะ (SUPER, MANAGER, FACTORY MANAGER). The legacy found nothing when the กะ had no แผนก row (⚠ BR-072);
/// here the document still prints, with those names blank.
/// </summary>
public sealed record OnboardingEmployee
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public DateOnly? BirthDate { get; init; }
    public string? NationalId { get; init; }
    public string? IdCardPlace { get; init; }
    public DateOnly? IdCardIssued { get; init; }
    /// <summary>ที่อยู่ทะเบียนบ้าน (PER_ADD) — "อยู่บ้านเลขที่" on the contract.</summary>
    public string? RegisteredAddress { get; init; }
    public DateOnly? StartDate { get; init; }
    public int? EmployeeType { get; init; }
    public double Salary { get; init; }
    public double DailyWage { get; init; }
    public string? Position { get; init; }
    public string? DepartmentName { get; init; }
    public string? SectionName { get; init; }
    public string? ShiftCode { get; init; }
    /// <summary>หัวหน้ากะ (SUPER) — ผู้สัมภาษณ์ on the hiring form.</summary>
    public string? SupervisorName { get; init; }
    /// <summary>ผู้จัดการฝ่าย (MANAGER).</summary>
    public string? ManagerName { get; init; }
    /// <summary>ผู้จัดการโรงงาน (FACTORY MANAGER) — ผู้อนุมัติ / ผู้ว่าจ้าง.</summary>
    public string? FactoryManagerName { get; init; }

    /// <summary>วันครบ 90 วัน = START + 89 (BR-053) — the end of ระยะทดลองงาน on the hiring form and both contracts.</summary>
    public DateOnly? NinetyDayDate => StartDate is { } s ? Probation.NinetyDayDate(s) : null;

    /// <summary>สัญญาจ้างรายเดือน is for ประเภท รายเดือน; every other employee gets สัญญาจ้างรายวัน (the legacy pickers: CLAS = "1" / ≠ "1").</summary>
    public bool IsMonthly => EmployeeType == EmployeeTypes.Monthly;

    /// <summary>อายุ on the contract: <c>(Date() − BORN) / 365</c> shown with no decimals (Format Fixed 0 rounds half away from zero).</summary>
    public int? AgeOn(DateOnly today)
        => BirthDate is { } born ? (int)Math.Round((today.DayNumber - born.DayNumber) / 365.0, MidpointRounding.AwayFromZero) : null;
}

/// <summary>
/// สถิติการลางานและใบเตือน of one employee for ใบประเมินทดลองงาน (BR-071, macro <c>ใบอนุมัติบรรจุพนักงาน</c>): days of การลา
/// SR 1, 2, 3, 4, 5, 8 and the times late, over all the employee's rows as the legacy summed them. The two warning
/// counts are null until ใบเตือน is kept by the system (#59) — the form then leaves them, and the total, to be written in.
/// </summary>
public sealed record ProbationStatistics(
    double PersonalLeave,
    double SickLeaveWithCertificate,
    double SickLeaveWithoutCertificate,
    double MaternityLeave,
    double WorkAccidentLeave,
    int LateCount,
    double Absent,
    int? VerbalWarnings,
    int? WrittenWarnings)
{
    /// <summary>คะแนนหักต่อหน่วย as printed in ตัวคูณ/หน่วย (the report's ControlSource, BR-071).</summary>
    public static class Points
    {
        public const int PersonalLeave = 2;
        public const int SickLeaveWithCertificate = 1;
        public const int SickLeaveWithoutCertificate = 2;
        public const int MaternityLeave = 2;
        public const int WorkAccidentLeave = 0;
        public const int Late = 1;
        public const int Absent = 3;
        public const int VerbalWarning = 5;
        public const int WrittenWarning = 10;
    }

    /// <summary>
    /// คะแนนหักรวม (2) = ลากิจ×(−2) + ลาป่วยมีใบแพทย์×(−1) + ลาป่วยไม่มีใบแพทย์×(−2) + ลาคลอด×(−2) + อุบัติเหตุในงาน×0 +
    /// มาสาย×(−1) + ขาดงาน×(−3) + ใบเตือนวาจา×(−5) + ใบเตือนหนังสือ×(−10) — negative, as printed. Null while the warnings
    /// are unknown.
    /// </summary>
    public double? TotalDeduction => VerbalWarnings is { } verbal && WrittenWarnings is { } written
        ? -(PersonalLeave * Points.PersonalLeave + SickLeaveWithCertificate * Points.SickLeaveWithCertificate
            + SickLeaveWithoutCertificate * Points.SickLeaveWithoutCertificate + MaternityLeave * Points.MaternityLeave
            + WorkAccidentLeave * Points.WorkAccidentLeave + LateCount * Points.Late + Absent * Points.Absent
            + verbal * Points.VerbalWarning + written * Points.WrittenWarning)
        : null;
}

/// <summary>A line of รายชื่อพนักงานแผนก… (RPT-21, query <c>รายชื่อเฉพาะแผนก7</c>).</summary>
/// <param name="ServiceYears">อายุงาน(ปี) = Int(DateDiff("m", START, Now) / 12).</param>
/// <param name="ServiceMonths">(เดือน) = DateDiff("m", START, Now) Mod 12.</param>
/// <param name="Age">อายุ = Int(DateDiff("m", BORN, Now) / 12).</param>
public sealed record DepartmentRosterLine(
    string Code, string Name, DateOnly? StartDate, string? Position, int? ServiceYears, int? ServiceMonths, int? Age,
    double Salary, double DailyWage);

/// <summary>
/// รายชื่อพนักงานแผนก… (RPT-21, BR-073): employees still working in a แผนก and กะ (blank = all), by รหัส, with the
/// counts of ชาย / หญิง and the average อายุ in the footer.
/// </summary>
public sealed record DepartmentRoster(string Title, IReadOnlyList<DepartmentRosterLine> Lines, int Male, int Female)
{
    /// <summary>อายุเฉลี่ย = Avg(อายุ) of the lines that have one; null when none has.</summary>
    public double? AverageAge => Lines.Any(l => l.Age is not null) ? Lines.Where(l => l.Age is not null).Average(l => (double)l.Age!.Value) : null;
}

/// <summary>A line of รายชื่อพนักงานทั้งหมด (RPT-21 report <c>EMPLO</c>, query <c>พิมพ์รายชื่อทั้งหมด</c>).</summary>
/// <param name="Age">อายุ = (Now − BORN) / 365, unrounded (the report shows no decimals).</param>
public sealed record WorkingEmployeeLine(
    string Code, string Name, string? DepartmentName, string? Position, DateOnly? BirthDate, DateOnly? StartDate, int? EmployeeType,
    double? Age, string? Hospital);

/// <summary>รายชื่อพนักงานทั้งหมด: everyone still working by รหัส, with ชาย / หญิง and the average อายุ.</summary>
public sealed record WorkingEmployeeList(IReadOnlyList<WorkingEmployeeLine> Lines, int Male, int Female)
{
    public double? AverageAge => Lines.Any(l => l.Age is not null) ? Lines.Where(l => l.Age is not null).Average(l => l.Age!.Value) : null;
}

/// <summary>A line of ทะเบียนประวัติพนักงาน (RPT-21, query <c>ทะเบียนประวัติพนักงาน</c>): every employee, those who resigned too.</summary>
/// <param name="Sex">เพศ as a word: หญิง / ชาย (blank when not recorded — the legacy printed ชาย for it).</param>
/// <param name="Wage">เงินเดือนหรือค่าแรง/วัน = เงินเดือน ถ้าไม่เป็น 0 ไม่อย่างนั้น ค่าแรง/วัน.</param>
public sealed record EmployeeRegisterLine(
    string Code, string Name, DateOnly? BirthDate, string? Sex, string Nationality, string? RegisteredAddress, string? CurrentAddress,
    string? Education, DateOnly? StartDate, string? Position, string? DepartmentName, double Wage, DateOnly? ResignDate);

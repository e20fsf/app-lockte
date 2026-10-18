namespace Lockte.Hr;

/// <summary>ประเภทพนักงาน (CLAS, T-ประเภทพนักงาน).</summary>
public static class EmployeeTypes
{
    public const int Monthly = 1;
    public const int Daily = 2;

    public static IReadOnlyDictionary<int, string> Names { get; } = new Dictionary<int, string>
    {
        [Monthly] = "รายเดือน",
        [Daily] = "รายวัน",
    };
}

/// <summary>Values of the SCR-02/03 dropdowns that come from the legacy lookup tables (data-model "ข้อมูลอ้างอิง").</summary>
public static class EmployeeLookups
{
    /// <summary>T-เพศ.</summary>
    public static IReadOnlyDictionary<string, string> Sexes { get; } = new Dictionary<string, string> { ["F"] = "หญิง", ["M"] = "ชาย" };

    /// <summary>T-TITLE (English titles, for the KBank file).</summary>
    public static IReadOnlyList<string> EnglishTitles { get; } = ["Mr.", "Mrs.", "Miss"];

    /// <summary>T-วุฒิการศึกษา11.</summary>
    public static IReadOnlyList<string> EducationLevels { get; } = ["ป.3", "ป.4", "ป.6", "ม.3", "ม.ศ.5", "ม.6", "ปวช.", "ปวส.", "ปริญญาตรี"];

    /// <summary>T-ตำแหน่ง — other values may be typed (e.g. พนักงานขับรถ).</summary>
    public static IReadOnlyList<string> Positions { get; } = ["พนักงาน", "รองหัวหน้า", "หัวหน้า"];
}

/// <summary>
/// One SCR-02/03 form to save. <see cref="Id"/> null = new employee; <see cref="RowVersion"/> = as loaded.
/// Code and name are required (a blank row is never saved, instead of BR-051's delete on close).
/// </summary>
public sealed record EmployeeInput
{
    public const string DefaultNationality = "ไทย";

    public int? Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string? EnglishTitle { get; init; }
    public string? EnglishName { get; init; }
    public string? Sex { get; init; }
    public DateOnly? BirthDate { get; init; }
    public string? NationalId { get; init; }
    public string? SocialSecurityNo { get; init; }
    public string? IdCardPlace { get; init; }
    public DateOnly? IdCardIssued { get; init; }
    public string? RegisteredAddress { get; init; }
    public string? CurrentAddress { get; init; }
    public string? Telephone { get; init; }
    public string? Nationality { get; init; } = DefaultNationality;
    public DateOnly? StartDate { get; init; }
    public string? ListNo { get; init; }
    public string? Education { get; init; }
    public int? DepartmentId { get; init; }
    public string? Position { get; init; }
    public string? Duty { get; init; }
    public int? EmployeeType { get; init; }
    public double Salary { get; init; }
    public double DailyWage { get; init; }
    public int? ShiftId { get; init; }
    public bool HotAllowance { get; init; }
    public string? BankAccountTtb { get; init; }
    public string? BankAccountKbank { get; init; }
    public string? Hospital { get; init; }
    public DateOnly? ResignDate { get; init; }
    public string? Memo { get; init; }
    public byte[]? RowVersion { get; init; }
}

/// <summary>An employee as screens and other modules see it, with the org names and the probation dates (BR-053).</summary>
public sealed record EmployeeDto
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? EnglishTitle { get; init; }
    public string? EnglishName { get; init; }
    public string? Sex { get; init; }
    public DateOnly? BirthDate { get; init; }
    public string? NationalId { get; init; }
    public string? SocialSecurityNo { get; init; }
    public string? IdCardPlace { get; init; }
    public DateOnly? IdCardIssued { get; init; }
    public string? RegisteredAddress { get; init; }
    public string? CurrentAddress { get; init; }
    public string? Telephone { get; init; }
    public required string Nationality { get; init; }
    public DateOnly? StartDate { get; init; }
    public string? ListNo { get; init; }
    public string? Education { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    /// <summary>ฝ่าย of the department (BR-052: chosen with the department).</summary>
    public string? SectionName { get; init; }
    public string? Position { get; init; }
    public string? Duty { get; init; }
    public int? EmployeeType { get; init; }
    public double Salary { get; init; }
    public double DailyWage { get; init; }
    public int? ShiftId { get; init; }
    public string? ShiftCode { get; init; }
    public bool HotAllowance { get; init; }
    public string? BankAccountTtb { get; init; }
    public string? BankAccountKbank { get; init; }
    public string? Hospital { get; init; }
    public DateOnly? ResignDate { get; init; }
    public string? Memo { get; init; }
    public required byte[] RowVersion { get; init; }

    public bool IsWorking => ResignDate is null;
    /// <summary>ยังไม่ผ่านทดลองงาน (RPT-20, BR-053).</summary>
    public bool OnProbation { get; init; }
    /// <summary>วันครบ 90 วัน = START + 89 (แบบฟอร์มรับพนักงาน, BR-053).</summary>
    public DateOnly? NinetyDayDate => StartDate is { } s ? Probation.NinetyDayDate(s) : null;
    /// <summary>วันครบทดลองงาน = START + 118 (สัญญาจ้าง, BR-053).</summary>
    public DateOnly? ProbationEndDate => StartDate is { } s ? Probation.EndDate(s) : null;

    /// <summary>The form to edit this employee.</summary>
    public EmployeeInput ToInput() => new()
    {
        Id = Id, Code = Code, Name = Name, EnglishTitle = EnglishTitle, EnglishName = EnglishName, Sex = Sex,
        BirthDate = BirthDate, NationalId = NationalId, SocialSecurityNo = SocialSecurityNo, IdCardPlace = IdCardPlace,
        IdCardIssued = IdCardIssued, RegisteredAddress = RegisteredAddress, CurrentAddress = CurrentAddress,
        Telephone = Telephone, Nationality = Nationality, StartDate = StartDate, ListNo = ListNo, Education = Education,
        DepartmentId = DepartmentId, Position = Position, Duty = Duty, EmployeeType = EmployeeType, Salary = Salary,
        DailyWage = DailyWage, ShiftId = ShiftId, HotAllowance = HotAllowance, BankAccountTtb = BankAccountTtb,
        BankAccountKbank = BankAccountKbank, Hospital = Hospital, ResignDate = ResignDate, Memo = Memo, RowVersion = RowVersion,
    };
}

/// <summary>A line of the SCR-03 employee search (ID, ชื่อ).</summary>
public sealed record EmployeeListItem(int Id, string Code, string Name, string? DepartmentName, bool IsWorking);

/// <summary>Values already entered, offered by the หน้าที่ and โรงพยาบาล dropdowns (SCR-02).</summary>
public sealed record EmployeeChoices(IReadOnlyList<string> Duties, IReadOnlyList<string> Hospitals);

/// <summary>
/// สถิติประกอบหน้าแก้ไขข้อมูลพนักงาน (BR-054), of employees still working, computed each time SCR-03 opens.
/// </summary>
public sealed record EmployeeStatistics(
    IReadOnlyList<DepartmentHeadcount> ByDepartment,
    IReadOnlyList<DepartmentEducation> ByEducation,
    IReadOnlyList<HospitalHeadcount> ByHospital)
{
    /// <summary>Columns of the legacy crosstab <c>วุฒิการศึกษา22</c>, in its order.</summary>
    public static IReadOnlyList<string> EducationLevels { get; } =
        ["ป.3", "ป.4", "ป.6", "ป.7", "ม.3", "ม.6", "ม.ศ.5", "ปวช.", "ปวส.", "ปริญญาตรี"];
}

/// <summary>ชาย / หญิง / รวม of a department, against its อัตรากำลัง (รวม − อัตรา).</summary>
public sealed record DepartmentHeadcount(int DepartmentId, string DepartmentName, int Male, int Female, int Total, int? StaffingTarget)
{
    public int? Difference => StaffingTarget is { } target ? Total - target : null;
}

/// <summary>A department's row of the education crosstab: count per <see cref="EmployeeStatistics.EducationLevels"/>.</summary>
public sealed record DepartmentEducation(int DepartmentId, string DepartmentName, IReadOnlyDictionary<string, int> Counts);

public sealed record HospitalHeadcount(string Hospital, int Count);

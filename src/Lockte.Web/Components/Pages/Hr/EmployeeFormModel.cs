using Lockte.Hr;

namespace Lockte.Web.Components.Pages.Hr;

/// <summary>
/// The editable fields of SCR-02/03 (MudBlazor binds to settable properties and DateTime dates). The evaluation
/// fields have no field on these screens (#67) but travel with the form, so a save keeps what it loaded.
/// </summary>
public sealed class EmployeeFormModel
{
    public int? Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? EnglishTitle { get; set; }
    public string? EnglishName { get; set; }
    public string? Sex { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? NationalId { get; set; }
    public string? SocialSecurityNo { get; set; }
    public string? IdCardPlace { get; set; }
    public DateTime? IdCardIssued { get; set; }
    public string? RegisteredAddress { get; set; }
    public string? CurrentAddress { get; set; }
    public string? Telephone { get; set; }
    public string? Nationality { get; set; } = EmployeeInput.DefaultNationality;
    public DateTime? StartDate { get; set; }
    public string? ListNo { get; set; }
    public string? Education { get; set; }
    public int? DepartmentId { get; set; }
    public int? SectionId { get; set; }
    public string? Position { get; set; }
    public string? Duty { get; set; }
    public int? EmployeeType { get; set; }
    public double Salary { get; set; }
    public double DailyWage { get; set; }
    public int? ShiftId { get; set; }
    public bool HotAllowance { get; set; }
    public string? BankAccountTtb { get; set; }
    public string? BankAccountKbank { get; set; }
    public string? Hospital { get; set; }
    public DateTime? ResignDate { get; set; }
    public string? Memo { get; set; }
    public byte[]? RowVersion { get; set; }
    /// <summary>As loaded: carries the fields these screens do not show.</summary>
    private EmployeeInput loaded = new();

    public static EmployeeFormModel From(EmployeeDto e) => new()
    {
        Id = e.Id, Code = e.Code, Name = e.Name, EnglishTitle = e.EnglishTitle, EnglishName = e.EnglishName, Sex = e.Sex,
        BirthDate = ToDateTime(e.BirthDate), NationalId = e.NationalId, SocialSecurityNo = e.SocialSecurityNo,
        IdCardPlace = e.IdCardPlace, IdCardIssued = ToDateTime(e.IdCardIssued), RegisteredAddress = e.RegisteredAddress,
        CurrentAddress = e.CurrentAddress, Telephone = e.Telephone, Nationality = e.Nationality,
        StartDate = ToDateTime(e.StartDate), ListNo = e.ListNo, Education = e.Education, DepartmentId = e.DepartmentId,
        SectionId = e.SectionId,
        Position = e.Position, Duty = e.Duty, EmployeeType = e.EmployeeType, Salary = e.Salary, DailyWage = e.DailyWage,
        ShiftId = e.ShiftId, HotAllowance = e.HotAllowance, BankAccountTtb = e.BankAccountTtb,
        BankAccountKbank = e.BankAccountKbank, Hospital = e.Hospital, ResignDate = ToDateTime(e.ResignDate), Memo = e.Memo,
        RowVersion = e.RowVersion, loaded = e.ToInput(),
    };

    public EmployeeInput ToInput() => loaded with
    {
        Id = Id, Code = Code, Name = Name, EnglishTitle = EnglishTitle, EnglishName = EnglishName, Sex = Sex,
        BirthDate = ToDateOnly(BirthDate), NationalId = NationalId, SocialSecurityNo = SocialSecurityNo, IdCardPlace = IdCardPlace,
        IdCardIssued = ToDateOnly(IdCardIssued), RegisteredAddress = RegisteredAddress, CurrentAddress = CurrentAddress,
        Telephone = Telephone, Nationality = Nationality, StartDate = ToDateOnly(StartDate), ListNo = ListNo,
        Education = Education, DepartmentId = DepartmentId, SectionId = SectionId, Position = Position, Duty = Duty, EmployeeType = EmployeeType,
        Salary = Salary, DailyWage = DailyWage, ShiftId = ShiftId, HotAllowance = HotAllowance, BankAccountTtb = BankAccountTtb,
        BankAccountKbank = BankAccountKbank, Hospital = Hospital, ResignDate = ToDateOnly(ResignDate), Memo = Memo,
        RowVersion = RowVersion,
    };

    private static DateTime? ToDateTime(DateOnly? date) => date?.ToDateTime(TimeOnly.MinValue);
    private static DateOnly? ToDateOnly(DateTime? date) => date is { } d ? DateOnly.FromDateTime(d) : null;
}

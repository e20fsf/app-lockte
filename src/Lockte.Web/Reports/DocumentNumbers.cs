using Lockte.SharedKernel.Persistence;

namespace Lockte.Web.Reports;

/// <summary>
/// เลขเอกสารควบคุม (ISO) of one printed form (#40): kept as data so ผู้ควบคุมเอกสาร issues a new edition without a
/// program change. The printed text is <see cref="Code"/> + <see cref="Edition"/> as stored, so the legacy spacing is
/// kept character for character (e.g. <c>PC-PE001-8/2547ฉบับที่3</c> beside <c>WI-PE006-1/2547 ฉบับที่ 1</c>).
/// </summary>
internal sealed class DocumentNumber : IAudited
{
    public int Id { get; set; }
    /// <summary>Which document: a <see cref="DocumentKeys"/> value, fixed by the program.</summary>
    public string Key { get; set; } = "";
    /// <summary>The document's name, for the edit screen.</summary>
    public string Name { get; set; } = "";
    /// <summary>รหัสเอกสาร, e.g. WI-PE-004-1/2547.</summary>
    public string Code { get; set; } = "";
    /// <summary>ฉบับที่, with the spaces in front of it as printed (blank for สปส.6-07 / แบบ คร.2).</summary>
    public string Edition { get; set; } = "";

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Key;
}

/// <summary>
/// The documents with a number printed top right in the legacy samples (specs/employee/print-samples.md, checked against
/// the PDFs): their keys are the RPT codes, with a suffix where one RPT prints several forms.
/// </summary>
public static class DocumentKeys
{
    public const string ProbationRequest = "RPT-22-1";
    public const string DocumentTransmittal = "RPT-22-2";
    public const string ProbationEvaluation = "RPT-22-3";
    public const string YearlyEvaluationDaily = "RPT-43-D";
    public const string YearlyEvaluationMonthly = "RPT-43-M";
    public const string LeaveForm = "RPT-50";
    public const string InsuredRegister = "RPT-51";
    public const string WorkCertificate = "RPT-53-W";
    public const string SalaryCertificate = "RPT-53-S";
    public const string ResignationLetter = "RPT-55";
    public const string ProbationForm = "RPT-56-1";
    public const string DailyContract = "RPT-56-2";
    public const string MonthlyContract = "RPT-56-3";
    public const string Kr2 = "RPT-57";
    /// <summary>RPT-21 ทะเบียนแม่พิมพ์ of ผลผลิตและORDER (#137): the only list of that program with a number.</summary>
    public const string MoldRegister = "RPT-21-M";
    /// <summary>RPT-01 ใบ SALE ORDER (#110).</summary>
    public const string SaleOrder = "RPT-01-O";

    public static IReadOnlyList<string> All { get; } =
    [
        ProbationRequest, DocumentTransmittal, ProbationEvaluation, YearlyEvaluationDaily, YearlyEvaluationMonthly, LeaveForm,
        InsuredRegister, WorkCertificate, SalaryCertificate, ResignationLetter, ProbationForm, DailyContract, MonthlyContract, Kr2, MoldRegister, SaleOrder,
    ];
}

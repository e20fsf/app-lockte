using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

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

    public static IReadOnlyList<string> All { get; } =
    [
        ProbationRequest, DocumentTransmittal, ProbationEvaluation, YearlyEvaluationDaily, YearlyEvaluationMonthly, LeaveForm,
        InsuredRegister, WorkCertificate, SalaryCertificate, ResignationLetter, ProbationForm, DailyContract, MonthlyContract, Kr2,
    ];
}

public sealed record DocumentNumberDto(int Id, string Key, string Name, string Code, string Edition, byte[] RowVersion)
{
    public string PrintedText => Code + Edition;
}

/// <summary>An edit of SCR "เลขเอกสารควบคุม": the row and its rowversion as loaded.</summary>
public sealed record DocumentNumberInput(int Id, string? Code, string? Edition, byte[] RowVersion);

/// <summary>Report infrastructure of the host (ADR 0002 table: schema <c>report</c>), like <c>auth</c> and <c>audit</c>.</summary>
public sealed class ReportsDbContext(DbContextOptions<ReportsDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "report";

    internal DbSet<DocumentNumber> DocumentNumbers => Set<DocumentNumber>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
        => modelBuilder.Entity<DocumentNumber>(d =>
        {
            d.HasAuditName(DocumentNumberService.Table);
            d.Property(x => x.Key).HasMaxLength(20).HasAuditName("เอกสาร");
            d.Property(x => x.Name).HasMaxLength(200).HasAuditName("ชื่อเอกสาร");
            d.Property(x => x.Code).HasMaxLength(DocumentNumberService.TextLength).HasAuditName("รหัสเอกสาร");
            d.Property(x => x.Edition).HasMaxLength(DocumentNumberService.TextLength).HasAuditName("ฉบับที่");
            d.HasIndex(x => x.Key).IsUnique();
        });
}

/// <summary>Reads and edits เลขเอกสารควบคุม; the rows themselves come with the migration (the program knows its documents).</summary>
public sealed class DocumentNumberService(ReportsDbContext db)
{
    public const string Table = "เลขเอกสารควบคุม";
    public const int TextLength = 50;

    public async Task<IReadOnlyList<DocumentNumberDto>> ListAsync()
        => await db.DocumentNumbers.AsNoTracking().OrderBy(d => d.Key)
            .Select(d => new DocumentNumberDto(d.Id, d.Key, d.Name, d.Code, d.Edition, d.RowVersion)).ToListAsync();

    /// <summary>What prints top right, or null for a document without a number.</summary>
    public async Task<string?> PrintedTextAsync(string key)
        => await db.DocumentNumbers.AsNoTracking().Where(d => d.Key == key).Select(d => d.Code + d.Edition).FirstOrDefaultAsync();

    /// <summary>
    /// The code is trimmed (blank is refused); the edition is kept as typed, its leading spaces being part of the print.
    /// A newer save by someone else throws <see cref="ConcurrencyConflictException"/>.
    /// </summary>
    public async Task<ServiceResult> SaveAsync(DocumentNumberInput input)
    {
        var code = input.Code?.Trim();
        var edition = (input.Edition ?? "").TrimEnd();
        if (string.IsNullOrEmpty(code)) return ServiceResult.Fail("กรุณากรอกรหัสเอกสาร");
        if (code.Length > TextLength || edition.Length > TextLength) return ServiceResult.Fail($"ยาวได้ไม่เกิน {TextLength} ตัวอักษร");
        var row = await db.DocumentNumbers.FirstOrDefaultAsync(d => d.Id == input.Id);
        if (row is null) return ServiceResult.Fail("ไม่พบเอกสารนี้");
        row.ExpectRowVersion(input.RowVersion);
        row.Code = code;
        row.Edition = edition;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(row.Id);
    }
}

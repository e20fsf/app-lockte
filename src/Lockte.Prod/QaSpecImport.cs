using Lockte.Prod.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

public sealed record QaSpecImportRow(string Name, string? Models, int? DocumentSequence, IReadOnlyList<QaMeasurePointInput> Points, IReadOnlyList<string> Defects);

/// <summary>A document type as the migration tool brings it (#129) with the specs found for it.</summary>
public sealed record QaDocumentTypeImport(
    string Code, string? Revision, string Name, string? DepartmentStep, string? SampleUnit, QaResultColumns ResultColumns, string? AttributeRanges, QaPageLayout PageLayout, string? Paper,
    IReadOnlyList<QaSpecImportRow> Specs);

/// <summary>What is in the QA tables, counted back.</summary>
public sealed record QaSpecCounts(int Types, int Specs, int Points, int Defects);

/// <summary>
/// ย้ายข้อมูลสเปคตรวจ QA (#129) into the tables of #128. For the data-migration tool only, never registered in the web app: the specs are taken as the tool extracted them (the checks are the tool's),
/// with no row-by-row history (#39). Deleting loads again only the document types and specs — the table of sample sizes is reference data and is left alone.
/// </summary>
public sealed class QaSpecImport(ProdDbContext db, AuditScope audit)
{
    public async Task DeleteAllAsync()
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.QaMeasurePoints.ExecuteDeleteAsync();
        await db.QaDefects.ExecuteDeleteAsync();
        await db.QaModelSpecs.ExecuteDeleteAsync();
        await db.QaDocumentTypes.ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }

    public async Task AddAsync(IEnumerable<QaDocumentTypeImport> types)
    {
        using (audit.SuppressRowChanges())
            foreach (var batch in types.Chunk(20))
            {
                foreach (var t in batch)
                {
                    var type = new QaDocumentType
                    {
                        Code = t.Code, Revision = t.Revision, Name = t.Name, DepartmentStep = t.DepartmentStep, SampleUnit = t.SampleUnit, ResultColumns = t.ResultColumns,
                        AttributeRanges = t.AttributeRanges, PageLayout = t.PageLayout, Paper = t.Paper,
                    };
                    db.QaDocumentTypes.Add(type);
                    foreach (var s in t.Specs)
                        db.QaModelSpecs.Add(new QaModelSpec
                        {
                            DocumentType = type, Name = s.Name, Models = s.Models, DocumentSequence = s.DocumentSequence,
                            Points = s.Points.Select((p, i) => new QaMeasurePoint { Sequence = i + 1, PointNo = p.PointNo, Description = p.Description, Standard = p.Standard }).ToList(),
                            Defects = s.Defects.Select((d, i) => new QaDefect { Sequence = i + 1, Name = d }).ToList(),
                        });
                }
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
    }

    public async Task<QaSpecCounts> GetCountsAsync()
        => new(await db.QaDocumentTypes.CountAsync(), await db.QaModelSpecs.CountAsync(), await db.QaMeasurePoints.CountAsync(), await db.QaDefects.CountAsync());

    /// <summary>The names of the specs in the database — to count back per group.</summary>
    public async Task<IReadOnlyList<string>> GetSpecNamesAsync() => await db.QaModelSpecs.AsNoTracking().Select(s => s.Name).ToListAsync();
}

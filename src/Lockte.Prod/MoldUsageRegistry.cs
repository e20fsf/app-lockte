using Lockte.Prod.Data;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Prod;

/// <summary>
/// What a cut did. <see cref="Error"/> is a Thai message when it could not be made (no previous cut to start from), <see cref="Busy"/> when another
/// person is cutting; <see cref="LinesWithoutPieces"/> are production lines of a counted mold whose pieces per shot is blank or 0 — not counted (BR-008).
/// </summary>
public sealed record MoldCutResult(bool Succeeded, string? Error, bool Busy, int Molds, int UsageRows, int LinesWithoutPieces)
{
    public static MoldCutResult Failed(string error) => new(false, error, false, 0, 0, 0);
}

/// <summary>การใช้งานของแม่พิมพ์ถึงวันตัด (RPT-15) with the maintenance criterion of the mold and its age (Q-34: PD_QTY ÷ 2 as the legacy report showed).</summary>
public sealed record MoldCumulativeLine(string MoldCode, string MoldName, int Cumulative, string? Criterion, decimal Age);

public sealed record MoldUsageLine(DateOnly Day, int Shots, bool IsOpening);

/// <summary>
/// ตัดยอดแม่พิมพ์ (SCR-18, BR-008): counts how many times each mold was used from the production lines, carries the totals over as the opening of the
/// next period, and keeps the total of every cut date as a history (instead of the legacy table that was rewritten). The mold file is not moved (ข้อตัดสิน #27).
/// </summary>
public sealed class MoldUsageRegistry(ProdDbContext db, AuditScope audit)
{
    private const string CutLock = "lockte:prod:mold-cut";

    /// <summary>Counted by BR-008: die casting (<c>BF</c>, <c>D0</c>, <c>CF</c>) and brake-pad pressing (<c>BL</c>, <c>DB</c>). The clutch molds (<c>CA CB CC CP</c>) are not counted (Q-34).</summary>
    public static bool IsCounted(string moldCode)
        => new[] { "BF", "D0", "CF", "BL", "DB" }.Any(prefix => moldCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>จำนวนครั้งที่ใช้ of a line = <c>Int(จำนวนดี ÷ ชิ้นต่อช็อต)</c>, rounded down; the good quantity only — a shot that gave rejects is not counted (BR-008, AC-12). Null when the pieces per shot is blank or 0.</summary>
    public static int? CountShots(decimal goodQuantity, int? piecesPerShot)
        => piecesPerShot is > 0 ? (int)Math.Floor(goodQuantity / piecesPerShot.Value) : null;

    /// <summary>ตัดยอดครั้งแรก (ขั้น 1–4): usage of <paramref name="from"/>–<paramref name="cut"/> made again, the total at <paramref name="cut"/> kept and carried over.</summary>
    public Task<MoldCutResult> CutAsync(DateOnly from, DateOnly cut) => RunAsync(from, cut, previousCut: null);

    /// <summary>ตัดซ้ำ: clears the usage, drops the totals from <paramref name="cut"/> on, takes the total of <paramref name="previousCut"/> (TO_DATE) back as the start and cuts again.</summary>
    public Task<MoldCutResult> RecutAsync(DateOnly from, DateOnly cut, DateOnly previousCut) => RunAsync(from, cut, previousCut);

    private async Task<MoldCutResult> RunAsync(DateOnly from, DateOnly cut, DateOnly? previousCut)
    {
        if (from > cut) return MoldCutResult.Failed("ต้นงวดต้องไม่หลังวันตัด");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var granted = (await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {CutLock}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0; SELECT @r AS Value")
            .ToListAsync()).Single();
        if (granted < 0) return new MoldCutResult(false, null, true, 0, 0, 0);

        using var _ = audit.SuppressRowChanges(); // derived data, made again at will: no row-by-row history

        if (previousCut is { } previous)
        {
            var starting = await db.MoldCutoffs.Where(c => c.CutDate == previous).Select(c => new { c.MoldId, c.Cumulative }).ToListAsync();
            if (starting.Count == 0) return MoldCutResult.Failed("ไม่พบยอดสะสมของวันตัดงวดก่อน");
            await db.MoldUsages.ExecuteDeleteAsync();
            await db.MoldCutoffs.Where(c => c.CutDate >= cut).ExecuteDeleteAsync();
            db.MoldUsages.AddRange(starting.Select(s => new MoldUsage { MoldId = s.MoldId, Day = previous, Shots = s.Cumulative, IsOpening = true }));
            await db.SaveChangesAsync();
        }

        // 1. the usage of the period, made again from the production lines
        await db.MoldUsages.Where(u => u.Day >= from).ExecuteDeleteAsync();
        var lines = await db.ProductionReportLines.AsNoTracking()
            .Where(l => l.MoldId != null && l.ProductionReport.ReportDate >= from && l.ProductionReport.ReportDate <= cut)
            .Select(l => new { Day = l.ProductionReport.ReportDate!.Value, MoldId = l.MoldId!.Value, l.Mold!.Code, l.Mold.PiecesPerShot, l.GoodQuantity })
            .ToListAsync();
        var withoutPieces = 0;
        var usage = new Dictionary<(DateOnly Day, int MoldId), int>();
        foreach (var line in lines.Where(l => IsCounted(l.Code)))
        {
            if (CountShots(line.GoodQuantity, line.PiecesPerShot) is not { } shots) { withoutPieces++; continue; }
            usage[(line.Day, line.MoldId)] = usage.GetValueOrDefault((line.Day, line.MoldId)) + shots;
        }
        db.MoldUsages.AddRange(usage.Select(u => new MoldUsage { MoldId = u.Key.MoldId, Day = u.Key.Day, Shots = u.Value }));
        await db.SaveChangesAsync();

        // 2. and 3. the total of the cut date: the molds still in use, Σ of every row up to it (the opening included)
        await db.MoldCutoffs.Where(c => c.CutDate == cut).ExecuteDeleteAsync();
        var totals = await db.MoldUsages.AsNoTracking().Where(u => u.Day <= cut && u.Mold.EndDate == null)
            .GroupBy(u => u.MoldId).Select(g => new { MoldId = g.Key, Total = g.Sum(u => u.Shots) }).ToListAsync();
        db.MoldCutoffs.AddRange(totals.Select(t => new MoldCutoff { MoldId = t.MoldId, CutDate = cut, Cumulative = t.Total }));

        // 4. what is up to the cut date is replaced by the total, as the opening of the next period
        await db.MoldUsages.Where(u => u.Day <= cut).ExecuteDeleteAsync();
        db.MoldUsages.AddRange(totals.Select(t => new MoldUsage { MoldId = t.MoldId, Day = cut, Shots = t.Total, IsOpening = true }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new MoldCutResult(true, null, false, totals.Count, usage.Count, withoutPieces);
    }
}

/// <summary>Reads of the mold usage for RPT-15 and the cut screen.</summary>
public sealed class MoldUsageDirectory(ProdDbContext db)
{
    /// <summary>The dates a cut was made, latest first.</summary>
    public async Task<IReadOnlyList<DateOnly>> GetCutDatesAsync()
        => await db.MoldCutoffs.AsNoTracking().Select(c => c.CutDate).Distinct().OrderByDescending(d => d).ToListAsync();

    /// <summary>การใช้งานของพิมพ์ถึงวันที่ …: the total of every mold at the cut date, by mold code.</summary>
    public async Task<IReadOnlyList<MoldCumulativeLine>> GetCumulativeAsync(DateOnly cutDate)
        => (await db.MoldCutoffs.AsNoTracking().Where(c => c.CutDate == cutDate)
                .Select(c => new { c.Mold.Code, c.Mold.Name, c.Cumulative, c.Mold.MaintenanceShots, c.Mold.MaintenancePieces, c.Mold.MaintenanceYears })
                .ToListAsync())
            .OrderBy(c => c.Code, StringComparer.Ordinal)
            .Select(c => new MoldCumulativeLine(c.Code, c.Name, c.Cumulative, Criterion(c.MaintenanceShots, c.MaintenancePieces, c.MaintenanceYears), c.Cumulative / 2m))
            .ToList();

    /// <summary>The usage rows of one mold in the range, by day (the opening row first on its day) — STOCKพิมพ์.</summary>
    public async Task<IReadOnlyList<MoldUsageLine>> GetUsageAsync(int moldId, DateOnly from, DateOnly to)
        => (await db.MoldUsages.AsNoTracking().Where(u => u.MoldId == moldId && u.Day >= from && u.Day <= to).OrderBy(u => u.Day).ThenByDescending(u => u.IsOpening)
            .Select(u => new MoldUsageLine(u.Day, u.Shots, u.IsOpening)).ToListAsync());

    /// <summary>The criterion as the report prints it: "50,000 SHOT", "100,000 ชิ้น" or "1 ปี" — whichever of the three the mold has.</summary>
    public static string? Criterion(int? shots, int? pieces, int? years)
        => shots is > 0 ? $"{shots:#,##0} SHOT" : pieces is > 0 ? $"{pieces:#,##0} ชิ้น" : years is > 0 ? $"{years} ปี" : null;
}

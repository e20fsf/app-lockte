using Lockte.Purch;
using Lockte.SharedKernel;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Purch;

/// <summary>RPT-32 ทะเบียนผู้ขาย (Approved Supplier List): the suppliers used in production, by รหัส.</summary>
public sealed record ApprovedSupplierReport(IReadOnlyList<SupplierDto> Rows);

/// <summary>RPT-33 หนังสือแสดงความมุ่งมั่นของผู้ส่งมอบหรือผู้รับเหมาช่วง: one letter for the supplier chosen, dated the day of printing.</summary>
public sealed record SupplierCommitmentLetter(string SupplierName, DateOnly Date)
{
    public string LongDate => Date.ToString("d MMMM yyyy", ThaiDate.Culture);
}

/// <summary>ทะเบียนผู้ขายที่อนุมัติ (RPT-32; #264) and หนังสือแสดงความมุ่งมั่น (RPT-33), ระดับ B. A supplier that is not used in production is left out of RPT-32.</summary>
public static class SupplierDocumentReports
{
    public const string ApprovedTitle = "ทะเบียนผู้ขาย (Approved Supplier List)";
    public const string CommitmentTitle = "หนังสือแสดงความมุ่งมั่นของผู้ส่งมอบหรือผู้รับเหมาช่วง";

    public static ApprovedSupplierReport Approved(IReadOnlyList<SupplierDto> all)
        => new(all.Where(s => s.UsedInProduction).OrderBy(s => s.Code, StringComparer.Ordinal).ToList());

    public static RouteGroupBuilder MapSupplierDocumentReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/purch/approved-suppliers", async (SupplierDirectory suppliers, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var report = Approved(await suppliers.GetAllAsync(usedInProductionOnly: true));
            var pdf = await printer.PrintAsync<ApprovedSupplierView>(ApprovedTitle, Paper.A4Landscape, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User), DocumentKeys.ApprovedSupplierList,
                cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "ทะเบียนผู้ขาย");
        });

        reports.MapGet("/purch/commitment", async (int? supplier, SupplierDirectory suppliers, TimeProvider clock, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (supplier is not { } id || (await suppliers.GetAllAsync()).FirstOrDefault(s => s.Id == id) is not { } found) return Results.BadRequest("กรุณาเลือกผู้ขาย");
            var letter = new SupplierCommitmentLetter(found.Name, DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
            var pdf = await printer.PrintAsync<SupplierCommitmentView>(CommitmentTitle, Paper.A4Portrait, new() { ["Report"] = letter }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "หนังสือมุ่งมั่น " + found.Code);
        });
        return reports;
    }
}

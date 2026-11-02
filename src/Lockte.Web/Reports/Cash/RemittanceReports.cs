using Lockte.Cash;
using Lockte.SharedKernel;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Cash;

/// <summary>One row of RPT-07 / RPT-08: a receipt number and its total (RND-A).</summary>
public sealed record RemittanceRow(string No, decimal Total);

/// <summary>RPT-07: the receipts of one closing, with the range asked for.</summary>
public sealed record RemittanceSummaryReport(string FromNo, string ToNo, DateOnly PrintedOn, IReadOnlyList<RemittanceRow> Rows)
{
    public decimal Total => Rows.Sum(r => r.Total);
}

/// <summary>RPT-08: the receipts waiting to be remitted, those with a total above 0.</summary>
public sealed record UnremittedReport(IReadOnlyList<RemittanceRow> Rows)
{
    public decimal Total => Rows.Sum(r => r.Total);
}

/// <summary>
/// ใบสรุปยอดเงินขายของเก่าส่งสำนักงาน (RPT-07, A4 ตั้ง) and ขายของเก่าและอื่นๆรอนำส่ง (RPT-08, Letter ตั้ง; #240), ระดับ B (ตามแบบกลาง #40): ลำดับ, เลขที่, จำนวนเงินต่อใบ (RND-A), รวมทั้งหมด.
/// RPT-07 prints the range that was closed — the legacy left "ตั้งแต่เลขที่ … ถึงเลขที่" blank every time (BR-016 ⚠) — and the date of printing in full at the top right; RPT-08 lists only
/// the numbers whose total is above 0, by number.
/// </summary>
public static class RemittanceReports
{
    public const string SummaryTitle = "ใบสรุปยอดเงินขายของเก่าส่งสำนักงาน";
    public const string UnremittedTitle = "ขายของเก่าและอื่นๆรอนำส่ง";

    public static RemittanceSummaryReport Summary(ScrapRemittanceDto remittance, DateOnly printedOn)
        => new(remittance.FromNo, remittance.ToNo, printedOn, remittance.Receipts.Select(r => new RemittanceRow(r.No, r.Total)).ToList());

    public static UnremittedReport Unremitted(IReadOnlyList<ScrapReceiptDto> waiting)
        => new(waiting.Where(r => r.Total > 0).Select(r => new RemittanceRow(r.No, r.Total)).ToList());

    public static RouteGroupBuilder MapCashRemittanceReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/cash/remittance", async (int id, ScrapRemittanceDirectory remittances, TimeProvider clock, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (await remittances.GetAsync(id) is not { } remittance) return Results.NotFound("ไม่พบการนำส่ง");
            var report = Summary(remittance, DateOnly.FromDateTime(clock.GetLocalNow().DateTime));
            var pdf = await printer.PrintAsync<RemittanceSummaryView>(SummaryTitle, Paper.A4Portrait, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, $"ใบสรุปยอดส่งสำนักงาน {remittance.FromNo}-{remittance.ToNo}");
        });

        reports.MapGet("/cash/unremitted", async (ScrapRemittanceDirectory remittances, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var report = Unremitted(await remittances.UnremittedAsync());
            var pdf = await printer.PrintAsync<UnremittedView>(UnremittedTitle, Paper.LetterPortrait, new() { ["Report"] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, UnremittedTitle);
        });
        return reports;
    }
}

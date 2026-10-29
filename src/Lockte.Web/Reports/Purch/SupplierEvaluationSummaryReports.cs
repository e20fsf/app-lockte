using Lockte.Purch;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Purch;

/// <summary>A row of RPT-09: a supplier that is used in production, and its evaluation of the quarter when it has one ("ไม่มีการสั่งซื้อ" when it has not).</summary>
public sealed record EvaluationSummaryRow(SupplierDto Supplier, SupplierEvaluationDto? Evaluation)
{
    /// <summary>จำนวนสั่งซื้อทั้งหมด = มีใบรับรอง + ไม่มีใบรับรอง (blank when 0).</summary>
    public int? OrderCount => Evaluation is { Counts: var c } && c.WithCertificate + c.WithoutCertificate is > 0 and var n ? n : null;
}

public sealed record EvaluationSummary(DateOnly From, DateOnly To, int Quarter, IReadOnlyList<EvaluationSummaryRow> Rows);

/// <summary>
/// RPT-08 แบบประเมินผู้ขายประจำปี (BR-051): a blank form per supplier used in production that had receipts in the period — only the supplier's details are filled in.
/// RPT-09 สรุปผลการประเมินติดตามผู้ขายประจำไตรมาส (BR-052): every supplier used in production, with the evaluation kept for the quarter and year chosen (not for a period that has to
/// match to the day, as the legacy did, AC-44), grouped by certificate and ordered by ไม่มีใบรับรอง. ระดับ B.
/// </summary>
public static class SupplierEvaluationSummaryReports
{
    public const string AnnualTitle = "แบบประเมินผู้ขายประจำปี";
    public const string SummaryTitle = "สรุปผลการประเมินติดตามผู้ขายประจำไตรมาส";

    /// <summary>The rows of the summary of a quarter of a year: every production supplier; certificate holders first, then by ไม่มีใบรับรอง (EXPR4), then by code.</summary>
    public static async Task<EvaluationSummary> BuildSummaryAsync(SupplierDirectory suppliers, SupplierEvaluationDirectory evaluations, int year, int quarter)
    {
        var from = new DateOnly(year, (quarter - 1) * 3 + 1, 1);
        var to = from.AddMonths(3).AddDays(-1);
        var kept = (await evaluations.ListAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), quarter)).ToDictionary(e => e.SupplierId, e => e);
        var rows = (await suppliers.GetAllAsync(usedInProductionOnly: true))
            .Select(s => new EvaluationSummaryRow(s, kept.GetValueOrDefault(s.Id)))
            .OrderByDescending(r => r.Supplier.Certificate).ThenBy(r => r.Evaluation?.Counts.WithoutCertificate ?? 0).ThenBy(r => r.Supplier.Code).ToList();
        return new EvaluationSummary(from, to, quarter, rows);
    }

    public static RouteGroupBuilder MapSupplierEvaluationSummaryReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/purch/evaluation-annual", async (DateOnly? from, DateOnly? to, SupplierDirectory suppliers, TimeProvider clock, HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (from is null || to is null) return Results.BadRequest("กรุณากรอกช่วงวันที่");
            if (from > to) return Results.BadRequest("ตั้งแต่วันที่ต้องไม่หลังถึงวันที่");
            var list = await suppliers.GetProductionSuppliersWithReceiptsAsync(from.Value, to.Value);
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var pdf = await printer.PrintAsync<SupplierAnnualEvaluationView>(AnnualTitle, Paper.A4Portrait, new() { ["Suppliers"] = list, ["Year"] = from.Value.Year, ["Date"] = today },
                AccountClaims.DisplayNameOf(http.User), DocumentKeys.SupplierAnnualEvaluation, cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "แบบประเมินผู้ขายประจำปี " + from.Value.Year);
        });

        reports.MapGet("/purch/evaluation-summary", async (int year, int quarter, SupplierDirectory suppliers, SupplierEvaluationDirectory evaluations, HttpContext http, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (quarter is < 1 or > 4 || year < 1900) return Results.BadRequest("กรุณาเลือกปีและไตรมาส");
            var summary = await BuildSummaryAsync(suppliers, evaluations, year, quarter);
            var pdf = await printer.PrintAsync<SupplierEvaluationSummaryView>(SummaryTitle, Paper.A4Landscape, new() { [nameof(SupplierEvaluationSummaryView.Report)] = summary },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, $"สรุปประเมินผู้ขาย ไตรมาส {quarter}-{year}");
        });
        return reports;
    }
}

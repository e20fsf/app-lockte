using Lockte.Purch;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Purch;

/// <summary>The supplier and the score of one evaluation as RPT-07 prints it.</summary>
public sealed record SupplierEvaluationPrint(
    DateOnly EvaluationDate,
    int Quarter,
    DateOnly From,
    DateOnly To,
    SupplierDto Supplier,
    SupplierCounts Counts,
    int Point1,
    int Point2,
    SupplierScore Score);

/// <summary>
/// RPT-07 แบบผลการประเมินติดตามผู้ขายประจำไตรมาส (#254, BR-050), ระดับ B: ordering it counts the supplier's receipts that have a LOT in the period, keeps the counts
/// (replacing those of the same supplier and period) and prints the form with the score. A supplier with nothing counted prints "ไม่มีข้อมูล".
/// </summary>
public static class SupplierEvaluationReports
{
    public const string Title = "แบบผลการประเมินติดตามผู้ขายประจำไตรมาส";

    public static async Task<(SupplierEvaluationPrint? Print, IReadOnlyList<string> Errors)> BuildAsync(SupplierEvaluationRegistry evaluations, SupplierDirectory suppliers,
        PurchSettingsDirectory settings, int supplierId, DateOnly from, DateOnly to, int quarter, DateOnly evaluationDate, int point1, int point2)
    {
        var result = await evaluations.EvaluateAsync(new SupplierEvaluationInput(supplierId, from, to, quarter, point1, point2));
        if (!result.Succeeded) return (null, result.Errors);
        if (result.Evaluation is null) return (null, []);
        var supplier = (await suppliers.GetAsync(supplierId))!;
        return (new SupplierEvaluationPrint(evaluationDate, quarter, from, to, supplier, result.Counts, point1, point2, result.Evaluation.Score), []);
    }

    public static RouteGroupBuilder MapSupplierEvaluationReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/purch/evaluation", async (int supplier, DateOnly? from, DateOnly? to, int quarter, DateOnly? date, int point1, int point2, HttpContext http,
            SupplierEvaluationRegistry evaluations, SupplierDirectory suppliers, PurchSettingsDirectory settings, TimeProvider clock, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (from is null || to is null) return Results.BadRequest("กรุณากรอกช่วงวันที่");
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var (print, errors) = await BuildAsync(evaluations, suppliers, settings, supplier, from.Value, to.Value, quarter, date ?? today, point1, point2);
            if (errors.Count > 0) return Results.BadRequest(string.Join(" ", errors));
            var pdf = await printer.PrintAsync<SupplierEvaluationView>(Title, Paper.A4Portrait, new() { [nameof(SupplierEvaluationView.Report)] = print }, AccountClaims.DisplayNameOf(http.User),
                DocumentKeys.SupplierEvaluation, cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "ประเมินผู้ขาย " + (print?.Supplier.Code ?? supplier.ToString()));
        });
        return reports;
    }
}

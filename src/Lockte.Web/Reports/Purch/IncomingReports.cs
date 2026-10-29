using Lockte.Purch;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Purch;

/// <summary>The counts printed under RPT-05 / RPT-06: รวมทั้งหมด n รายการ; ใบรับรอง มี/ไม่มี; ส่งทัน/ไม่ทัน; ผ่าน/ไม่ผ่าน.</summary>
public sealed record IncomingTotals(int Count, int WithCertificate, int WithoutCertificate, int OnTime, int Late, int Passed, int Failed)
{
    public static IncomingTotals Of(IReadOnlyList<IncomingRow> rows) => new(
        rows.Count, rows.Count(r => r.HasCertificate == true), rows.Count(r => r.HasCertificate == false), rows.Count(r => r.OnTime), rows.Count(r => !r.OnTime),
        rows.Count(r => !r.Failed), rows.Count(r => r.Failed));
}

/// <summary>What RPT-05 / RPT-06 print: the period, the supplier asked for (null = ทุกราย), the rows and their totals.</summary>
public sealed record IncomingReport(DateOnly From, DateOnly To, string? SupplierName, IReadOnlyList<IncomingRow> Rows)
{
    public IncomingTotals Totals => IncomingTotals.Of(Rows);
}

/// <summary>
/// RPT-05 บันทึกการรับของ (PC-PU002-3/2547 ฉบับที่ 2) and RPT-06 บันทึกการทำ INCOMING INSPECTION (II-QA001/2548 ฉบับที่ 1), ระดับ B on A4 ตั้ง (#257): the receipts of the period
/// that QA gave a LOT (BR-035) — never a receipt without one — of one supplier or all. RPT-05 is by วันที่รับ, RPT-06 by supplier then วันที่รับ.
/// </summary>
public static class IncomingReports
{
    public const string RecordTitle = "บันทึกการรับของ";
    public const string InspectionTitle = "บันทึกการทำ INCOMING INSPECTION";

    public static async Task<IncomingReport> BuildAsync(GoodsReceiptDirectory receipts, SupplierDirectory suppliers, DateOnly from, DateOnly to, int? supplierId, bool mainOnly)
    {
        var rows = await receipts.GetIncomingAsync(from, to, supplierId, mainOnly);
        var name = supplierId is { } id ? (await suppliers.GetAsync(id))?.Name : null;
        return new IncomingReport(from, to, name, rows);
    }

    public static RouteGroupBuilder MapIncomingReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/purch/incoming/{kind}", async (string kind, DateOnly? from, DateOnly? to, int? supplier, bool? mainOnly, GoodsReceiptDirectory receipts, SupplierDirectory suppliers,
            HttpContext http, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (kind is not ("record" or "inspection")) return Results.NotFound();
            if (from is null || to is null) return Results.BadRequest("กรุณากรอกช่วงวันที่");
            if (from > to) return Results.BadRequest("ตั้งแต่วันที่ต้องไม่หลังถึงวันที่");
            var report = await BuildAsync(receipts, suppliers, from.Value, to.Value, supplier, mainOnly ?? false);
            var user = AccountClaims.DisplayNameOf(http.User);
            var pdf = kind == "record"
                ? await printer.PrintAsync<IncomingRecordView>(RecordTitle, Paper.A4Portrait, new() { [nameof(IncomingRecordView.Report)] = report }, user, DocumentKeys.IncomingRecord, cancellationToken)
                : await printer.PrintAsync<IncomingInspectionView>(InspectionTitle, Paper.A4Landscape, new() { [nameof(IncomingInspectionView.Report)] = report }, user, DocumentKeys.IncomingInspection, cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, kind == "record" ? "บันทึกการรับของ" : "INCOMING INSPECTION");
        });
        return reports;
    }
}

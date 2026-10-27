using Lockte.Prod;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Order;

/// <summary>RPT-15 การใช้งานของพิมพ์ถึงวันที่ …: the total of every mold in use at a cut date.</summary>
public sealed record MoldCumulativeReport(DateOnly CutDate, IReadOnlyList<MoldCumulativeLine> Lines);

/// <summary>RPT-15 STOCKพิมพ์: the usage of one mold in a range. The head names the mold and the range (D-10); the legacy head named neither.</summary>
public sealed record MoldStockReport(string MoldCode, string MoldName, DateOnly From, DateOnly To, IReadOnlyList<MoldUsageLine> Lines)
{
    public int Total => Lines.Sum(l => l.Shots);
}

/// <summary>RPT-15 รายงานการใช้งานแม่พิมพ์ (#125), both ระดับ B on A4 ตั้ง.</summary>
public static class MoldUsageReports
{
    public const string CumulativeTitle = "การใช้งานของพิมพ์ถึงวันที่";
    public const string StockTitle = "การใช้งานแม่พิมพ์ รายตัว";

    public static async Task<MoldCumulativeReport> CumulativeAsync(MoldUsageDirectory usage, DateOnly cutDate)
        => new(cutDate, await usage.GetCumulativeAsync(cutDate));

    public static async Task<MoldStockReport?> StockAsync(MoldUsageDirectory usage, MoldDirectory molds, int moldId, DateOnly from, DateOnly to)
        => await molds.GetAsync(moldId) is { } mold ? new(mold.Code, mold.Name, from, to, await usage.GetUsageAsync(moldId, from, to)) : null;

    public static RouteGroupBuilder MapMoldUsageReports(this RouteGroupBuilder reports)
    {
        var group = reports.MapGroup("/order/molds");

        group.MapGet("/usage", async (DateOnly? cut, HttpContext http, MoldUsageDirectory usage, ReportPrinter printer, CancellationToken ct) =>
        {
            if (cut is null) return Results.BadRequest("กรุณาเลือกวันตัดยอด");
            var report = await CumulativeAsync(usage, cut.Value);
            var title = $"{CumulativeTitle} {ThaiDate.Format(cut.Value)}";
            return await HrAttendanceReports.PrintAsync<MoldCumulativeView>(http, printer, title, Paper.A4Portrait, report, title, ct);
        });

        group.MapGet("/stock", async (int? mold, DateOnly? from, DateOnly? to, HttpContext http, MoldUsageDirectory usage, MoldDirectory molds, ReportPrinter printer, CancellationToken ct) =>
        {
            if (HrAttendanceReports.Range(from, to) is { } bad) return bad;
            if (mold is null) return Results.BadRequest("กรุณาเลือกแม่พิมพ์");
            if (await StockAsync(usage, molds, mold.Value, from!.Value, to!.Value) is not { } report) return Results.NotFound("ไม่พบแม่พิมพ์");
            return await HrAttendanceReports.PrintAsync<MoldStockView>(http, printer, StockTitle, Paper.A4Portrait, report, $"{StockTitle} {report.MoldCode}", ct);
        });

        return reports;
    }
}

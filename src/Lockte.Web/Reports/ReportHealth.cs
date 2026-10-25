using System.Diagnostics;
using System.Net;

namespace Lockte.Web.Reports;

/// <summary>
/// <c>GET /health/reports</c> — the installer's check that Chromium prints under the IIS app pool (#21): one A4 page
/// of Thai text through the same <see cref="ReportPdfRenderer"/> as every report. Only from the server itself (no
/// login, so it must not be reachable from the network); <c>?pdf=1</c> returns the PDF instead of the timings.
/// </summary>
public static class ReportHealth
{
    public const string Path = "/health/reports";

    /// <summary>The test page: the stacked vowels and tone marks that break first when shaping goes wrong.</summary>
    public static string Html() => FormHtml.Compose("ตรวจระบบพิมพ์รายงาน", Paper.A4Portrait,
        "<div class=\"page\"><div class=\"t\" style=\"left:20mm;top:20mm;font-size:16pt\">"
        + "ตรวจระบบพิมพ์รายงาน — ปิ่น ปี๊บ ฟ้า ฝั่ง น้ำ ผู้ใหญ่ ญี่ปุ่น ฐิติ ฎีกา ฤๅษี ศักดิ์ฤทธิ์ ๑๒๓๔๕</div></div>");

    /// <summary>A request from the server itself: no remote address (in-process) or a loopback one (IIS on the same machine).</summary>
    public static bool IsLocal(HttpContext http) => http.Connection.RemoteIpAddress is not { } remote || IPAddress.IsLoopback(remote);

    public static IEndpointRouteBuilder MapReportHealth(this IEndpointRouteBuilder app)
    {
        app.MapGet(Path, async (HttpContext http, ReportPdfRenderer renderer, string? pdf, CancellationToken cancellationToken) =>
        {
            if (!IsLocal(http)) return Results.NotFound();
            var watch = Stopwatch.StartNew();
            try
            {
                var bytes = await renderer.RenderAsync(Html(), Paper.A4Portrait, cancellationToken);
                if (pdf is "1" or "true") return Results.File(bytes, "application/pdf", "health-reports.pdf");
                return Results.Ok(new { ok = true, ms = watch.ElapsedMilliseconds, bytes = bytes.Length });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Json(new { ok = false, ms = watch.ElapsedMilliseconds, error = ex.GetType().Name + ": " + ex.Message },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });
        return app;
    }
}

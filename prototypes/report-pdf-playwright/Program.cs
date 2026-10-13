// PROTOTYPE (throwaway) — answers issue #21: can Razor → Playwright/Chromium PDF reproduce
// แบบ คร.2 and ทะเบียนผู้ประกันตน, and does it run under an IIS app pool? Not production code.
using System.Globalization;
using System.Text.Json;
using Microsoft.Playwright;
using ReportPdfProto;
using ReportPdfProto.Reports;

// Browsers ship inside the app folder: the IIS app-pool identity has no usable user profile.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")))
    Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.Combine(AppContext.BaseDirectory, "ms-playwright"));
if (!Directory.Exists(PdfService.BrowsersPath) || Directory.GetDirectories(PdfService.BrowsersPath).Length == 0)
    Microsoft.Playwright.Program.Main(["install", "--only-shell", "chromium"]); // needs internet once

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<PdfService>();
var app = builder.Build();
app.Use((c, next) =>
{
    PdfService.FontMode = c.Request.Query["font"].FirstOrDefault() ?? "route";
    PdfService.Pool = c.Request.Query["pool"] != "0";
    return next(c);
});
var employees = FakeData.Employees(450);
var warmSw = System.Diagnostics.Stopwatch.StartNew();
await app.Services.GetRequiredService<PdfService>().Warm(PdfService.PoolSize);
Console.WriteLine($"[warm] browser + {PdfService.PoolSize} pages in {warmSw.ElapsedMilliseconds} ms");
var startedAt = DateTime.Now;

static string Inv(double v) => v.ToString(CultureInfo.InvariantCulture);
double Q(HttpContext c, string k, double d) => double.TryParse(c.Request.Query[k], CultureInfo.InvariantCulture, out var v) ? v : d;

const string Base = "html,body{margin:0}body{font-family:'THSarabunNew';font-size:var(--pt);-webkit-print-color-adjust:exact}.at{position:absolute;white-space:pre;line-height:1}";

// Font size and margins are calibration knobs: legacy font size/page setup were not recoverable — tune against the printed Access sample (#25).
(string css, PagePdfOptions opt) Kr2Setup(HttpContext c)
{
    var css = Base + $":root{{--pt:{Inv(Q(c, "pt", 15))}pt}}.kr2{{position:relative;height:25.9cm}}"
            + (c.Request.Query.ContainsKey("grid") ? ".kr2{background:repeating-linear-gradient(#0000 0 0.98cm,#f008 0.98cm 1cm),repeating-linear-gradient(90deg,#0000 0 0.98cm,#f008 0.98cm 1cm)}" : "");
    return (css, new PagePdfOptions
    {
        Format = "A4", PrintBackground = true,
        Margin = new() { Left = $"{Inv(Q(c, "ml", 2.0))}cm", Top = $"{Inv(Q(c, "mt", 1.5))}cm", Right = "0.5cm", Bottom = "0.5cm" },
    });
}

(string css, PagePdfOptions opt) RegisterSetup(HttpContext c)
{
    var css = Base + $":root{{--pt:{Inv(Q(c, "pt", 14))}pt}}"
        + ".reg{width:26.1cm;table-layout:fixed;border-collapse:collapse}"
        + ".reg th.pagehead{padding:0;font-weight:normal}.head{position:relative;height:2.9cm;text-align:left}"
        + ".reg tr.cols th{font-weight:normal;text-align:left;border-top:1px solid #000;border-bottom:1px solid #000;padding:2px 3px}"
        + ".reg td{vertical-align:top;padding:1px 3px;line-height:1.15}.reg td.num{text-align:right}"
        + ".reg tbody tr{break-inside:avoid}.reg td.pagefoot{padding-top:6px;border-top:1px solid #000}";
    // Access report has no page number; criterion asks for one → Chromium footer template (has its own font/CSS scope).
    // Header/footer templates render outside the page: no request interception, so use a Thai system font (present on Windows Server).
    var footer = "<div style=\"width:100%;font-family:'Leelawadee UI',Tahoma,sans-serif;font-size:9pt;text-align:right;padding-right:1cm\">หน้า <span class=\"pageNumber\"></span>/<span class=\"totalPages\"></span></div>";
    return (css, new PagePdfOptions
    {
        Format = "A4", Landscape = true, PrintBackground = true,
        DisplayHeaderFooter = true, HeaderTemplate = "<span></span>", FooterTemplate = footer,
        Margin = new() { Left = "1.2cm", Right = "1.2cm", Top = "1cm", Bottom = "1.6cm" },
    });
}

const string ThaiCss = "body{font-family:'THSarabunNew';font-size:16pt}.big{font-size:28pt}.cols{display:flex;gap:.6cm;align-items:flex-start}.box{border:1px solid #888;padding:4px}";

async Task<IResult> Pdf(HttpContext c, Task<RenderResult> render, string name)
{
    var r = await render;
    c.Response.Headers["X-Html-Ms"] = r.HtmlMs.ToString();
    c.Response.Headers["X-Pdf-Ms"] = r.PdfMs.ToString();
    return Results.File(r.Pdf, "application/pdf", c.Request.Query.ContainsKey("download") ? name : null);
}

app.MapGet("/", () => Results.Content("""
    <meta charset="utf-8"><body style="font-family:sans-serif">
    <h3>PROTOTYPE — Razor → Playwright PDF (#21) · ข้อมูลปลอมทั้งหมด</h3>
    <ul>
      <li><a href="kr2?i=0">แบบ คร.2</a> · <a href="kr2?i=0&grid=1">+ ตาราง 1 ซม. (เทียบตำแหน่ง)</a> — ปรับ <code>pt</code>, <code>ml</code>, <code>mt</code> (ซม.)</li>
      <li><a href="register">ทะเบียนผู้ประกันตน 450 แถว</a> · <a href="register?rows=1200">1,200 แถว</a></li>
      <li><a href="thai">ทดสอบการตัดคำ/สระซ้อน</a></li>
      <li><a href="bench">bench (เวลา + RAM)</a> · <a href="env">env</a></li>
    </ul>
    """, "text/html; charset=utf-8"));

app.MapGet("/kr2", (HttpContext c, PdfService pdf) =>
{
    var (css, opt) = Kr2Setup(c);
    var e = employees[(int)Q(c, "i", 0) % employees.Count];
    return Pdf(c, pdf.Render<Kr2>(new() { ["E"] = e }, css, opt), "kr2.pdf");
});

app.MapGet("/register", (HttpContext c, PdfService pdf) =>
{
    var (css, opt) = RegisterSetup(c);
    var rows = (int)Q(c, "rows", 450);
    var data = rows <= employees.Count ? employees.Take(rows).ToList() : FakeData.Employees(rows, seed: 7);
    return Pdf(c, pdf.Render<Register>(new() { ["Rows"] = data }, css, opt), "register.pdf");
});

app.MapGet("/thai", (HttpContext c, PdfService pdf) =>
    Pdf(c, pdf.Render<ThaiTest>([], ThaiCss, new() { Format = "A4", Landscape = true, Margin = new() { Top = "1cm", Left = "1cm" } }), "thai.pdf"));

// /bench?n=5&parallel=5 — sequential timings per report, then N concurrent ทะเบียนผู้ประกันตน (≈ 5 concurrent users).
app.MapGet("/bench", async (HttpContext c, PdfService pdf) =>
{
    var n = (int)Q(c, "n", 5); var par = (int)Q(c, "parallel", 5);
    var before = PdfService.Memory();
    async Task<object> Seq(string name, Func<Task<RenderResult>> f)
    {
        var runs = new List<long>(); long size = 0; var peak = before;
        for (var i = 0; i < n; i++)
        {
            var r = await f(); runs.Add(r.HtmlMs + r.PdfMs); size = r.Pdf.Length;
            var m = PdfService.Memory(); if (m.appMb + m.chromiumMb > peak.appMb + peak.chromiumMb) peak = m;
        }
        return new { name, msEach = runs, firstMs = runs[0], avgWarmMs = runs.Skip(1).DefaultIfEmpty(runs[0]).Average(), pdfKb = size / 1024, peakAppMb = peak.appMb, peakChromiumMb = peak.chromiumMb, chromiumProcs = peak.chromiumProcs };
    }
    var (kcss, kopt) = Kr2Setup(c); var (rcss, ropt) = RegisterSetup(c);
    var kr2 = await Seq("kr2", () => pdf.Render<Kr2>(new() { ["E"] = employees[0] }, kcss, kopt));
    var reg = await Seq("register-450", () => pdf.Render<Register>(new() { ["Rows"] = employees }, rcss, ropt));
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var parallel = await Task.WhenAll(Enumerable.Range(0, par).Select(_ => pdf.Render<Register>(new() { ["Rows"] = employees }, rcss, ropt)));
    var afterPar = PdfService.Memory();
    return Results.Json(new
    {
        before = new { before.appMb, before.chromiumMb },
        results = new[] { kr2, reg },
        parallelRegister = new { count = par, wallMs = sw.ElapsedMilliseconds, eachMs = parallel.Select(r => r.HtmlMs + r.PdfMs), afterAppMb = afterPar.appMb, afterChromiumMb = afterPar.chromiumMb, afterPar.chromiumProcs },
    }, new JsonSerializerOptions { WriteIndented = true });
});

app.MapGet("/env", () => Results.Json(new
{
    user = Environment.UserName,
    os = Environment.OSVersion.ToString(),
    dotnet = Environment.Version.ToString(),
    iisAppPool = Environment.GetEnvironmentVariable("APP_POOL_ID"),
    underIis = Environment.GetEnvironmentVariable("ASPNETCORE_IIS_PHYSICAL_PATH") is not null,
    browsersPath = PdfService.BrowsersPath,
    startedAt,
    memory = PdfService.Memory() is var m ? new { m.appMb, m.chromiumMb, m.chromiumProcs } : null,
}));

app.Run();

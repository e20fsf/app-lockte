// PROTOTYPE — Razor component → HTML string → headless Chromium (Playwright) → PDF.
// One shared browser per app; a fresh page per request.
using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Playwright;

namespace ReportPdfProto;

public record RenderResult(byte[] Pdf, long HtmlMs, long PdfMs, int HtmlKb);

public sealed class PdfService(IServiceProvider services, ILoggerFactory loggers) : IAsyncDisposable
{
    public static string BrowsersPath => Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")!;

    readonly SemaphoreSlim _launch = new(1, 1);
    readonly System.Collections.Concurrent.ConcurrentBag<IPage> _pool = new();
    IPlaywright? _pw;
    IBrowser? _browser;
    IBrowserContext? _ctx;
    static string? _embedCss;

    // PROTO switches (set per request from the query string; racy, fine for a spike):
    //   font = route (default: font file served by Playwright request interception)
    //        | embed (base64 data: URI in the HTML) | local (font installed in Windows)
    //   pool = true (default: reuse warm pages) | false (new page per job)
    public static string FontMode = "route";
    public static bool Pool = true;
    const string FontHost = "https://report-fonts.invalid/";

    public static string FontCss => FontMode switch
    {
        "local" => "@font-face{font-family:'THSarabunNew';src:local('TH Sarabun New')}@font-face{font-family:'THSarabunNew';font-weight:bold;src:local('TH Sarabun New Bold')}",
        "embed" => _embedCss ??= Face("THSarabunNew.ttf", "normal", true) + Face("THSarabunNew Bold.ttf", "bold", true),
        _ => Face("THSarabunNew.ttf", "normal", false) + Face("THSarabunNew Bold.ttf", "bold", false),
    };

    static string Face(string file, string weight, bool embed) =>
        $"@font-face{{font-family:'THSarabunNew';font-weight:{weight};src:url({(embed ? "data:font/ttf;base64," + Convert.ToBase64String(File.ReadAllBytes(FontPath(file))) : FontHost + Uri.EscapeDataString(file))}) format('truetype');}}";

    static string FontPath(string file) => Path.Combine(AppContext.BaseDirectory, file);

    async Task<IBrowserContext> Context()
    {
        if (_ctx is not null && _browser is { IsConnected: true }) return _ctx;
        await _launch.WaitAsync();
        try
        {
            if (_ctx is not null && _browser is { IsConnected: true }) return _ctx;
            _pw ??= await Playwright.CreateAsync();
            _browser = await _pw.Chromium.LaunchAsync(new() { Headless = true });
            _ctx = await _browser.NewContextAsync();
            // Fonts are served from the app folder through interception: no network, no IIS static files, no 1 MB data: URI per job.
            await _ctx.RouteAsync(FontHost + "**", route => route.FulfillAsync(new()
            {
                BodyBytes = File.ReadAllBytes(FontPath(Uri.UnescapeDataString(new Uri(route.Request.Url).AbsolutePath.TrimStart('/')))),
                ContentType = "font/ttf",
                Headers = new Dictionary<string, string> { ["Cache-Control"] = "max-age=31536000", ["Access-Control-Allow-Origin"] = "*" },
            }));
            return _ctx;
        }
        finally { _launch.Release(); }
    }

    async Task<IPage> Rent()
    {
        var ctx = await Context();
        if (Pool && _pool.TryTake(out var p) && !p.IsClosed) return p;
        return await ctx.NewPageAsync();
    }

    // Pool size bounds Chromium RAM (VM has 8 GB shared with SQL Server). Pages are emptied before pooling.
    public static int PoolSize = 3;
    readonly SemaphoreSlim _slots = new(PoolSize, PoolSize); // queue extra jobs instead of spawning renderers

    async Task Return(IPage page)
    {
        if (Pool && _pool.Count < PoolSize)
        {
            await page.SetContentAsync("<!doctype html><title>idle</title>");
            _pool.Add(page);
        }
        else await page.CloseAsync();
    }

    // Opening a page spawns a renderer process (~5 s on a machine with endpoint security) — do it at startup, not per request.
    public async Task Warm(int pages)
    {
        var ctx = await Context();
        var warm = await Task.WhenAll(Enumerable.Range(0, pages).Select(_ => ctx.NewPageAsync()));
        foreach (var p in warm) _pool.Add(p);
    }

    public async Task<string> RenderHtml<T>(Dictionary<string, object?> parameters, string css) where T : IComponent
    {
        await using var hr = new HtmlRenderer(services, loggers);
        var body = await hr.Dispatcher.InvokeAsync(async () =>
            (await hr.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
        return $"<!doctype html><html lang=\"th\"><head><meta charset=\"utf-8\"><style>{FontCss}\n{css}</style></head><body>{body}</body></html>";
    }

    public async Task<RenderResult> Render<T>(Dictionary<string, object?> parameters, string css, PagePdfOptions options) where T : IComponent
    {
        var sw = Stopwatch.StartNew();
        var html = await RenderHtml<T>(parameters, css);
        var htmlMs = sw.ElapsedMilliseconds;
        await _slots.WaitAsync();
        var page = await Rent();
        var ok = false;
        try
        {
            var t0 = sw.ElapsedMilliseconds;
            await page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.Load });
            await page.EvaluateAsync("document.fonts.ready");
            var t1 = sw.ElapsedMilliseconds;
            var pdf = await page.PdfAsync(options);
            Console.WriteLine($"[pdf] {typeof(T).Name} font={FontMode} pool={Pool} getPage={t0 - htmlMs}ms setContent={t1 - t0}ms pdf={sw.ElapsedMilliseconds - t1}ms html={html.Length / 1024}KB");
            ok = true;
            return new RenderResult(pdf, htmlMs, sw.ElapsedMilliseconds - htmlMs, html.Length / 1024);
        }
        finally
        {
            if (ok) await Return(page);
            else await page.CloseAsync(); // never pool a crashed/failed page
            _slots.Release();
        }
    }

    // Working set (MB) of this process and of the Chromium processes Playwright started from BrowsersPath.
    public static (double appMb, double chromiumMb, int chromiumProcs) Memory()
    {
        double chrome = 0; var n = 0;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (!p.ProcessName.Contains("chrome", StringComparison.OrdinalIgnoreCase) && !p.ProcessName.Contains("headless", StringComparison.OrdinalIgnoreCase)) continue;
                if (p.MainModule?.FileName?.StartsWith(BrowsersPath, StringComparison.OrdinalIgnoreCase) != true) continue;
                chrome += p.WorkingSet64; n++;
            }
            catch { /* other users' processes */ }
            finally { p.Dispose(); }
        }
        using var self = Process.GetCurrentProcess();
        return (Math.Round(self.WorkingSet64 / 1048576.0, 1), Math.Round(chrome / 1048576.0, 1), n);
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _pw?.Dispose();
    }
}

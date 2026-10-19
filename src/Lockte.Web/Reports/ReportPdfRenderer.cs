using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Lockte.Web.Reports;

/// <summary>Settings of section <c>Reports</c>.</summary>
public sealed class ReportOptions
{
    public const string Section = "Reports";

    /// <summary>
    /// Warm Chromium pages kept open and the most prints at once (#21: each page is a renderer process, ~5 s to open
    /// on a PC with endpoint security, up to ~860 MB RAM in total for 3). Prints beyond it wait for a page.
    /// </summary>
    public int PagePoolSize { get; set; } = 3;

    /// <summary>
    /// Where Chromium is installed (env <c>PLAYWRIGHT_BROWSERS_PATH</c>). Blank = Playwright's default under the user
    /// profile; on IIS set a folder of the app, as the app pool identity has no usable profile (#21).
    /// </summary>
    public string? BrowsersPath { get; set; }

    /// <summary>Download Chromium headless shell on first use when it is missing (needs internet once).</summary>
    public bool InstallBrowser { get; set; } = true;

    /// <summary>Open Chromium and the page pool when the app starts (in the background), not on the first print.</summary>
    public bool WarmUpOnStart { get; set; } = true;

    /// <summary>The longest one print may take.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(90);
}

/// <summary>
/// HTML → PDF with one headless Chromium for the app and a pool of warm pages (#21). Fonts are served from
/// <c>Reports/Fonts</c> by request interception and Chromium embeds them (subset) in the PDF. A page that failed is
/// closed, never pooled; a browser that died is started again on the next print.
/// </summary>
public sealed class ReportPdfRenderer(IOptions<ReportOptions> options, ILogger<ReportPdfRenderer> logger) : IAsyncDisposable
{
    private readonly ReportOptions settings = options.Value;
    private readonly SemaphoreSlim launch = new(1, 1);
    private readonly SemaphoreSlim slots = new(Math.Max(1, options.Value.PagePoolSize), Math.Max(1, options.Value.PagePoolSize));
    private readonly ConcurrentBag<IPage> idle = [];
    private readonly ConcurrentDictionary<string, byte[]> fonts = new();
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IBrowserContext? context;
    private bool disposed;

    /// <summary>Starts Chromium and fills the page pool.</summary>
    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await ContextAsync(cancellationToken);
        var missing = Math.Max(1, settings.PagePoolSize) - idle.Count;
        if (missing <= 0) return;
        var pages = await Task.WhenAll(Enumerable.Range(0, missing).Select(_ => ctx.NewPageAsync()));
        foreach (var page in pages) idle.Add(page);
    }

    /// <summary>One PDF of <paramref name="html"/> (from <see cref="ReportHtml.Compose"/>) on <paramref name="paper"/>.</summary>
    public async Task<byte[]> RenderAsync(string html, Paper paper, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await slots.WaitAsync(cancellationToken);
        IPage? page = null;
        var ok = false;
        try
        {
            page = await RentAsync(cancellationToken);
            var timeout = (float)settings.Timeout.TotalMilliseconds;
            page.SetDefaultTimeout(timeout);
            var (width, height) = ReportHtml.PageMillimetres(paper);
            await page.SetViewportSizeAsync(Pixels(width - 2 * ReportHtml.MarginSide), Pixels(height - ReportHtml.MarginTop - ReportHtml.MarginBottom));
            await page.SetContentAsync(html, new() { WaitUntil = WaitUntilState.Load, Timeout = timeout });
            await page.EvaluateAsync("async () => { await document.fonts.ready; window.lockteFitToPage?.(); }");
            var pdf = await page.PdfAsync(new() { PreferCSSPageSize = true, PrintBackground = true });
            ok = true;
            return pdf;
        }
        finally
        {
            if (page is not null)
            {
                if (ok) await ReturnAsync(page);
                else await CloseQuietlyAsync(page);
            }
            slots.Release();
        }
    }

    /// <summary>CSS pixels (96 per inch) of a length in mm.</summary>
    private static int Pixels(double mm) => (int)Math.Floor(mm / 25.4 * 96);

    private async Task<IPage> RentAsync(CancellationToken cancellationToken)
    {
        var ctx = await ContextAsync(cancellationToken);
        while (idle.TryTake(out var page))
        {
            if (!page.IsClosed && page.Context == ctx) return page;
            await CloseQuietlyAsync(page);
        }
        logger.LogInformation("Opening a new Chromium page for reports (none idle)");
        return await ctx.NewPageAsync();
    }

    private async Task ReturnAsync(IPage page)
    {
        try
        {
            if (idle.Count < settings.PagePoolSize && !page.IsClosed)
            {
                await page.SetContentAsync("<!doctype html><title>idle</title>"); // let go of the report's memory
                idle.Add(page);
                return;
            }
        }
        catch (PlaywrightException ex)
        {
            logger.LogWarning(ex, "A report page could not be emptied; closing it");
        }
        await CloseQuietlyAsync(page);
    }

    private static async Task CloseQuietlyAsync(IPage page)
    {
        try { await page.CloseAsync(); }
        catch (PlaywrightException) { /* already gone with its browser */ }
    }

    private async Task<IBrowserContext> ContextAsync(CancellationToken cancellationToken)
    {
        if (context is not null && browser is { IsConnected: true }) return context;
        await launch.WaitAsync(cancellationToken);
        try
        {
            if (context is not null && browser is { IsConnected: true }) return context;
            if (browser is not null) logger.LogWarning("Chromium for reports was not connected; starting it again");
            if (!string.IsNullOrWhiteSpace(settings.BrowsersPath))
                Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", settings.BrowsersPath);
            if (settings.InstallBrowser) await Task.Run(InstallChromium, cancellationToken);
            playwright ??= await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var ctx = await browser.NewContextAsync();
            await ctx.RouteAsync(ReportHtml.FontHost + "**", route => route.FulfillAsync(new()
            {
                BodyBytes = Font(Uri.UnescapeDataString(new Uri(route.Request.Url).AbsolutePath.TrimStart('/'))),
                ContentType = "font/ttf",
                Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
            }));
            context = ctx;
            return ctx;
        }
        finally
        {
            launch.Release();
        }
    }

    /// <summary>Only the files of <see cref="ReportHtml.Fonts"/>, from the app folder.</summary>
    private byte[] Font(string file)
    {
        if (!ReportHtml.Fonts.Any(f => f.File == file)) return [];
        return fonts.GetOrAdd(file, f => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Reports", "Fonts", f)));
    }

    /// <summary>Playwright's own installer: does nothing when this Chromium is there already.</summary>
    private void InstallChromium()
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", "--only-shell", "chromium"]);
        if (exitCode != 0) logger.LogError("Installing Chromium for reports failed with exit code {ExitCode}", exitCode);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        if (browser is not null) await browser.CloseAsync();
        playwright?.Dispose();
    }
}

/// <summary>Opens Chromium and the page pool in the background at startup (28–35 s on the VM, #21) so the first print is quick.</summary>
internal sealed class ReportWarmUp(ReportPdfRenderer renderer, IOptions<ReportOptions> options, ILogger<ReportWarmUp> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WarmUpOnStart) return;
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await renderer.WarmUpAsync(stoppingToken);
            logger.LogInformation("Chromium for reports ready with {Pages} pages in {Ms} ms", options.Value.PagePoolSize, watch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Chromium for reports could not start; the first print will try again");
        }
    }
}

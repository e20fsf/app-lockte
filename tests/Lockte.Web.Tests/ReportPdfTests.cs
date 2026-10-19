using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lockte.Web.Reports;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lockte.Web.Tests;

/// <summary>
/// One Chromium for the test run (installed on first use, like the app does), shared by <see cref="ReportPdfTests"/>.
/// </summary>
public sealed class ChromiumFixture : IAsyncLifetime
{
    public ReportPdfRenderer Renderer { get; } = new(Options.Create(new ReportOptions { PagePoolSize = 2 }), NullLogger<ReportPdfRenderer>.Instance);

    public Task InitializeAsync() => Renderer.WarmUpAsync();

    public async Task DisposeAsync() => await Renderer.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class ChromiumCollection : ICollectionFixture<ChromiumFixture>
{
    public const string Name = "Chromium";
}

/// <summary>
/// The report pipeline end to end (#52, #21): HTML → pooled Chromium page → PDF, with TH Sarabun New embedded and the
/// page sizes of #40. What the pages look like is checked by eye (#41: no automated test of PDF looks).
/// </summary>
[Collection(ChromiumCollection.Name)]
public sealed class ReportPdfTests(ChromiumFixture chromium)
{
    private static readonly PrintStamp Stamp = new(DateTimeOffset.Now, "tester");

    private static string Html(Paper paper, int rows) => ReportHtml.Compose(new ReportDocument("ทดสอบรายงาน", paper, "WI-TEST-1/2569 ฉบับที่ 1"), Stamp,
        "<table><thead><tr><th>ลำดับ</th><th>ชื่อ</th></tr></thead><tbody>"
        + string.Concat(Enumerable.Range(1, rows).Select(i => $"<tr><td>{i}</td><td>ญี่ปุ่น ฤๅษี ปิ่น ป่ำ น้ำ ศักดิ์ฤทธิ์ {i}</td></tr>"))
        + "</tbody></table>");

    private static string Text(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    private static (double Width, double Height) MediaBox(byte[] pdf)
    {
        var m = Regex.Match(Text(pdf), @"/MediaBox \[0 0 ([\d.]+) ([\d.]+)\]");
        Assert.True(m.Success, "no MediaBox");
        return (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    private static int Pages(byte[] pdf) => Regex.Matches(Text(pdf), @"/Type /Page\b").Count;

    [Fact] // ฟอนต์ TH Sarabun New ฝังใน PDF ทุกระดับ (#40)
    public async Task A_report_becomes_a_pdf_with_TH_Sarabun_New_embedded()
    {
        var pdf = await chromium.Renderer.RenderAsync(Html(Paper.A4Portrait, 5), Paper.A4Portrait);

        Assert.StartsWith("%PDF-", Text(pdf));
        Assert.Contains("THSarabunNew", Text(pdf));
        Assert.Contains("/FontFile2", Text(pdf));
        Assert.Equal(1, Pages(pdf));
    }

    [Theory] // A4 ตั้ง/นอน และ A5 ตั้ง/นอน (#40) — in points, 1 mm = 72/25.4
    [InlineData(Paper.A4Portrait, 210, 297)]
    [InlineData(Paper.A4Landscape, 297, 210)]
    [InlineData(Paper.A5Portrait, 148, 210)]
    [InlineData(Paper.A5Landscape, 210, 148)]
    public async Task Each_paper_gives_its_page_size(Paper paper, double widthMm, double heightMm)
    {
        var (width, height) = MediaBox(await chromium.Renderer.RenderAsync(Html(paper, 1), paper));

        Assert.Equal(widthMm * 72 / 25.4, width, 0);
        Assert.Equal(heightMm * 72 / 25.4, height, 0);
    }

    [Fact]
    public async Task A_long_report_runs_over_several_pages()
        => Assert.True(Pages(await chromium.Renderer.RenderAsync(Html(Paper.A4Portrait, 200), Paper.A4Portrait)) >= 3);

    [Fact] // more prints at once than pooled pages: the rest wait their turn (#21)
    public async Task Prints_at_the_same_time_share_the_pool()
    {
        var pdfs = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => chromium.Renderer.RenderAsync(Html(Paper.A4Portrait, 30), Paper.A4Portrait)));

        Assert.All(pdfs, pdf => Assert.StartsWith("%PDF-", Text(pdf)));
    }
}

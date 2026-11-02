using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lockte.Web.Tests;

/// <summary>
/// #346/#348: a pooled Chromium page that printed an แบบกลาง report kept its <c>lockteFitToPage</c>, which then shrank the next
/// ระดับ A form to the report's printable width (A4 form → ×0.885 with the 12 mm side margins): small text, empty lower page.
/// </summary>
public sealed class FormAfterReportTests : IAsyncLifetime
{
    private readonly ReportPdfRenderer renderer = new(Options.Create(new ReportOptions { PagePoolSize = 1 }), NullLogger<ReportPdfRenderer>.Instance);

    public Task InitializeAsync() => renderer.WarmUpAsync();

    public async Task DisposeAsync() => await renderer.DisposeAsync();

    private static async Task<string> LetterAsync()
    {
        var letter = new ResignationLetterData("A005", "นางสาวทองใส  โชยรัมย์", "ประกอบ", "D", new DateOnly(1992, 10, 26),
            new DateOnly(2026, 10, 2), "กลับบ้าน", "นางสาวอัมพร   เจริญสุข", "นายสมชาย ใจดี", "นายสมบัติ  ตัวอย่างดี");
        return FormHtml.Compose("ใบลาออก", Paper.A4Portrait,
            await ReportBody.RenderAsync<ResignationLetterView>(new() { ["Letter"] = letter, ["Today"] = new DateOnly(2026, 10, 29) }));
    }

    [Fact]
    public async Task A_form_printed_after_a_report_on_the_same_page_is_not_shrunk()
    {
        var html = await LetterAsync();
        var fresh = await renderer.RenderAsync(html, Paper.A4Portrait);

        // pool of one: the report and the form share the page
        await renderer.RenderAsync(ReportHtml.Compose(new ReportDocument("ทดสอบ", Paper.A4Portrait), new PrintStamp(DateTimeOffset.Now, "t"),
            "<table><tr><td>x</td></tr></table>"), Paper.A4Portrait);
        var after = await renderer.RenderAsync(html, Paper.A4Portrait);

        Assert.Equal(fresh.Length, after.Length);
    }
}

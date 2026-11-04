using System.Text;
using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lockte.Web.Tests;

/// <summary>The font the HR documents print in on THIS machine: Angsana New when installed, else the fall-back (#395).</summary>
internal static class TestFonts
{
    public static ReportFontResolver Resolver(ReportOptions? options = null, Func<string, bool>? installed = null, ILogger<ReportFontResolver>? logger = null)
        => new(Options.Create(options ?? new ReportOptions()), logger ?? NullLogger<ReportFontResolver>.Instance) { IsInstalled = installed ?? InstalledFonts.Has };

    public static ReportFont Hr => Resolver().For(ReportProgram.Hr);

    /// <summary>What the PDF names the HR font: Chromium embeds the PostScript name of a local font.</summary>
    public static string HrPdfName => Hr.Key == ReportFonts.AngsanaNewKey ? "Angsana" : "THSarabunNew";
}

/// <summary>#395: HR documents in Angsana New from the server's fonts (never shipped), back to TH Sarabun New by one setting or when missing.</summary>
[Collection(ChromiumCollection.Name)]
public sealed class ReportFontTests(ChromiumFixture chromium)
{
    private sealed class Capture : ILogger<ReportFontResolver>
    {
        public List<(LogLevel Level, string Text)> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public void Hr_prints_in_Angsana_New_by_default_and_everything_else_in_TH_Sarabun_New()
    {
        var resolver = TestFonts.Resolver(installed: _ => true);

        Assert.Equal(ReportFonts.AngsanaNewKey, resolver.For(ReportProgram.Hr).Key);
        foreach (var other in new[] { ReportProgram.Order, ReportProgram.Prod, ReportProgram.Purch, ReportProgram.Calib, ReportProgram.Cash })
            Assert.Equal(ReportFonts.ThSarabunNewKey, resolver.For(other).Key);
    }

    [Fact] // the one setting that switches back
    public void One_setting_switches_a_program_back_or_over()
    {
        var options = new ReportOptions { ProgramFonts = new(StringComparer.OrdinalIgnoreCase) { ["Hr"] = "THSarabunNew", ["Cash"] = "AngsanaNew" } };
        var resolver = TestFonts.Resolver(options, _ => true);

        Assert.Equal(ReportFonts.ThSarabunNewKey, resolver.For(ReportProgram.Hr).Key);
        Assert.Equal(ReportFonts.AngsanaNewKey, resolver.For(ReportProgram.Cash).Key);
    }

    [Fact] // the licence: the font is the server's, never a file of the app
    public void Angsana_New_is_a_local_font_and_no_font_file_is_shipped()
    {
        Assert.Contains("local('Angsana New')", ReportFonts.AngsanaNew.FaceCss);
        Assert.DoesNotContain("url(", ReportFonts.AngsanaNew.FaceCss);
        var fonts = Path.Combine(AppContext.BaseDirectory, "Reports", "Fonts");
        Assert.DoesNotContain(Directory.GetFiles(fonts), f => Path.GetFileName(f).StartsWith("angsa", StringComparison.OrdinalIgnoreCase));
    }

    [Fact] // a machine without the font still prints, in TH Sarabun New, and says so
    public void A_missing_Angsana_New_falls_back_with_one_warning()
    {
        var log = new Capture();
        var resolver = TestFonts.Resolver(installed: _ => false, logger: log);

        Assert.Equal(ReportFonts.ThSarabunNewKey, resolver.For(ReportProgram.Hr).Key);
        Assert.Equal(ReportFonts.ThSarabunNewKey, resolver.For(ReportProgram.Hr).Key);

        var warning = Assert.Single(log.Lines, l => l.Level == LogLevel.Warning);
        Assert.Contains("Angsana New", warning.Text);
        Assert.Contains("NOT installed", warning.Text);
    }

    [Fact]
    public void An_unknown_font_name_falls_back_with_a_warning()
    {
        var log = new Capture();
        var resolver = TestFonts.Resolver(new ReportOptions { ProgramFonts = new() { ["Hr"] = "Comic" } }, _ => true, log);

        Assert.Equal(ReportFonts.ThSarabunNewKey, resolver.For(ReportProgram.Hr).Key);
        Assert.Contains(log.Lines, l => l.Level == LogLevel.Warning && l.Text.Contains("Comic"));
    }

    [Fact]
    public void A_document_names_only_the_font_it_is_given()
    {
        var html = FormHtml.Compose("x", Paper.A4Portrait, "", ReportFonts.AngsanaNew);
        Assert.Contains("font-family:'LockteAngsanaNew'", html);
        Assert.DoesNotContain("THSarabunNew", html);

        var report = ReportHtml.Compose(new ReportDocument("x", Paper.A4Portrait), new PrintStamp(DateTimeOffset.Now, "t"), "", ReportFonts.AngsanaNew);
        Assert.DoesNotContain("THSarabunNew", report);

        Assert.Contains("font-family:'THSarabunNew'", FormHtml.Compose("x", Paper.A4Portrait, ""));
    }

    [Fact] // a view's program comes from its namespace, so HR views need no per-call setting
    public void A_report_view_belongs_to_the_program_of_its_namespace()
    {
        Assert.Equal(ReportProgram.Hr, ReportPrinter.ProgramOf(typeof(PayReportView)));
        Assert.Null(ReportPrinter.ProgramOf(typeof(string)));
    }

    [Fact] // on a machine with Angsana New the slip really is printed in it
    public async Task A_pay_slip_pdf_carries_the_HR_font()
    {
        var dates = new PayPeriodDates(new(2026, 10, 12), new(2026, 10, 26), new(2026, 11, 1));
        var period = new PayPeriodDto(1, dates.From, dates.To, dates.PayDate, PayPeriodStep.PayCalculated, null, null, [], DateTimeOffset.Now, "hr1");
        var slip = new PaySlip("A005", "นางสาวทดสอบ ระบบ", 15, 2, 1, 525, 3, 1.8, 0, 0, 0, 0, 0, 3019, OvertimeParts.None, 0, null);
        var batch = new PaySlipBatch(period, [slip], []);

        var pdf = Encoding.Latin1.GetString(await chromium.Renderer.RenderAsync(
            FormPageHtml.Compose(PaySlipForm.Title, PaySlipForm.Layout(batch), TestFonts.Hr), Paper.A4Portrait));

        Assert.Contains(TestFonts.HrPdfName, pdf);
        if (TestFonts.Hr.Key == ReportFonts.AngsanaNewKey) Assert.DoesNotContain("THSarabunNew", pdf);
    }
}

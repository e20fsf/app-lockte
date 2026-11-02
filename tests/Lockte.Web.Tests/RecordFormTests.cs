using System.Text;
using System.Text.RegularExpressions;
using Lockte.Calib;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Calib;

namespace Lockte.Web.Tests;

/// <summary>
/// RPT-03 แบบฟอร์มบันทึกผลการสอบเทียบ (#205, ระดับ A): the head typed from the register, a page to an instrument, the results table empty. The position of every element is checked by
/// overlaying the PDF on the sample (set env <c>LOCKTE_PDF_OUT</c> to a folder to keep the PDFs the tests print).
/// </summary>
[Collection(ChromiumCollection.Name)]
public sealed class RecordFormTests(ChromiumFixture chromium)
{
    private static InstrumentDto Instrument(InstrumentKind kind, string code, string name = "Height Gauge", string? range = "0-300", string? unit = "mm", string? limit = "0.1",
        string? standard = "GB-QA001/2551-GB-QA005/2551", string? calibrator = "ช่าง ทดสอบ", string? provider = null, string? department = null)
        => new(1, kind, code, name, null, "Mitutoyo", 1, department, department, [], [], range, "0.01", limit, unit, standard, 7, calibrator, null, provider, null, null, null, [1]);

    private static int Pages(byte[] pdf) => Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type /Page\b").Count;

    private static void Keep(string name, byte[] pdf)
    {
        if (Environment.GetEnvironmentVariable("LOCKTE_PDF_OUT") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".pdf"), pdf);
        }
    }

    [Fact] // BR-031 / print-samples: head of a caliper sheet — `0-300 mm`, `± 0.1 mm`, the standard as typed, the employee in "ผู้สอบเทียบ", the other box empty
    public void A_caliper_sheet_prints_its_head_from_the_register()
    {
        var svg = System.Net.WebUtility.HtmlDecode(RecordForm.Page(RecordForm.HeadOf(Instrument(InstrumentKind.Caliper, "HG-DM002/2547")), "PC-QA009-4/2547 ฉบับที่ 2").ToSvg());
        foreach (var text in new[] { ">บริษัท ล็อคเต้ จำกัด<", ">บันทึกผลการสอบเทียบ<", ">PC-QA009-4/2547 ฉบับที่ 2<", ">Height Gauge<", ">HG-DM002/2547<", ">0-300 mm<", ">± 0.1 mm<",
                     ">GB-QA001/2551-GB-QA005/2551<", ">ช่าง ทดสอบ<", ">ผู้สอบเทียบ<", ">หน่วยงานสอบเทียบ<", ">คู่มือวิธีการที่ใช้เลขที่<", ">สภาวะการสอบเทียบ<", ">สรุปผลการสอบเทียบ<", ">(ผู้ประเมินผลการสอบเทียบ)<" })
            Assert.Contains(text, svg);
        Assert.Equal(2, Regex.Matches(svg, ">(?:ผ่าน|ไม่ผ่าน)<").Count);
    }

    [Fact] // print-samples: the external sheet has the company in "หน่วยงานสอบเทียบ" and nothing in "ผู้สอบเทียบ"; no standard instrument
    public void An_external_sheet_puts_the_company_in_the_organisation_box()
    {
        var head = RecordForm.HeadOf(Instrument(InstrumentKind.External, "GB-QA001/2551", "GAUGE BLOCK (5 mm)", calibrator: "ไม่ควรพิมพ์", provider: "Quality Calibration Co.,Ltd."));
        Assert.Equal(("Quality Calibration Co.,Ltd.", null, null, true), (head.Provider, head.Calibrator, head.StandardUsed, head.External));
        var svg = RecordForm.Page(head, null).ToSvg();
        Assert.Contains(">Quality Calibration Co.,Ltd.<", svg);
        Assert.DoesNotContain("ไม่ควรพิมพ์", svg, StringComparison.Ordinal);
    }

    [Fact] // เครื่องชั่ง: the department follows the name without a label
    public void A_scale_sheet_has_its_department_after_the_name()
        => Assert.Equal("เครื่องชั่ง 3 กก. BL", RecordForm.HeadOf(Instrument(InstrumentKind.Scale, "WG-BL001/2568", "เครื่องชั่ง 3 กก.", department: "BL")).Name);

    [Fact] // the mark of the kind is printed already: one cross for ภายใน, one for ภายนอก
    public void Exactly_one_kind_box_is_crossed()
    {
        var internalSvg = RecordForm.Page(RecordForm.HeadOf(Instrument(InstrumentKind.Caliper, "X")), null).ToSvg();
        var externalSvg = RecordForm.Page(RecordForm.HeadOf(Instrument(InstrumentKind.External, "X")), null).ToSvg();
        int Crosses(string svg) => Regex.Matches(svg, "stroke-width=\"0.5\"").Count;
        Assert.Equal((2, 2), (Crosses(internalSvg), Crosses(externalSvg)));
        Assert.NotEqual(internalSvg, externalSvg);
    }

    [Fact] // the results table: 20 numbered rows
    public void The_table_has_twenty_numbered_rows()
    {
        var svg = RecordForm.Page(RecordForm.HeadOf(Instrument(InstrumentKind.Caliper, "X")), null).ToSvg();
        for (var n = 1; n <= 20; n++) Assert.Contains($">{n}<", svg);
        Assert.DoesNotContain(">21<", svg, StringComparison.Ordinal);
    }

    [Fact] // AC-13: 13 instruments → 13 pages
    public async Task A_page_is_printed_for_each_instrument()
    {
        var heads = Enumerable.Range(1, 13).Select(n => RecordForm.HeadOf(Instrument(InstrumentKind.Caliper, $"VN-QA{n:000}/2569"))).ToList();
        var pages = RecordForm.Layout(heads, "PC-QA009-4/2547 ฉบับที่ 2");
        Assert.Equal(13, pages.Count);
        var pdf = await chromium.Renderer.RenderAsync(FormPageHtml.Compose(RecordForm.Title, pages), pages[0].Paper);
        Keep("calib-record-13", pdf);
        Assert.Equal(13, Pages(pdf));
    }

    [Fact]
    public async Task A_long_value_is_squeezed_into_its_line_and_the_page_still_prints_once()
    {
        var head = RecordForm.HeadOf(Instrument(InstrumentKind.Caliper, "VN-QA001/2569", standard: new string('9', 200)));
        var pages = RecordForm.Layout([head], null);
        var pdf = await chromium.Renderer.RenderAsync(FormPageHtml.Compose(RecordForm.Title, pages), pages[0].Paper);
        Keep("calib-record-long", pdf);
        Assert.Equal(1, Pages(pdf));
    }
}

/// <summary>The fixed RPT-03 sheets of เพรสเซอร์เกจ and ตัวควบคุมอุณหภูมิ (#206): one page for "ทุกเครื่อง", the values from the settings.</summary>
public sealed class FixedRecordFormTests
{
    private static CalibSettingsDto Defaults => new(4m, 2.1m, 7, 8, "QA", "QMR", "", "", CalibSettingsDefaults.PressureGaugeName, CalibSettingsDefaults.PressureGaugeRange,
        CalibSettingsDefaults.PressureGaugeLimit, CalibSettingsDefaults.TempControlName, CalibSettingsDefaults.TempControlRange, CalibSettingsDefaults.TempControlLimit, null);

    [Fact] // reports.md RPT-03 / Q-13: the constants of the legacy sheets
    public void The_pressure_gauge_sheet_prints_the_constants_of_the_legacy_form()
    {
        var head = RecordReports.FixedHead(InstrumentKind.PressureGauge, Defaults);
        Assert.Equal(("Pressure Gauge", "ทุกเครื่อง", "50 - 150 Kg/cm²", "±10 kg/cm²"), (head.Name, head.Code, head.Range, head.Limit));
        Assert.Equal((null, null, null, false), (head.Calibrator, head.Provider, head.StandardUsed, head.External));
        var svg = System.Net.WebUtility.HtmlDecode(RecordForm.Page(head, null).ToSvg());
        foreach (var text in new[] { ">Pressure Gauge<", ">ทุกเครื่อง<", ">50 - 150 Kg/cm²<", ">±10 kg/cm²<" }) Assert.Contains(text, svg);
    }

    [Fact]
    public void The_temp_control_sheet_prints_its_own_constants()
    {
        var head = RecordReports.FixedHead(InstrumentKind.TempControl, Defaults);
        Assert.Equal(("Temp Control", "ทุกเครื่อง", "190 - 210 °C", "±10 °C"), (head.Name, head.Code, head.Range, head.Limit));
    }

    [Fact] // the values are master data: another value in the settings prints
    public void The_sheet_follows_the_settings()
        => Assert.Equal("0 - 700 Kg/cm²", RecordReports.FixedHead(InstrumentKind.PressureGauge, Defaults with { PressureGaugeRange = "0 - 700 Kg/cm²" }).Range);

    [Fact]
    public void Only_the_two_fixed_kinds_are_fixed()
    {
        Assert.Equal([InstrumentKind.PressureGauge, InstrumentKind.TempControl], Enum.GetValues<InstrumentKind>().Where(RecordReports.IsFixed));
        Assert.All(Enum.GetValues<InstrumentKind>().Where(RecordReports.IsFixed), k => Assert.False(RecordReports.IsPerInstrument(k)));
        Assert.Equal(5, Enum.GetValues<InstrumentKind>().Count(k => RecordReports.IsFixed(k) || RecordReports.IsPerInstrument(k))); // JIG has its own forms (#207 #208)
    }
}

using Lockte.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lockte.Web.Reports;

/// <summary>
/// Prints one report (#52, the pipeline every report uses): the report's Razor component (it only lays out the rows a
/// module's query returned, #41) → HTML by <see cref="HtmlRenderer"/> → แบบกลาง B by <see cref="ReportHtml"/> → PDF
/// by <see cref="ReportPdfRenderer"/>.
/// </summary>
public sealed class ReportPrinter(ReportPdfRenderer pdf, DocumentNumberService documentNumbers, TimeProvider clock)
{
    /// <param name="title">The title as the legacy report printed it.</param>
    /// <param name="documentKey">A <see cref="DocumentKeys"/> value when the document has a เลขเอกสารควบคุม.</param>
    /// <param name="printedBy">The signed-in user's display name, printed in the footer.</param>
    public async Task<byte[]> PrintAsync<TReport>(string title, Paper paper, Dictionary<string, object?> parameters, string printedBy,
        string? documentKey = null, CancellationToken cancellationToken = default) where TReport : IComponent
    {
        var number = documentKey is null ? null : await documentNumbers.PrintedTextAsync(documentKey);
        var body = await ReportBody.RenderAsync<TReport>(parameters);
        var html = ReportHtml.Compose(new ReportDocument(title, paper, number), new PrintStamp(clock.GetLocalNow(), printedBy), body);
        return await pdf.RenderAsync(html, paper, cancellationToken);
    }

    /// <summary>
    /// Prints a ระดับ A form (#40 ตรงแบบเดิม): its own pages as the legacy printed them, without the แบบกลาง header,
    /// footer or print stamp.
    /// </summary>
    public Task<byte[]> PrintFormAsync(string title, IReadOnlyList<FormPage> pages, CancellationToken cancellationToken = default)
        => pdf.RenderAsync(FormPageHtml.Compose(title, pages), pages[0].Paper, cancellationToken);
}

/// <summary>A report component to HTML, outside any circuit or request. Report components take parameters only, no services.</summary>
public static class ReportBody
{
    public static async Task<string> RenderAsync<TReport>(Dictionary<string, object?> parameters) where TReport : IComponent
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<TReport>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }
}

/// <summary>Text the report bodies share, as the legacy printed it (print-forms.md).</summary>
public static class ReportText
{
    /// <summary>วันที่ในตาราง: d/M/yyyy พ.ศ.</summary>
    public static string Date(DateOnly date) => ThaiDate.Format(date);

    /// <summary>
    /// Two decimals with thousands separators, as Access's Format: the Double read to 15 digits (decimal), then half away
    /// from zero (13.9375 → 13.94, 2.675 → 2.68).
    /// </summary>
    public static string Decimal2(double value) => Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero).ToString("#,##0.00", ThaiDate.Culture);

    /// <summary>เงินบาทเต็มจำนวนมีคั่นหลักพัน (Standard, 0 ตำแหน่ง), half away from zero as Access's Format.</summary>
    public static string Baht(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("#,##0", ThaiDate.Culture);

    /// <summary>ทศนิยม 2 ตำแหน่งของ <see cref="decimal"/> (เช่น %ค่าล่วงเวลา), half away from zero.</summary>
    public static string Decimal2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("#,##0.00", ThaiDate.Culture);

    /// <summary>ชื่อเดือนในหัว from the dates themselves (D-5): "ตุลาคม 2569", "ตุลาคม–พฤศจิกายน 2569", "ธันวาคม 2569–มกราคม 2570".</summary>
    public static string MonthTitle(DateOnly from, DateOnly to)
    {
        string Month(DateOnly d) => d.ToString("MMMM", ThaiDate.Culture);
        string Year(DateOnly d) => d.ToString("yyyy", ThaiDate.Culture);
        if (from.Year == to.Year && from.Month == to.Month) return $"{Month(from)} {Year(from)}";
        return from.Year == to.Year ? $"{Month(from)}–{Month(to)} {Year(to)}" : $"{Month(from)} {Year(from)}–{Month(to)} {Year(to)}";
    }

    /// <summary>One decimal with thousands separators (Standard, DecimalPlaces 1), half away from zero: 2.875 → 2.9.</summary>
    public static string Decimal1(double value) => Math.Round((decimal)value, 1, MidpointRounding.AwayFromZero).ToString("#,##0.0", ThaiDate.Culture);

    /// <summary>A percentage of Decimal2, blank when there is none (no จำนวนวันทำงาน typed).</summary>
    public static string Percent(double? value) => value is { } v ? Decimal2(v) : "";

    /// <summary>เวลา as Short Time printed it: <c>8:00</c>, <c>17:30</c>; blank when there is none.</summary>
    public static string Time(TimeOnly? time) => time?.ToString("H:mm", System.Globalization.CultureInfo.InvariantCulture) ?? "";
}

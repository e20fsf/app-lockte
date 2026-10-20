using System.Globalization;
using Lockte.Hr;
using Lockte.Web.Reports.Forms;
using Microsoft.AspNetCore.Components;

namespace Lockte.Web.Reports.Hr;

/// <summary>
/// เอกสารพิมพ์รายบุคคล ระดับ A (#65): RPT-50, 53, 54, 55 open as PDF in a new tab like the other reports. Each GET
/// takes what the legacy form passed to its report (รหัสพนักงาน, and the typed date/reason/scale where there is one).
/// </summary>
public static class HrDocumentEndpoints
{
    public static IEndpointRouteBuilder MapHrDocuments(this IEndpointRouteBuilder app)
    {
        var hr = app.MapGroup("/reports/hr").RequireAuthorization();

        // RPT-50 ใบลางาน: A5 นอน.
        hr.MapGet("/leave-form/{employeeId:int}", async (int employeeId, HrDocumentDirectory documents, FormPrinter printer, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            if (await documents.GetLeaveFormAsync(employeeId) is not { } form) return Results.NotFound("ไม่พบพนักงานคนนี้");
            var pdf = await printer.PrintAsync<LeaveFormView>(LeaveFormView.Title, Paper.A5Landscape, DocumentKeys.LeaveForm,
                new() { [nameof(LeaveFormView.Form)] = form, [nameof(LeaveFormView.PrintedAt)] = clock.GetLocalNow().DateTime }, cancellationToken);
            return Pdf(pdf, $"ใบลางาน {form.Code}");
        });

        // RPT-53 หนังสือรับรองการทำงาน (kind=work) / เงินเดือน (kind=salary): A4 ตั้ง.
        hr.MapGet("/certificate/{employeeId:int}", async (int employeeId, string? kind, HrDocumentDirectory documents, FormPrinter printer,
            CancellationToken cancellationToken) =>
        {
            var salary = kind == "salary";
            if (await documents.GetCertificateAsync(employeeId) is not { } person) return Results.NotFound("ไม่พบพนักงานคนนี้");
            var pdf = await printer.PrintAsync<CertificateView>(CertificateView.TitleOf(salary), Paper.A4Portrait,
                salary ? DocumentKeys.SalaryCertificate : DocumentKeys.WorkCertificate,
                new() { [nameof(CertificateView.Person)] = person, [nameof(CertificateView.Salary)] = salary }, cancellationToken);
            return Pdf(pdf, $"{CertificateView.TitleOf(salary)} {person.Name}");
        });

        // RPT-54 ฟอร์มเกษียณ (kind=retire) / ฟอร์มปลดออก (kind=dismiss): A4 ตั้ง, the scale as typed on the screen
        // (day = 6 values, line = 7 lines; missing → the legacy table).
        hr.MapGet("/severance/{employeeId:int}", async (int employeeId, string? kind, string? reason, int[]? day, string[]? line,
            HrDocumentDirectory documents, FormPrinter printer, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var dismissal = kind == "dismiss";
            var legacy = SeveranceScale.Legacy;
            var scale = new SeveranceScale(day is { Length: 6 } ? day : legacy.Days, line is { Length: 7 } ? line : legacy.Lines);
            if (await documents.GetSeveranceAsync(employeeId, scale, reason) is not { } data) return Results.NotFound("ไม่พบพนักงานคนนี้");
            var pdf = await printer.PrintAsync<SeveranceView>(SeveranceView.TitleOf(dismissal), Paper.A4Portrait, null,
                new()
                {
                    [nameof(SeveranceView.Data)] = data, [nameof(SeveranceView.Dismissal)] = dismissal,
                    [nameof(SeveranceView.Today)] = DateOnly.FromDateTime(clock.GetLocalNow().DateTime),
                }, cancellationToken);
            return Pdf(pdf, $"{(dismissal ? "ฟอร์มปลดออก" : "ฟอร์มเกษียณ")} {data.Person.Name}");
        });

        // RPT-55 ใบลาออก: A4 ตั้ง (Letter เดิม), date = yyyy-MM-dd as typed on SCR-33.
        hr.MapGet("/resignation-letter/{employeeId:int}", async (int employeeId, string? date, string? reason, HrDocumentDirectory documents,
            FormPrinter printer, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            DateOnly? resign = DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
            if (await documents.GetResignationLetterAsync(employeeId, resign, reason) is not { } letter) return Results.NotFound("ไม่พบพนักงานคนนี้");
            var pdf = await printer.PrintAsync<ResignationLetterView>(ResignationLetterView.Title, Paper.A4Portrait, DocumentKeys.ResignationLetter,
                new()
                {
                    [nameof(ResignationLetterView.Letter)] = letter,
                    [nameof(ResignationLetterView.Today)] = DateOnly.FromDateTime(clock.GetLocalNow().DateTime),
                }, cancellationToken);
            return Pdf(pdf, $"ใบลาออก {letter.Code}");
        });

        // RPT-55 รายงานพนักงานที่ลาออก: A4 ตั้ง (Letter เดิม).
        hr.MapGet("/resigned", async (HrDocumentDirectory documents, FormPrinter printer, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var rows = await documents.ListResignedAsync();
            var pdf = await printer.PrintAsync<ResignedListView>(ResignedListView.Title, Paper.A4Portrait, null,
                new() { [nameof(ResignedListView.Rows)] = rows, [nameof(ResignedListView.Today)] = DateOnly.FromDateTime(clock.GetLocalNow().DateTime) },
                cancellationToken);
            return Pdf(pdf, ResignedListView.Title);
        });

        return app;
    }

    private static IResult Pdf(byte[] pdf, string name) => new InlinePdf(Results.File(pdf, "application/pdf"), name);

    /// <summary>Shown in the browser's PDF viewer (inline) under a readable file name, never cached.</summary>
    private sealed class InlinePdf(IResult file, string name) : IResult
    {
        public Task ExecuteAsync(HttpContext http)
        {
            http.Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(name + ".pdf")}";
            http.Response.Headers.CacheControl = "no-store";
            return file.ExecuteAsync(http);
        }
    }
}

/// <summary>
/// Prints one ระดับ A form: its Razor component → HTML → <see cref="FormHtml"/> → PDF, with the document's
/// เลขเอกสารควบคุม (passed to the component as <c>DocumentNumber</c>) when it has one.
/// </summary>
public sealed class FormPrinter(ReportPdfRenderer pdf, DocumentNumberService documentNumbers)
{
    public async Task<byte[]> PrintAsync<TForm>(string title, Paper paper, string? documentKey, Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default) where TForm : IComponent
    {
        if (documentKey is not null) parameters["DocumentNumber"] = await documentNumbers.PrintedTextAsync(documentKey);
        var body = await ReportBody.RenderAsync<TForm>(parameters);
        return await pdf.RenderAsync(FormHtml.Compose(title, paper, body), paper, cancellationToken);
    }
}

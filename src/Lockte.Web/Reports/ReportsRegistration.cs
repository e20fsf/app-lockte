using Lockte.Hr;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Lockte.Web.Reports.Hr;
using Lockte.Web.Reports.Order;
using Lockte.Web.Reports.Order.Qa;
using Lockte.Web.Reports.Purch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Lockte.Web.Reports;

public static class ReportsRegistration
{
    /// <summary>เลขเอกสารควบคุม (schema <c>report</c>).</summary>
    public static IServiceCollection AddReportData(this IServiceCollection services, string connectionString)
    {
        services.AddModuleDbContext<ReportsDbContext>(connectionString, ReportsDbContext.Schema);
        services.AddScoped<DocumentNumberService>();
        return services;
    }

    /// <summary>The PDF pipeline: one Chromium with a warm page pool, opened in the background at startup.</summary>
    public static IServiceCollection AddReportPrinting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ReportOptions>(configuration.GetSection(ReportOptions.Section));
        services.Configure<EmployerOptions>(configuration.GetSection(EmployerOptions.Section));
        services.Configure<PurchLetterheadOptions>(configuration.GetSection(PurchLetterheadOptions.Section));
        services.AddSingleton<ReportPdfRenderer>();
        services.AddHostedService<ReportWarmUp>();
        services.AddScoped<ReportPrinter>();
        services.AddMemoryCache();
        services.AddSingleton<OvertimeSheetPrints>();
        return services;
    }

    /// <summary>
    /// Reports open as PDF in a new browser tab (the user prints from there, as Access opened a preview). One GET per
    /// report; every report is behind the default policy (staff).
    /// </summary>
    public static IEndpointRouteBuilder MapReports(this IEndpointRouteBuilder app)
    {
        var reports = app.MapGroup("/reports").RequireAuthorization().RequirePayWhereListed(); // #277: ข้อมูลค่าจ้าง → 403
        reports.MapHrAttendanceReports(); // ใบเตือน การลา การมาทำงาน (#59)
        reports.MapHrPayHistoryReports(); // ค่าแรงสิ้นเดือน, สรุปหลายงวด (#56)
        reports.MapHrVacationPayReports(); // ค่าพักร้อน (#63)
        reports.MapHrBonusReports(); // โบนัส และจ่ายรวมค่าแรงงวด (#62)
        reports.MapHrEvaluationReports(); // ปรับค่าแรง ประเมินผล (#67)
        reports.MapOrderMasterListReports(); // รายชื่อข้อมูลหลักของผลผลิตและORDER (#137)
        reports.MapSaleOrderReports(); // ใบ SALE ORDER (#110)
        reports.MapQaSheetReports(); // ใบตรวจคุณภาพ QA (#130)
        reports.MapYieldReports(); // ผลผลิตประจำเดือน และผลผลิตรวม (#138)
        reports.MapLotTraceReports(); // เปรียบเทียบ Lot การผลิต (#127)
        reports.MapStockCutReports(); // รายงานตัดสต็อก (#123)
        reports.MapProductionSlipReports(); // ใบรายงานผลผลิต (#139)
        reports.MapExportSummaries(); // สรุปการส่งออก (#114)
        reports.MapMoldUsageReports(); // การใช้งานแม่พิมพ์ (#125)
        reports.MapStockReports(); // สต็อกคงเหลือ และ STOCK CARD (#124)
        reports.MapWithdrawalReports(); // รายงานใบเบิก (#120)
        reports.MapDeliveryReports(); // เอกสาร ISO การส่งของ (#136)
        reports.MapOrderReports(); // ORDER ค้างส่ง และสรุปการรับ ORDER (#113)
        reports.MapPurchRequestReports(); // ใบขออนุมัติสั่งซื้อ (วัตถุดิบ #220)
        reports.MapPurchaseOrderReports(); // ใบสั่งซื้อ (วัตถุดิบ #224)
        reports.MapSupplierEvaluationReports(); // ประเมินผู้ขาย (วัตถุดิบ #254)
        reports.MapSupplierEvaluationSummaryReports(); // แบบประเมินประจำปี และสรุปรายไตรมาส (วัตถุดิบ #255)
        reports.MapPurchBacklogReports(); // ยอดค้างสั่ง/ค้างรับ (วัตถุดิบ #256)
        reports.MapIncomingReports(); // บันทึกการรับของ และ INCOMING INSPECTION (วัตถุดิบ #257)
        reports.MapReceiptReports(); // รายงานการรับวัตถุดิบตามแผนก/ชื่อ/ผู้ขาย (วัตถุดิบ #258)

        // RPT-01 เวลาทำงานรายคน (รายงาน คำนวณเวลาทำงาน13): A4 ตั้ง, no document number.
        reports.MapGet("/hr/pay-periods/{id:int}/time", async (int id, HttpContext http, PayPeriodDirectory periods, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (await periods.GetTimeReportAsync(id) is not { } report) return Results.NotFound("ยังไม่ได้คำนวณเวลาทำงานของงวดนี้");
            var pdf = await printer.PrintAsync<TimeReportView>(TimeReportView.Title, Paper.A4Portrait,
                new() { [nameof(TimeReportView.Report)] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return Pdf(http, pdf, "เวลาทำงาน " + report.Period.From.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        });

        // RPT-02 ค่าแรงรายคน (รายงาน ค่าแรง1, SCR-41): A4 ตั้ง, one department or all (no ?department).
        reports.MapGet("/hr/pay-periods/{id:int}/pay", async (int id, int? department, HttpContext http, PayPeriodDirectory periods,
            ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (await periods.GetPayReportAsync(id, department) is not { } report) return Results.NotFound("ยังไม่ได้คำนวณค่าแรงของงวดนี้");
            var pdf = await printer.PrintAsync<PayReportView>(PayReportView.Title, Paper.A4Portrait,
                new() { [nameof(PayReportView.Report)] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return Pdf(http, pdf, "ค่าแรง " + Iso(report.Period.From) + (report.DepartmentName is { } name ? " " + name : ""));
        });

        // RPT-03 สรุปค่าแรงรายแผนก (รายงาน สรุปค่าแรง): A4 ตั้ง. Printing it is step พิมพ์สรุปค่าแรง of เมนู 6.
        reports.MapGet("/hr/pay-periods/{id:int}/pay-summary", async (int id, HttpContext http, PayPeriodDirectory periods,
            PayPeriodRegistry registry, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (await periods.GetPaySummaryAsync(id) is not { } report) return Results.NotFound("ยังไม่ได้คำนวณค่าแรงของงวดนี้");
            var pdf = await printer.PrintAsync<PaySummaryView>(PaySummaryView.Title, Paper.A4Portrait,
                new() { [nameof(PaySummaryView.Report)] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            await registry.CompleteStepAsync(id, PayPeriodStep.SummaryPrinted);
            return Pdf(http, pdf, "สรุปค่าแรง " + Iso(report.Period.From));
        });

        // SCR-42 ค่าแรงส่งแบงค์ (RPT-05, INT-01/02, #54): the ttb list (PAY_W) or the กสิกร list (PAY_OT) of a งวด, as the PDF or ?format=xlsx for the template
        // of the bank; either one marks the ส่งแบงค์ step done (a step still locked by the one before it stays as it is).
        reports.MapGet("/hr/pay-periods/{id:int}/bank/{bank}", async (int id, string bank, string? format, HttpContext http, PayPeriodDirectory periods,
            PayPeriodRegistry registry, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (await periods.GetAsync(id) is not { PayCalculatedAt: not null, Migrated: false })
                return Results.NotFound("ยังไม่ได้คำนวณค่าแรงของงวดนี้ในระบบใหม่");
            if (BankFiles.Of(bank, BankTransfers.Build(await periods.GetBankInputsAsync(id))) is not { } table) return Results.NotFound("ไม่มีรายการนี้");
            var result = await HrBonusReports.BankFileAsync(http, printer, table, format, cancellationToken);
            await registry.CompleteStepAsync(id, PayPeriodStep.BankPrinted);
            return result;
        });

        // RPT-11 สรุปค่าแรงทั้ง 2 ช่วง (มาโคร สรุปค่าแรงช่วง1+2, BR-008, #58): the two ช่วง of the งวดคร่อมปีใหม่ by id; each ช่วง alone is RPT-03.
        reports.MapGet("/hr/two-part-periods/{first:int}/{second:int}/pay-summary", async (int first, int second, HttpContext http,
            PayPeriodDirectory periods, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (await periods.GetTwoPartSummaryAsync(first, second) is not { } report)
                return Results.NotFound("สองช่วงนี้ยังไม่ครบ: ต้องกรอกจำนวนวันที่จ่ายทั้งสองช่วง ช่วงที่ 2 เริ่มวันถัดจากช่วงที่ 1 และคำนวณค่าแรงทั้งสองช่วงแล้ว");
            var pdf = await printer.PrintAsync<PaySummaryView>(PaySummaryView.Title + " ทั้ง 2 ช่วง", Paper.A4Portrait,
                new() { [nameof(PaySummaryView.Report)] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return Pdf(http, pdf, "สรุปค่าแรง 2 ช่วง " + Iso(report.Period.From));
        });

        // SCR-47 ค่าแรงส่งแบงค์ทั้ง 2 ช่วง (BR-008 ข้อ 4): the transfer lists with the pay of both ช่วง added; printing it marks the ส่งแบงค์ step of both
        // (a step still locked by the one before it stays as it is). The bank file itself comes with #54.
        reports.MapGet("/hr/two-part-periods/{first:int}/{second:int}/bank", async (int first, int second, string? list, string? format, HttpContext http,
            PayPeriodDirectory periods, PayPeriodRegistry registry, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (await periods.GetTwoPartBankAsync(first, second) is not { } bank)
                return Results.NotFound("สองช่วงนี้ยังไม่ครบ: ต้องกรอกจำนวนวันที่จ่ายทั้งสองช่วง ช่วงที่ 2 เริ่มวันถัดจากช่วงที่ 1 และคำนวณค่าแรงทั้งสองช่วงแล้ว");
            // ?list=ttb|kbank: that list alone, as the PDF of RPT-05 or ?format=xlsx for the template of the bank (#54).
            if (list is not null)
            {
                if (BankFiles.Of(list, bank.Rows) is not { } table) return Results.NotFound("ไม่มีรายการนี้");
                var file = await HrBonusReports.BankFileAsync(http, printer, table, format, cancellationToken);
                await registry.CompleteStepAsync(first, PayPeriodStep.BankPrinted);
                await registry.CompleteStepAsync(second, PayPeriodStep.BankPrinted);
                return file;
            }
            var pdf = await printer.PrintAsync<TwoPartBankView>(TwoPartBankView.Title, Paper.A4Portrait,
                new() { [nameof(TwoPartBankView.Bank)] = bank }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            await registry.CompleteStepAsync(first, PayPeriodStep.BankPrinted);
            await registry.CompleteStepAsync(second, PayPeriodStep.BankPrinted);
            return Pdf(http, pdf, "ส่งแบงค์ 2 ช่วง " + Iso(bank.Period.From));
        });

        // RPT-58 สรุปค่าแรงงานรับจ้างตามลูกค้า (SCR-44 "พิมพ์", รายงาน สรุปค่าแรง1): A4 ตั้ง, ?from=&to= ISO and ?customer= รหัสลูกค้า.
        // Calculated for the print only: no งวดค่าแรง step, nothing stored (ContractLaborDirectory).
        reports.MapGet("/hr/pay/contract-labor", async (DateOnly? from, DateOnly? to, string? customer, HttpContext http,
            ContractLaborDirectory contractLabor, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (from is null || to is null || from > to || string.IsNullOrWhiteSpace(customer))
                return Results.BadRequest("กรุณากรอกตั้งแต่วันที่ ถึงวันที่ (ไม่ย้อนกัน) และเลือกลูกค้า");
            if (await contractLabor.GetReportAsync(from.Value, to.Value, customer) is not { } report) return Results.NotFound("ไม่พบลูกค้ารายนี้");
            var title = ContractLaborView.TitleOf(report);
            var pdf = await printer.PrintAsync<ContractLaborView>(title, Paper.A4Portrait,
                new() { [nameof(ContractLaborView.Report)] = report }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return Pdf(http, pdf, "ค่าแรงงานรับจ้าง " + report.CustomerCode + " " + Iso(report.From));
        });

        // RPT-04 สลิปเงินเดือน (รายงาน พิมพ์SLIP, BR-034): ระดับ A, A4 ตั้ง 3 ใบต่อหน้า. Printing it is step พิมพ์ SLIP of
        // เมนู 6 once the step before is done (#22); the slips print as soon as ค่าแรง is calculated.
        reports.MapGet("/hr/pay-periods/{id:int}/slips", async (int id, HttpContext http, PayPeriodDirectory periods,
            PayPeriodRegistry registry, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (await periods.GetSlipsAsync(id) is not { } batch)
                return Results.NotFound("ยังไม่ได้คำนวณค่าแรงของงวดนี้ในระบบใหม่ (งวดที่ย้ายมาจากระบบเดิมพิมพ์สลิปไม่ได้)");
            if (batch.Slips.Count == 0) return Results.NotFound("งวดนี้ไม่มีพนักงานที่คำนวณค่าแรง");
            var pdf = await printer.PrintFormAsync(PaySlipForm.Title, PaySlipForm.Layout(batch), cancellationToken);
            await registry.CompleteStepAsync(id, PayPeriodStep.SlipPrinted);
            return Pdf(http, pdf, "สลิป " + Iso(batch.Period.From));
        });

        // RPT-16 ใบขออนุมัติทำงานล่วงเวลา (รายงาน OT): A5 ตั้ง, no document number. The sheet SCR-14 just saved (OvertimeSheetPrints).
        reports.MapGet("/hr/overtime-sheets/{key:guid}", async (Guid key, HttpContext http, OvertimeSheetPrints sheets, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (sheets.Find(key, http.User.Identity?.Name) is not { } sheet) return Results.NotFound("ไม่พบใบขออนุมัติทำ OT นี้ กรุณาพิมพ์ใหม่จากหน้าจอ");
            var pdf = await printer.PrintAsync<OvertimeSheetView>(OvertimeSheetView.Title, OvertimeSheetView.Paper,
                new() { [nameof(OvertimeSheetView.Sheet)] = sheet }, AccountClaims.DisplayNameOf(http.User), cancellationToken: cancellationToken);
            return Pdf(http, pdf, "ใบขออนุมัติทำ OT " + sheet.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        });

        reports.MapOnboardingReports(); // #66: RPT-20, RPT-21, RPT-22, RPT-56

        // RPT-51 ทะเบียนผู้ประกันตน สปส.6-07 (เมนู 18): ระดับ A, A4 นอน, every employee row.
        reports.MapGet("/hr/insured-persons", async (HttpContext http, GovernmentFormDirectory forms, IOptionsSnapshot<EmployerOptions> employer,
            ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var pages = InsuredPersonsForm.Layout(await forms.ListInsuredPersonsAsync(), employer.Value);
            return Pdf(http, await printer.PrintFormAsync(InsuredPersonsForm.Title, pages, cancellationToken), InsuredPersonsForm.Title);
        });

        // RPT-57 แบบ คร.2 (ปุ่มในข้อมูลพนักงาน): ระดับ A, A4 ตั้ง. A POST from the form on SCR-02/03, so the names of
        // the parents HR types stay out of the URL (and the server's logs); the form carries the antiforgery token.
        reports.MapPost("/hr/employees/{id:int}/kor2", async (int id, [FromForm] string? name, [FromForm] string? job,
            [FromForm] string? father, [FromForm] string? mother, HttpContext http, GovernmentFormDirectory forms,
            IOptionsSnapshot<EmployerOptions> employer, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (await forms.GetKor2Async(id) is not { } employee) return Results.NotFound("ไม่พบพนักงาน");
            var page = Kor2Form.Layout(employee, new Kor2Input(name, job, father, mother), employer.Value);
            return Pdf(http, await printer.PrintFormAsync(Kor2Form.Title, [page], cancellationToken), $"แบบ คร.2 {employee.Code}");
        });

        return app;
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Shown in the browser's PDF viewer (inline) under a readable file name.</summary>
    internal static IResult Pdf(HttpContext http, byte[] pdf, string name)
    {
        http.Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(name + ".pdf")}";
        http.Response.Headers.CacheControl = "no-store";
        return Results.File(pdf, "application/pdf");
    }
}

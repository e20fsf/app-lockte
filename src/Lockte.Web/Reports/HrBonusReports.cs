using Lockte.Hr;
using Lockte.Web.Accounts;
using Lockte.Web.Reports.Hr;
using static Lockte.Web.Reports.HrAttendanceReports;

namespace Lockte.Web.Reports;

/// <summary>
/// RPT-35 คำนวณโบนัส (SCR-50 "พิมพ์") and BR-035 จ่ายรวมกับค่าแรงงวด (RPT-40 ทหารไทย, RPT-41 กสิกร, RPT-42 สลิปรวมโบนัส, #62). The bank
/// lists are check lists on A4 until the bank file (#54); the slips are the form of RPT-04 with the bonus filled.
/// </summary>
public static class HrBonusReports
{
    public const string NoRun = "ยังไม่ได้คำนวณโบนัส — กดพิมพ์ในหน้าคำนวณโบนัสก่อน";

    public static RouteGroupBuilder MapHrBonusReports(this RouteGroupBuilder reports)
    {
        // SCR-50: calculates (replacing the last run) and prints RPT-35.
        reports.MapGet("/hr/bonus", async (DateOnly? from, DateOnly? to, int? department, decimal? monthly, decimal? daily, HttpContext http,
            BonusRegistry registry, BonusDirectory bonus, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            if (Range(from, to) is { } bad) return bad;
            var result = await registry.CalculateAsync(new BonusInput(from!.Value, to!.Value, department, monthly ?? BonusInput.DefaultMonthly,
                daily ?? BonusInput.DefaultDaily));
            if (!result.Succeeded) return Results.BadRequest(string.Join(" ", result.Errors));
            var report = (await bonus.GetReportAsync())!;
            return await PrintAsync<BonusReportView>(http, printer, BonusReportView.Title, Paper.A4Landscape, report, "โบนัส " + Iso(report.Input.From), cancellationToken);
        });

        // RPT-35 of the last run, without calculating again.
        reports.MapGet("/hr/bonus/last", async (HttpContext http, BonusDirectory bonus, ReportPrinter printer, CancellationToken cancellationToken) =>
            await bonus.GetReportAsync() is not { } report
                ? Results.NotFound(NoRun)
                : await PrintAsync<BonusReportView>(http, printer, BonusReportView.Title, Paper.A4Landscape, report, "โบนัส " + Iso(report.Input.From), cancellationToken));

        // RPT-40 / RPT-41: the TTB list (ค่าแรงงวด + โบนัส) or the KBANK list (OT งวด + โบนัส) of a งวด.
        reports.MapGet("/hr/pay-periods/{id:int}/bonus-bank/{bank}", async (int id, string bank, HttpContext http, PayPeriodDirectory periods,
            BonusDirectory bonus, ReportPrinter printer, CancellationToken cancellationToken) =>
        {
            var key = bank.ToUpperInvariant();
            if (key is not (BankTransfers.Ttb or BankTransfers.Kbank)) return Results.NotFound("ไม่มีรายการนี้");
            if (await periods.GetAsync(id) is not { } period) return Results.NotFound("ไม่พบงวดค่าแรงนี้");
            if (await bonus.GetPaymentAsync(period) is not { } payment) return Results.NotFound("ยังไม่ได้คำนวณโบนัส หรือยังไม่ได้คำนวณค่าแรงของงวดนี้ในระบบใหม่");
            var rows = payment.Rows.Where(r => r.Bank == key).ToList();
            var list = new TwoPartBank(period, rows, BankTransfers.Summary(rows));
            var title = key == BankTransfers.Ttb ? "ค่าแรงรวมโบนัสส่งแบงค์ (ทหารไทย)" : "เงิน OT รวมโบนัสส่งแบงค์ (กสิกร)";
            var pdf = await printer.PrintAsync<TwoPartBankView>(title, Paper.A4Portrait,
                new() { [nameof(TwoPartBankView.Bank)] = list, [nameof(TwoPartBankView.Titles)] = BonusTitles }, AccountClaims.DisplayNameOf(http.User),
                cancellationToken: cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, title + " " + Iso(period.From));
        });

        // RPT-42: the slips with the bonus (BR-035); does not change the งวด's steps.
        reports.MapGet("/hr/pay-periods/{id:int}/slips-with-bonus", async (int id, HttpContext http, PayPeriodDirectory periods, ReportPrinter printer,
            CancellationToken cancellationToken) =>
        {
            if (await periods.GetSlipsAsync(id, withBonus: true) is not { } batch)
                return Results.NotFound("ยังไม่ได้คำนวณโบนัส หรือยังไม่ได้คำนวณค่าแรงของงวดนี้ในระบบใหม่");
            if (batch.Slips.Count == 0) return Results.NotFound("ไม่มีพนักงานที่ได้โบนัสรวมในสลิปของงวดนี้");
            var pdf = await printer.PrintFormAsync(PaySlipForm.Title, PaySlipForm.Layout(batch), cancellationToken);
            return ReportsRegistration.Pdf(http, pdf, "สลิปรวมโบนัส " + Iso(batch.Period.From));
        });

        return reports;
    }

    private static readonly Dictionary<string, string> BonusTitles = new()
    {
        [BankTransfers.Ttb] = "ทหารไทย (ค่าแรงงวด + โบนัส)",
        [BankTransfers.Kbank] = "กสิกรไทย (OT งวด + โบนัส)",
    };
}

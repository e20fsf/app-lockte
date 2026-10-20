using Lockte.Hr;
using Lockte.Web.Accounts;

namespace Lockte.Web.Reports.Hr;

/// <summary>
/// The reports of #66 under <c>/reports/hr/employees</c>: RPT-20, RPT-21 (ระดับ B, แบบกลาง) and the ระดับ A forms of
/// RPT-56 and RPT-22. Each opens as a PDF in the browser, like every report (<see cref="ReportsRegistration.MapReports"/>).
/// </summary>
public static class OnboardingReports
{
    public const string Base = "reports/hr/employees";

    /// <summary>Query-string date: yyyy-MM-dd.</summary>
    public const string DateFormat = "yyyy-MM-dd";

    public static string HiringFormHref(int employeeId, DateOnly? appliedOn, DateOnly? interviewedOn)
        => $"{Base}/{employeeId}/hiring-form?applied={Iso(appliedOn)}&interviewed={Iso(interviewedOn)}";

    public static string ContractHref(int employeeId) => $"{Base}/{employeeId}/contract";

    public static string ProbationApprovalHref(int employeeId, ProbationApprovalInput input)
        => $"{Base}/{employeeId}/probation-approval?section={Uri.EscapeDataString(input.Section ?? "")}&due={Iso(input.DueOn)}&return={Iso(input.ReturnBy)}";

    public static string RosterHref(int? departmentId, int? shiftId) => $"{Base}/roster?department={departmentId}&shift={shiftId}";

    private static string Iso(DateOnly? date) => date?.ToString(DateFormat, System.Globalization.CultureInfo.InvariantCulture) ?? "";

    private static DateOnly? ParseDate(string? text)
        => DateOnly.TryParseExact(text, DateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)
            ? d : null;

    public static void MapOnboardingReports(this IEndpointRouteBuilder reports)
    {
        var employees = reports.MapGroup("/hr/employees");

        // RPT-20 พนักงานยังไม่ผ่านทดลองงาน (รายงาน พนักงานยังไม่บรรจุ): A4 ตั้ง.
        employees.MapGet("/on-probation", async (HttpContext http, EmployeeDirectory directory, ReportPrinter printer, CancellationToken ct) =>
        {
            var pdf = await printer.PrintAsync<OnProbationReportView>(OnProbationReportView.Title, Paper.A4Portrait,
                new() { [nameof(OnProbationReportView.Employees)] = await directory.ListOnProbationAsync() },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: ct);
            return Pdf(http, pdf, "พนักงานยังไม่ผ่านทดลองงาน");
        });

        // RPT-21 รายชื่อพนักงานแผนก… (SCR-04 "พิมพ์"): A4 ตั้ง; no แผนก / กะ = all.
        employees.MapGet("/roster", async (int? department, int? shift, HttpContext http, OnboardingDirectory directory, ReportPrinter printer,
            CancellationToken ct) =>
        {
            var roster = await directory.GetDepartmentRosterAsync(department, shift);
            var pdf = await printer.PrintAsync<DepartmentRosterView>(roster.Title, Paper.A4Portrait,
                new() { [nameof(DepartmentRosterView.Roster)] = roster }, AccountClaims.DisplayNameOf(http.User), cancellationToken: ct);
            return Pdf(http, pdf, roster.Title.Replace("  ", " "));
        });

        // RPT-21 รายชื่อพนักงานทั้งหมด (รายงาน EMPLO): A4 ตั้ง.
        employees.MapGet("/working", async (HttpContext http, OnboardingDirectory directory, ReportPrinter printer, CancellationToken ct) =>
        {
            var pdf = await printer.PrintAsync<WorkingEmployeesView>(WorkingEmployeesView.Title, Paper.A4Portrait,
                new() { [nameof(WorkingEmployeesView.List)] = await directory.GetWorkingEmployeesAsync() },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: ct);
            return Pdf(http, pdf, WorkingEmployeesView.Title);
        });

        // RPT-21 ทะเบียนประวัติพนักงาน: A4 นอน, shrunk to the page (D-4).
        employees.MapGet("/register", async (HttpContext http, OnboardingDirectory directory, ReportPrinter printer, CancellationToken ct) =>
        {
            var pdf = await printer.PrintAsync<EmployeeRegisterView>(EmployeeRegisterView.Title, Paper.A4Landscape,
                new() { [nameof(EmployeeRegisterView.Lines)] = await directory.GetRegisterAsync() },
                AccountClaims.DisplayNameOf(http.User), cancellationToken: ct);
            return Pdf(http, pdf, EmployeeRegisterView.Title);
        });

        // RPT-56 แบบฟอร์มการรับพนักงานเข้าทดลองงาน: ระดับ A, A4 ตั้ง.
        employees.MapGet("/{id:int}/hiring-form", async (int id, string? applied, string? interviewed, HttpContext http,
            OnboardingDirectory directory, DocumentNumberService numbers, ReportPrinter printer, CancellationToken ct) =>
        {
            if (await directory.GetEmployeeAsync(id) is not { } employee) return Results.NotFound("ไม่พบพนักงาน");
            var page = OnboardingForms.HiringForm(employee, new HiringFormInput(ParseDate(applied), ParseDate(interviewed)),
                await numbers.PrintedTextAsync(DocumentKeys.ProbationForm));
            return Pdf(http, await printer.PrintFormAsync(OnboardingForms.HiringTitle, [page], ct), $"{OnboardingForms.HiringTitle} {employee.Code}");
        });

        // RPT-56 สัญญาจ้างแรงงานรายเดือน (ประเภท รายเดือน) / รายวัน (everyone else): ระดับ A, A4 ตั้ง.
        employees.MapGet("/{id:int}/contract", async (int id, HttpContext http, OnboardingDirectory directory, DocumentNumberService numbers,
            ReportPrinter printer, TimeProvider clock, CancellationToken ct) =>
        {
            if (await directory.GetEmployeeAsync(id) is not { } employee) return Results.NotFound("ไม่พบพนักงาน");
            var key = employee.IsMonthly ? DocumentKeys.MonthlyContract : DocumentKeys.DailyContract;
            var page = OnboardingForms.Contract(employee, employee.IsMonthly, DateOnly.FromDateTime(clock.GetLocalNow().DateTime),
                await numbers.PrintedTextAsync(key));
            return Pdf(http, await printer.PrintFormAsync(OnboardingForms.ContractTitle, [page], ct),
                $"สัญญาจ้างแรงงาน{(employee.IsMonthly ? "รายเดือน" : "รายวัน")} {employee.Code}");
        });

        // RPT-22 (SCR-06 "พิมพ์"): ใบนำส่งเอกสาร, ใบประเมินทดลองงาน, ใบขอบรรจุ — ระดับ A, A5 ตั้ง, the three as one PDF in
        // the order the legacy macro opened them.
        employees.MapGet("/{id:int}/probation-approval", async (int id, string? section, string? due, string? @return, HttpContext http,
            OnboardingDirectory directory, DocumentNumberService numbers, ReportPrinter printer, TimeProvider clock, CancellationToken ct) =>
        {
            if (await directory.GetEmployeeAsync(id) is not { } employee) return Results.NotFound("ไม่พบพนักงาน");
            var input = new ProbationApprovalInput(string.IsNullOrWhiteSpace(section) ? employee.SectionName : section.Trim(),
                ParseDate(due), ParseDate(@return));
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            var stats = await directory.GetProbationStatisticsAsync(id);
            FormPage[] pages =
            [
                OnboardingForms.Transmittal(employee, input, today, await numbers.PrintedTextAsync(DocumentKeys.DocumentTransmittal)),
                OnboardingForms.Evaluation(employee, input, stats, today, await numbers.PrintedTextAsync(DocumentKeys.ProbationEvaluation)),
                OnboardingForms.Request(employee, await numbers.PrintedTextAsync(DocumentKeys.ProbationRequest)),
            ];
            return Pdf(http, await printer.PrintFormAsync(OnboardingForms.ProbationTitle, pages, ct), $"{OnboardingForms.ProbationTitle} {employee.Code}");
        });
    }

    /// <summary>Shown in the browser's PDF viewer (inline) under a readable file name.</summary>
    private static IResult Pdf(HttpContext http, byte[] pdf, string name)
    {
        http.Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(name + ".pdf")}";
        http.Response.Headers.CacheControl = "no-store";
        return Results.File(pdf, "application/pdf");
    }
}

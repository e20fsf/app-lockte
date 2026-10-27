using Lockte.Org;
using Lockte.Prod;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Tests;

/// <summary>รายงานผลผลิตประจำเดือน (RPT-16, BR-020), ผลผลิตรวมทุกแผนก / ไดคาสท์ and ผลผลิตเดือน (#138). Fictional data; each test works in a year of its own and transfers its range into the stock first.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class YieldReportTests(LocalDbFixture db)
{
    private static int year = 2900;
    private static int counter = Random.Shared.Next(10000, 60000);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static DateOnly NewMonth() => new(Interlocked.Increment(ref year), 9, 1);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("report1");
        return await action(scope);
    }

    private async Task<int> MachineAsync(string code)
    {
        if (await AsAsync(s => s.Get<OrgDirectory>().FindMachineByCodeAsync(code)) is { } existing) return existing.Id;
        var department = await AsAsync(async s =>
        {
            var org = s.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ผลิต"), 1, null))).Id!.Value;
            return (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 1, section, null, [], null))).Id!.Value;
        });
        var saved = await AsAsync(s => s.Get<OrgRegistry>().SaveMachineAsync(new MachineInput(null, code, "เครื่องผลผลิต", department, null)));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return saved.Id!.Value;
    }

    private async Task<int> ProductAsync(string code, decimal? weight = null, int? typeId = null, int? modelId = null)
    {
        var saved = await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, code, "สินค้า " + code, null, Unit: "ชิ้น", AluminumWeight: weight, ProductTypeId: typeId, ModelId: modelId)));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        return saved.Id!.Value;
    }

    private async Task LineAsync(string department, int? machine, DateOnly date, int product, decimal good, decimal? reject)
    {
        var no = department + Interlocked.Increment(ref counter).ToString("00000");
        var saved = await AsAsync(s => s.Get<ProductionReportRegistry>().SaveAsync(new ProductionReportInput(null, null, department, date, no, machine,
            [new ProductionReportLineInput(null, null, product, null, null, null, 8m, null, null, good, reject, null, null, null)]), ProductionSaveMode.Save));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
    }

    private Task TransferAsync(DateOnly month) => AsAsync(s => s.Get<StockRegistry>().TransferProductionAsync(month, month.AddMonths(1).AddDays(-1)));

    private Task<YieldReport> MonthlyAsync(DateOnly month)
        => AsAsync(s => YieldReports.MonthlyAsync(s.Get<ProductionReportDirectory>(), s.Get<OrgDirectory>(), month, month.AddMonths(1).AddDays(-1)));

    private static async Task<string> HtmlAsync<TView>(object report) where TView : Microsoft.AspNetCore.Components.IComponent
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<TView>(new() { ["Report"] = report }));

    [Fact] // BR-020 #1: good 9,880 and reject 120 → 1.2 %, which is not over the objective 1.2 → ผ่าน; the group is told by the product code
    public async Task The_percent_of_rejects_is_judged_against_the_objective_of_the_group()
    {
        var month = NewMonth();
        var part = await ProductAsync(Unique("G1"));
        await LineAsync("D", null, month.AddDays(2), part, 9880, 120);
        await TransferAsync(month);

        var report = await MonthlyAsync(month);

        var row = Assert.Single(report.Rows);
        Assert.Equal(("ไดคาสท์", "ก้ามเบรก", 9880m, 120m, 10000m, 1.2m, true), (row.Department, row.Name, row.Good, row.Reject, row.Total, row.Percent, row.Passed));
        var html = await HtmlAsync<YieldMonthlyView>(report);
        Assert.Contains("1.200", html);
        Assert.Contains("ผ่าน", html);
        Assert.Contains("0.80", html);
        Assert.Contains("ผู้จัดการโรงงาน", html);
    }

    [Fact] // BR-020 #2: 87,354 good and 173.5 rejected → 0.198 %, well under 1.2
    public async Task A_real_looking_month_comes_out_under_the_objective()
    {
        var month = NewMonth();
        var part = await ProductAsync(Unique("G1"));
        await LineAsync("D", null, month.AddDays(1), part, 87354, 173.5m);
        await TransferAsync(month);

        var row = Assert.Single((await MonthlyAsync(month)).Rows);

        Assert.Equal(0.198m, Math.Round(row.Percent!.Value, 3));
        Assert.True(row.Passed);
    }

    [Fact] // over the objective → ไม่ผ่าน; the machine decides ตกแต่ง (G3* on T01 only) and a group with no output is not shown
    public async Task A_group_over_its_objective_fails_and_the_machine_picks_the_group()
    {
        var month = NewMonth();
        var (t01, t09) = (await MachineAsync("T01"), await MachineAsync("T09"));
        var part = await ProductAsync(Unique("G3"));
        await LineAsync("T", t01, month.AddDays(1), part, 990, 10); // 1 % > 0.16
        await LineAsync("T", t09, month.AddDays(1), part, 5000, 0); // another machine: not ตกแต่ง
        await TransferAsync(month);

        var report = await MonthlyAsync(month);

        var row = Assert.Single(report.Rows);
        Assert.Equal(("ตกแต่ง", 990m, 10m, false), (row.Department, row.Good, row.Reject, row.Passed));
        Assert.DoesNotContain(report.Rows, r => r.Department == "ไดคาสท์");
    }

    [Fact] // only the reports that went into the stock are counted; nothing made (ดี + เสีย = 0) shows "-" for the %
    public async Task Only_transferred_reports_count_and_zero_output_shows_a_dash()
    {
        var month = NewMonth();
        var part = await ProductAsync(Unique("G5"));
        await LineAsync("B", null, month.AddDays(1), part, 0, 0);
        await TransferAsync(month);
        await LineAsync("B", null, month.AddDays(2), part, 500, 50); // made after the transfer: not yet in the stock

        var report = await MonthlyAsync(month);

        var row = Assert.Single(report.Rows);
        Assert.Equal((0m, null, null), (row.Total, row.Percent, row.Passed));
        Assert.Contains("-", await HtmlAsync<YieldMonthlyView>(report));
        Assert.Contains("ไม่มีข้อมูล", await HtmlAsync<YieldMonthlyView>(await MonthlyAsync(NewMonth())));
    }

    [Fact] // a die-cast D0… part on a d… machine has a row of its own, with the objective 5 %
    public async Task A_die_cast_part_has_a_row_by_name()
    {
        var month = NewMonth();
        var d01 = await MachineAsync("D01");
        var part = await ProductAsync(Unique("D0"));
        await LineAsync("D", d01, month.AddDays(1), part, 960, 40);
        await TransferAsync(month);

        var row = Assert.Single((await MonthlyAsync(month)).Rows);

        Assert.Equal(("ไดคาสท์", 5m, 4m), (row.Department, row.Target, row.Percent));
        Assert.Contains("สินค้า", row.Name);
    }

    [Fact] // รวมทุกแผนก: by department → type → product, or by model; the die-cast list has the aluminium used (blank when there is no weight)
    public async Task The_all_department_and_die_cast_reports()
    {
        var month = NewMonth();
        var type = Unique("ประเภท");
        var model = Unique("รุ่น");
        var typeId = (await AsAsync(s => s.Get<ProductListRegistry>().SaveProductTypeAsync(null, type, null))).Id;
        var modelId = (await AsAsync(s => s.Get<ProductListRegistry>().SaveModelAsync(null, model, null))).Id;
        var heavy = await ProductAsync(Unique("G1"), weight: 0.125m, typeId: typeId, modelId: modelId);
        var light = await ProductAsync(Unique("G1"), weight: null, typeId: typeId, modelId: modelId);
        await LineAsync("D", null, month.AddDays(1), heavy, 800, 200);
        await LineAsync("D", null, month.AddDays(2), light, 50, 0);
        await TransferAsync(month);
        var to = month.AddMonths(1).AddDays(-1);

        var byCode = await AsAsync(s => YieldReports.AllDepartmentsAsync(s.Get<ProductionReportDirectory>(), s.Get<OrgDirectory>(), month, to, byModel: false));
        var byModel = await AsAsync(s => YieldReports.AllDepartmentsAsync(s.Get<ProductionReportDirectory>(), s.Get<OrgDirectory>(), month, to, byModel: true));
        var dieCast = await AsAsync(s => YieldReports.DieCastAsync(s.Get<ProductionReportDirectory>(), s.Get<OrgDirectory>(), month, to));

        Assert.Equal(2, byCode.Rows.Count);
        Assert.All(byCode.Rows, r => Assert.Equal(("ไดคาสท์", type), (r.Department, r.Group)));
        var merged = Assert.Single(byModel.Rows);
        Assert.Equal((model, 850m, 200m, 1050m), (merged.Name, merged.Good, merged.Reject, merged.Total));
        Assert.Null(merged.Code);
        Assert.Equal(125m, dieCast.Rows.Single(r => r.Weight == 0.125m).Aluminum); // 1,000 × 0.125
        Assert.Null(dieCast.Rows.Single(r => r.Weight is null).Aluminum);
        Assert.Equal(125m, dieCast.TotalAluminum);
        Assert.Contains(type, await HtmlAsync<YieldAllDepartmentsView>(byCode));
        Assert.Contains("รวมแผนกไดคาสท์", await HtmlAsync<YieldAllDepartmentsView>(byCode));
        Assert.Contains("รวมอลูมิเนียม", await HtmlAsync<YieldDieCastView>(dieCast));
    }

    [Fact] // ผลผลิตเดือน: the twelve months at once, a month with none is 0, and the total
    public async Task The_year_table_has_twelve_months_at_once()
    {
        var january = NewMonth();
        var part = await ProductAsync(Unique("G6"));
        await LineAsync("G", null, january.AddMonths(-8).AddDays(3), part, 100, 1); // January
        await LineAsync("G", null, january.AddMonths(-8).AddDays(4), part, 50, 1);
        await TransferAsync(january.AddMonths(-8));
        await LineAsync("G", null, january.AddMonths(2).AddDays(3), part, 70, 1); // November
        await TransferAsync(january.AddMonths(2));

        var report = await AsAsync(s => YieldReports.YearAsync(s.Get<ProductionReportDirectory>(), s.Get<OrgDirectory>(), january.Year));

        var row = Assert.Single(report.Rows);
        Assert.Equal((150m, 0m, 70m, 220m), (row.Months[0], row.Months[1], row.Months[10], row.Total));
        var html = await HtmlAsync<YieldYearView>(report);
        Assert.Contains("พ.ศ.", html);
    }

    [Fact] // the control number of the report as the sample prints it
    public async Task The_control_number_is_the_one_of_the_sample()
    {
        var printed = await AsAsync(s => s.Get<DocumentNumberService>().PrintedTextAsync(DocumentKeys.MonthlyYield));

        Assert.Equal("PC-PD003-5/2547 ฉบับที่ 1", printed);
    }
}

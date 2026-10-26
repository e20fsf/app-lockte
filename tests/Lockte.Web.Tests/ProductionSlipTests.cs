using Lockte.Org;
using Lockte.Prod;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Tests;

/// <summary>ใบรายงานผลผลิตประจำวัน / ใบโอนผลผลิต (RPT-10, #139) of the seven departments. Fictional data; each test prints a month of its own.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class ProductionSlipTests(LocalDbFixture db)
{
    private static int year = 2700;
    private static int counter = Random.Shared.Next(10000, 60000);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static DateOnly NewMonth() => new(Interlocked.Increment(ref year), 5, 1);

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("report1");
        return await action(scope);
    }

    private async Task<int> ProductAsync()
    {
        var saved = await AsAsync(s => s.Get<ProductRegistry>().SaveAsync(new ProductInput(null, Unique("ZQ"), "สินค้าใบโอน", null, Unit: "ชิ้น")));
        return saved.Id!.Value;
    }

    private async Task SaveAsync(string department, DateOnly date, int product, decimal good, decimal? reject)
    {
        var no = department + Interlocked.Increment(ref counter).ToString("00000");
        var saved = await AsAsync(s => s.Get<ProductionReportRegistry>().SaveAsync(new ProductionReportInput(null, null, department, date, no, null,
            [new ProductionReportLineInput(null, null, product, null, null, null, 8m, "LM1", "LM2", good, reject, null, null, "260903B35-A/1")]), ProductionSaveMode.Save));
        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
    }

    private Task<ProductionSlipsReport> BuildAsync(DateOnly from, DateOnly to, string? department = null, string? number = null)
        => AsAsync(s => ProductionSlipReports.BuildAsync(s.Get<ProductionReportDirectory>(), s.Get<ProductDirectory>(), s.Get<OrgDirectory>(), s.Get<IProductionEmployeeLookup>(), from, to, department, number));

    [Fact] // all seven departments print on the same page, with the lot labels of their settings; the totals and the percentage against the target
    public async Task Every_department_prints_with_its_labels_and_target()
    {
        var month = NewMonth();
        var product = await ProductAsync();
        foreach (var d in ProductionDepartments.All) await SaveAsync(d.Code, month.AddDays(3), product, 980, 20);

        var report = await BuildAsync(month, month.AddMonths(1).AddDays(-1));

        Assert.Equal(7, report.Slips.Count);
        var die = report.Slips.Single(s => s.Department.Code == "D");
        Assert.Equal((980m, 20m, 1000m, 2.00m, false), (die.Good, die.Reject, die.Total, die.RejectPercent!.Value, die.Passed!.Value)); // 2.00 % is over 1.2
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<ProductionSlipsView>(new() { ["Report"] = report }));
        Assert.Contains("ล๊อตอลูมิเนียม", html);
        Assert.Contains("ล๊อตเคมีผ้าเบรก", html);
        Assert.Contains("ไม่ผ่าน ไม่เกิน 1.2", html); // spaced, unlike the legacy
        Assert.Contains("ไม่ผ่าน ไม่เกิน 0.16", html);
        Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(html, "page-break-after:always").Count); // one page per report
        Assert.Contains("หัวหน้าแผนกรับโอน", html);
        Assert.Contains("หัวหน้าแผนกประกันคุณภาพ", html);
    }

    [Fact] // pass when not over the target; บรรจุ has no target and no pass text
    public async Task A_report_within_the_target_passes_and_packing_has_no_verdict()
    {
        var month = NewMonth();
        var product = await ProductAsync();
        await SaveAsync("D", month.AddDays(1), product, 995, 5); // 0.50 %
        await SaveAsync("P", month.AddDays(1), product, 900, 100);

        var die = (await BuildAsync(month, month.AddMonths(1).AddDays(-1), "D")).Slips.Single();
        var packing = (await BuildAsync(month, month.AddMonths(1).AddDays(-1), "P")).Slips.Single();

        Assert.True(die.Passed);
        Assert.Null(packing.Passed);
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<ProductionSlipsView>(new() { ["Report"] = new ProductionSlipsReport([packing]) }));
        Assert.DoesNotContain("ผ่าน", html);
    }

    [Fact] // the range, the department and the number narrow it; stock does not matter; the date reads with the day of the week
    public async Task The_range_the_department_and_the_number_narrow_the_slips()
    {
        var month = NewMonth();
        var product = await ProductAsync();
        await SaveAsync("T", month.AddDays(1), product, 10, null);
        await SaveAsync("T", month.AddDays(20), product, 20, null);
        await SaveAsync("A", month.AddDays(1), product, 30, null);

        var all = await BuildAsync(month, month.AddMonths(1).AddDays(-1));
        var first = await BuildAsync(month, month.AddDays(5));
        var onlyT = await BuildAsync(month, month.AddMonths(1).AddDays(-1), "t");
        var byNumber = await BuildAsync(month, month.AddMonths(1).AddDays(-1), null, all.Slips[0].ReportNo);

        Assert.Equal(3, all.Slips.Count);
        Assert.Equal(2, first.Slips.Count);
        Assert.Equal(2, onlyT.Slips.Count);
        Assert.Single(byNumber.Slips);
        Assert.Matches("^วัน.+ที่ \\d+ .+ \\d{4}$", ProductionSlipReports.DateText(month));
        var empty = await BuildAsync(month.AddYears(-80), month.AddYears(-80).AddDays(2));
        Assert.Contains("ไม่มีข้อมูล", System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<ProductionSlipsView>(new() { ["Report"] = empty })));
    }

    [Fact] // the control number as the sample prints it
    public async Task The_control_number_is_the_one_of_the_sample()
    {
        var printed = await AsAsync(s => s.Get<DocumentNumberService>().PrintedTextAsync(DocumentKeys.ProductionSlip));

        Assert.Equal("PC-PD003-4/2547 ฉบับที่2", printed);
    }
}

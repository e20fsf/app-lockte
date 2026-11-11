using Lockte.Web.Components.Pages.Purch;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>UAT วัตถุดิบ ครั้งที่ 1 (#421), without a database: the quarter list, the year, ผลตรวจ, the date of the commitment letter and its missing footer.</summary>
public sealed class PurchUat2Tests
{
    [Fact]
    public void The_quarter_list_is_quarters_1_to_4_each_with_its_own_text()
    {
        Assert.Equal([1, 2, 3, 4], PurchChoices.Quarters.Select(q => q.Quarter));
        Assert.Equal(4, PurchChoices.Quarters.Select(q => q.Text).Distinct().Count());
        Assert.All(PurchChoices.Quarters, q => Assert.Contains($"ไตรมาสที่ {q.Quarter}", q.Text));
        Assert.DoesNotContain(PurchChoices.Quarters, q => q.Text.Contains('5'));
    }

    [Fact]
    public void The_year_is_shown_in_the_buddhist_era_and_converted_back_for_the_search()
    {
        Assert.Equal(2569, PurchChoices.ToBuddhist(2026));
        Assert.Equal(2026, PurchChoices.ToChristian(2569));
        Assert.Equal(2026, PurchChoices.ToChristian(PurchChoices.ToBuddhist(2026)));
    }

    [Fact]
    public async Task The_summary_names_the_quarter_and_the_buddhist_year_while_the_period_is_still_the_christian_one()
    {
        var summary = new EvaluationSummary(new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30), 2, [new(new(1, "DC-31", "ผู้ขาย DC-31", "ที่อยู่", "เขต", "02", null, null, null, 30, true, "ชิ้นส่วน", "ผลิต", true, true, []), null)]);
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<SupplierEvaluationSummaryView>(new() { ["Report"] = summary }));
        Assert.Contains("ไตรมาสที่ 2", html);
        Assert.Contains("ปี พ.ศ. 2569", html);
        Assert.DoesNotContain("2026", html);
    }

    [Fact]
    public void The_inspection_result_offers_ผ่าน_first_and_ไม่ผ่าน_kept_as_the_old_flag()
    {
        Assert.Equal([(false, "ผ่าน"), (true, "ไม่ผ่าน")], PurchChoices.InspectionResults);
        Assert.False(PurchChoices.DefaultFailed); // ผ่าน by default (BR-032 acts only on rows marked ไม่ผ่าน)
    }

    [Fact]
    public async Task The_commitment_letter_prints_the_date_chosen_and_the_page_has_no_footer()
    {
        var letter = new SupplierCommitmentLetter("ผู้ขายทดสอบ", new DateOnly(2026, 11, 3));
        var body = await ReportBody.RenderAsync<SupplierCommitmentView>(new() { ["Report"] = letter });
        Assert.Contains("วันที่ 3 พฤศจิกายน 2569", System.Net.WebUtility.HtmlDecode(body));

        var html = ReportHtml.Compose(new ReportDocument(SupplierDocumentReports.CommitmentTitle, Paper.A4Portrait, Footer: false), new PrintStamp(DateTimeOffset.Now, "ผู้ทดสอบ"), body);
        Assert.DoesNotContain("@bottom-left", html);
        Assert.DoesNotContain("@bottom-right", html);
        Assert.DoesNotContain("counter(pages)", html);
    }
}

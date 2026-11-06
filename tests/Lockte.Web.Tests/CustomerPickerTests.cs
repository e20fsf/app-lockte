using Lockte.Sales;
using Lockte.Web.Components;
using Lockte.Web.Reports;

namespace Lockte.Web.Tests;

/// <summary>UAT-33 (#414), no database: the search rule over a list and the seven columns of a pick-box row.</summary>
public sealed class CustomerPickerTests
{
    [Theory]
    [InlineData("05", true)]
    [InlineData("05-0", true)]
    [InlineData("-", true)]
    [InlineData("ร้าน", false)]
    [InlineData("A05", false)]
    [InlineData("05 ", false)] // callers trim first
    public void Digits_and_dashes_only_are_a_code(string term, bool isCode) => Assert.Equal(isCode, CustomerSearch.IsCodeTerm(term));

    private static CustomerDto Customer(string code, string name) => new(1, code, name, [], 30, "9 หมู่ 1 ต.ทดสอบ", Province: "จังหวัดทดสอบ", SalesTerritoryCode: "05", Remark: "ส่งช่วงเช้า");

    [Fact] // the in-memory rule (a pick-box with its own candidates) is the same as the register's
    public void Filtering_a_list_in_memory_follows_the_same_rule()
    {
        var list = new[] { Customer("05-02-001", "ข"), Customer("01-01-005", "ร้านห้า"), Customer("05-01-001", "ก") };

        Assert.Equal(["05-01-001", "05-02-001"], CustomerSearch.Filter(list, "05").Select(c => c.Code));
        Assert.Equal(["01-01-005"], CustomerSearch.Filter(list, "ร้านห้า").Select(c => c.Code));
        Assert.Equal(3, CustomerSearch.Filter(list, "").Count());
    }

    [Fact] // the seven columns of the legacy combo, in order, each a single line
    public async Task A_row_shows_the_seven_columns_in_order()
    {
        var html = System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<CustomerPickerRow>(new() { [nameof(CustomerPickerRow.Customer)] = Customer("05-01-001", "ร้านทดสอบ") }));

        var cells = System.Text.RegularExpressions.Regex.Matches(html, "<span class=\"(cp-[a-z]+)\"[^>]*>([^<]*)</span>").Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();

        Assert.Equal(
            [("cp-code", "05-01-001"), ("cp-name", "ร้านทดสอบ"), ("cp-addr", "9 หมู่ 1 ต.ทดสอบ"), ("cp-sale", "05"), ("cp-credit", "30 วัน"), ("cp-prov", "จังหวัดทดสอบ"), ("cp-remark", "ส่งช่วงเช้า")],
            cells);
        Assert.Contains("white-space:nowrap", html);
    }
}

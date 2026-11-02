using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Hr;

namespace Lockte.Web.Tests;

/// <summary>#348 ใบลาออก: the content is spread over the whole A4 page, still one page.</summary>
[Collection(ChromiumCollection.Name)]
public sealed class ResignationLetterLayoutTests(ChromiumFixture chromium)
{
    private static async Task<string> BodyAsync()
    {
        var letter = new ResignationLetterData("A005", "นางสาวทองใส  โชยรัมย์", "ประกอบ", "D", new DateOnly(1992, 10, 26),
            new DateOnly(2026, 10, 2), "กลับบ้าน", "นางสาวอัมพร   เจริญสุข", "นายสมชาย ใจดี", "นายสมบัติ  ตัวอย่างดี");
        return await ReportBody.RenderAsync<ResignationLetterView>(new() { ["Letter"] = letter, ["Today"] = new DateOnly(2026, 10, 29) });
    }

    [Fact] // the first text starts in the top part and the last signature is in the bottom part of the page, nothing runs past the margin
    public async Task Content_is_spread_over_the_page()
    {
        var tops = Regex.Matches(await BodyAsync(), @"top:([\d.]+)mm").Select(m => double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();

        Assert.InRange(tops.Min(), 0, 25);
        Assert.InRange(tops.Max(), 255, 285);
    }

    [Fact]
    public async Task Prints_one_A4_page()
    {
        var pdf = await chromium.Renderer.RenderAsync(FormHtml.Compose(ResignationLetterView.Title, Paper.A4Portrait, await BodyAsync()), Paper.A4Portrait);

        Assert.Single(Regex.Matches(System.Text.Encoding.Latin1.GetString(pdf), @"/Type /Page\b"));
    }
}

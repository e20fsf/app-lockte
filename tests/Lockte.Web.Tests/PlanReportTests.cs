using Lockte.Calib;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Calib;

namespace Lockte.Web.Tests;

/// <summary>RPT-01 แผนการสอบเทียบ (#202) without a database: fixed text, the months from the settings, the notes and the sign boxes.</summary>
public sealed class PlanViewTests
{
    private static async Task<string> RenderAsync(PlanReport report)
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<PlanView>(new() { ["Report"] = report }));

    /// <summary>The two rows of the table body.</summary>
    private static List<string> Body(string html) => html.Split("<tbody>")[1].Split("</tbody>")[0].Split("<tr").Skip(1).ToList();

    private static PlanReport Default => PlanReport.Of(new CalibSettingsDto(4m, 2.1m, CalibSettingsDefaults.PlanExternalMonth, CalibSettingsDefaults.PlanInternalMonth,
        CalibSettingsDefaults.PlanResponsible, CalibSettingsDefaults.PlanReviewer, CalibSettingsDefaults.PlanNote, CalibSettingsDefaults.ExternalRegisterNote, "", "", "", "", "", "", null));

    [Fact]
    public async Task The_plan_has_the_two_rows_the_parties_and_the_twelve_months()
    {
        var html = await RenderAsync(Default);
        foreach (var text in new[] { "สอบเทียบภายนอก", "สอบเทียบภายใน", "1 ครั้ง / ปี", ">QA<", ">QMR<", "ผู้รับผิดชอบ", "ผู้ตรวจสอบ", ">ม.ค.<", ">ธ.ค.<" }) Assert.Contains(text, html);
        Assert.Equal(12, PlanReport.MonthNames.Count);
        Assert.Equal(2, html.Split("1 ครั้ง / ปี").Length - 1);
    }

    [Fact] // print sample: กค. green on the external row, สค. on the internal
    public async Task The_month_of_each_round_is_shaded_green()
    {
        var html = await RenderAsync(Default);
        var rows = Body(html);
        Assert.Equal(2, rows.Count);
        int Planned(string row) => row.Split("<td").Skip(1).ToList().FindIndex(c => c.Contains("planned", StringComparison.Ordinal)) - 5 + 1; // 5 text cells before the months
        Assert.Equal((7, 8), (Planned(rows[0]), Planned(rows[1])));
        Assert.Equal(1, rows[0].Split("planned").Length - 1);
    }

    [Fact] // the months are master data: changed in the settings, the shading moves
    public async Task Another_month_moves_the_shading()
    {
        var html = await RenderAsync(Default with { ExternalMonth = 3, InternalMonth = 12 });
        var rows = Body(html);
        int Planned(string row) => row.Split("<td").Skip(1).ToList().FindIndex(c => c.Contains("planned", StringComparison.Ordinal)) - 5 + 1;
        Assert.Equal((3, 12), (Planned(rows[0]), Planned(rows[1])));
    }

    [Fact] // the sign boxes are จัดเตรียมโดย / ตรวจสอบโดย / อนุมัติโดย; the note ends at 350 (the external register's does not)
    public async Task The_note_and_the_sign_boxes_are_under_the_table()
    {
        var html = await RenderAsync(Default);
        Assert.Contains("จัดเตรียมโดย", html);
        Assert.Contains("ตรวจสอบโดย", html);
        Assert.Contains("อนุมัติโดย", html);
        Assert.Contains("100,150,200,250,300,350", html);
        Assert.DoesNotContain("100,150,200,250 3.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("100,150,200,250 3.", CalibSettingsDefaults.PlanNote, StringComparison.Ordinal);
        Assert.Contains("100,150,200,250 3.", CalibSettingsDefaults.ExternalRegisterNote, StringComparison.Ordinal);
    }
}

/// <summary>The settings behind RPT-01 and the document numbers of the calibration forms (#202).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class PlanSettingsTests(LocalDbFixture db)
{
    private async Task<T> AsQaAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    [Fact]
    public async Task The_plan_settings_are_saved_read_back_and_checked()
    {
        var before = await AsQaAsync(s => s.Get<CalibSettingsDirectory>().GetAsync());
        var save = await AsQaAsync(s => s.Get<CalibSettingsRegistry>().SaveAsync(new(before.StickerWidthCm, before.StickerHeightCm, before.RowVersion, 9, 10, "QA", "QMR", "หมายเหตุ " + Guid.NewGuid().ToString("N")[..4])));
        Assert.True(save.Succeeded, string.Join(", ", save.Errors));
        var after = await AsQaAsync(s => s.Get<CalibSettingsDirectory>().GetAsync());
        Assert.Equal((9, 10), (after.PlanExternalMonth, after.PlanInternalMonth));
        Assert.StartsWith("หมายเหตุ ", after.PlanNote);
        Assert.Equal(before.ExternalRegisterNote, after.ExternalRegisterNote); // left out = unchanged

        var bad = await AsQaAsync(s => s.Get<CalibSettingsRegistry>().SaveAsync(new(after.StickerWidthCm, after.StickerHeightCm, after.RowVersion, 13, 0)));
        Assert.Equal(["เดือนของแผนการสอบเทียบต้องเป็น 1–12"], bad.Errors);

        // put the legacy values back
        var restore = await AsQaAsync(s => s.Get<CalibSettingsRegistry>().SaveAsync(new(after.StickerWidthCm, after.StickerHeightCm, after.RowVersion,
            CalibSettingsDefaults.PlanExternalMonth, CalibSettingsDefaults.PlanInternalMonth, CalibSettingsDefaults.PlanResponsible, CalibSettingsDefaults.PlanReviewer, CalibSettingsDefaults.PlanNote)));
        Assert.True(restore.Succeeded);
    }

    [Theory]
    [InlineData(Lockte.Web.Reports.DocumentKeys.CalibPlan, "PC-QA009-2/2547 ฉบับที่ 2")]
    [InlineData(Lockte.Web.Reports.DocumentKeys.CalibRegisterInternal, "PC-QA009-1/2552 ฉบับที่ 2")]
    [InlineData(Lockte.Web.Reports.DocumentKeys.CalibRegisterExternal, "PC-QA009-1/2547 ฉบับที่ 2")]
    [InlineData(Lockte.Web.Reports.DocumentKeys.CalibRecord, "PC-QA009-4/2547 ฉบับที่ 2")]
    public async Task The_calibration_documents_have_their_control_numbers(string key, string printed)
        => Assert.Equal(printed, await AsQaAsync(s => s.Get<Lockte.Web.Reports.DocumentNumberService>().PrintedTextAsync(key)));
}

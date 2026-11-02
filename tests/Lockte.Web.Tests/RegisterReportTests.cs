using Lockte.Calib;
using Lockte.Web.Reports;
using Lockte.Web.Reports.Calib;

namespace Lockte.Web.Tests;

/// <summary>RPT-02 บัญชีรายชื่อเครื่องมือวัด (#203) without a database: the twelve columns, the Buddhist date, `DC (D02)`, the JIG's blank columns, the sign boxes once.</summary>
public sealed class RegisterViewTests
{
    private static InstrumentDto Row(InstrumentKind kind, string code, string? name = "Vernier Digital", DateOnly? registered = null, string? dept = "DC", string[]? machines = null, string? calibrator = "ช่าง ทดสอบ")
        => new(1, kind, code, name, registered ?? new DateOnly(2024, 5, 3), "Mitutoyo", 1, dept, "แผนกทดสอบ", [], machines ?? [], "0-150", "0.01", "0.1", "mm", "GB-QA001/2551", 7, calibrator, null, null,
            "Axis 7, 6", "WI-QA049", "หมายเหตุ", [1]);

    private static async Task<string> RenderAsync(RegisterReport report)
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<RegisterView>(new() { ["Report"] = report }));

    [Fact]
    public async Task A_row_prints_the_twelve_columns_with_the_legacy_department_text_and_the_buddhist_date()
    {
        var html = await RenderAsync(new RegisterReport(InstrumentKind.PressureGauge, [Row(InstrumentKind.PressureGauge, "PG-DC002/2569", dept: "DC", machines: ["D02"])], null));
        foreach (var text in new[] { ">ที่<", ">ชื่อเครื่องมือวัด<", ">หมายเลขเครื่อง<", ">วันที่ขึ้นบัญชี<", ">ยี่ห้อ/ผู้ผลิต<", ">แผนก<", ">พิกัดใช้งาน<", ">ค่าความละเอียด<", ">หน่วย<", "ขีดจำกัดที่ยอมรับได้", ">ผู้สอบเทียบ<", ">หมายเหตุ<",
                     ">PG-DC002/2569<", ">3/5/2567<", ">DC (D02)<", ">ช่าง ทดสอบ<", ">0-150<", ">mm<" })
            Assert.Contains(text, html);
        Assert.Equal(12, html.Split("</th>").Length - 1);
    }

    [Fact]
    public async Task Rows_are_numbered_in_order_and_the_sign_boxes_come_once_after_the_last()
    {
        var rows = Enumerable.Range(1, 40).Select(n => Row(InstrumentKind.TempControl, $"TC-DC{n:000}/2568")).ToList();
        var html = await RenderAsync(new RegisterReport(InstrumentKind.TempControl, rows, null));
        Assert.Contains("<td class=\"num\">40</td>", html);
        Assert.Equal(1, html.Split("จัดทำโดย").Length - 1);
        Assert.True(html.IndexOf("TC-DC040/2568", StringComparison.Ordinal) < html.IndexOf("จัดทำโดย", StringComparison.Ordinal));
        Assert.DoesNotContain("หมายเหตุ:", html, StringComparison.Ordinal); // the internal register has no note
    }

    [Fact] // reports.md ⚠ JIG: the headings of ยี่ห้อ/พิกัด/ความละเอียด/หน่วย stay but their cells are empty; แกน, คู่มือ and เวอร์เนียที่ใช้ are not printed
    public async Task A_jig_leaves_brand_range_resolution_and_unit_empty()
    {
        var html = await RenderAsync(new RegisterReport(InstrumentKind.Jig, [Row(InstrumentKind.Jig, "JG-QA001/2550", "RX100,MATE'U,BELL,087,200")], null));
        Assert.Contains(">RX100,MATE'U,BELL,087,200<", html);
        Assert.Contains(">ยี่ห้อ/ผู้ผลิต<", html);
        foreach (var text in new[] { "Mitutoyo", "0-150", "0.01", ">mm<", "Axis 7, 6", "WI-QA049", "GB-QA001/2551" }) Assert.DoesNotContain(text, html, StringComparison.Ordinal);
        Assert.Contains(">0.1<", html); // the limit is printed
    }

    [Fact]
    public async Task No_instrument_prints_no_data()
        => Assert.Contains("ไม่มีข้อมูล", await RenderAsync(new RegisterReport(InstrumentKind.Scale, [], null)));
}

/// <summary>The external register (#204): the company in full, the fixed note, the first sign box "จัดทำโดย".</summary>
public sealed class ExternalRegisterViewTests
{
    private static InstrumentDto External(string code, string? provider)
        => new(1, InstrumentKind.External, code, "GAUGE BLOCK (5 mm)", new DateOnly(2012, 10, 1), "Mitutoyo", 1, "QA", "ประกันคุณภาพ", [], [], "5", null, "0.05", "MM", null, null, null, 9, provider,
            null, null, "สอบเทียบที่โรงงาน", [1]);

    private static async Task<string> RenderAsync(RegisterReport report)
        => System.Net.WebUtility.HtmlDecode(await ReportBody.RenderAsync<RegisterView>(new() { ["Report"] = report }));

    [Fact] // the sample cut the name to `Quality Calibratio` (D-1); here it prints whole
    public async Task The_company_is_printed_in_full_and_the_note_under_the_sign_boxes()
    {
        var company = "Quality Calibration Co.,Ltd.";
        var html = await RenderAsync(new RegisterReport(InstrumentKind.External, [External("GB-QA001/2551", company), External("Q01-1", null)], CalibSettingsDefaults.ExternalRegisterNote));
        Assert.Contains($">{company}<", html);
        Assert.Contains(">Q01-1<", html); // a code without a year is accepted
        Assert.Contains("จัดทำโดย", html);
        Assert.Contains("100,150,200,250 3.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("350", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("จัดทำโดย", StringComparison.Ordinal) < html.IndexOf("เครื่องทดสอบแรงเสียดทาน", StringComparison.Ordinal));
        Assert.Equal(2, html.Split("<td class=\"num\">").Length - 1);
    }

    [Fact]
    public void The_external_register_has_its_own_title_and_control_number()
    {
        Assert.Equal("บัญชีรายชื่อเครื่องมือวัด(สอบเทียบภายนอก)", RegisterReports.TitleOf(InstrumentKind.External));
        Assert.Equal(DocumentKeys.CalibRegisterExternal, RegisterReports.DocumentKeyOf(InstrumentKind.External));
        Assert.Equal(DocumentKeys.CalibRegisterInternal, RegisterReports.DocumentKeyOf(InstrumentKind.Scale));
    }
}

/// <summary>The rows RPT-02 takes (AC-12, #203): one kind, in order of หมายเลขเครื่อง, a deleted instrument not among them.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class RegisterDataTests(LocalDbFixture db)
{
    private async Task<T> AsQaAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    [Fact] // reports.md: the first rows of the caliper register are HG-…, MM-…, VN-…
    public async Task The_register_lists_one_kind_by_code_and_not_a_deleted_instrument()
    {
        var tag = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        foreach (var code in new[] { $"VN-{tag}02/2564", $"HG-{tag}02/2547", $"MM-{tag}01/2547" })
            Assert.True((await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(null, InstrumentKind.Caliper, code, null, "Gauge", new DateOnly(2004, 1, 5))))).Succeeded);
        var gone = (await AsQaAsync(s => s.Get<InstrumentDirectory>().FindByCodeAsync($"MM-{tag}01/2547")))!;
        Assert.True((await AsQaAsync(s => s.Get<InstrumentRegistry>().DeleteAsync(gone.Id, gone.RowVersion))).Succeeded);

        var mine = (await AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.Caliper))).Where(i => i.Code.Contains(tag, StringComparison.Ordinal)).Select(i => i.Code).ToList();
        Assert.Equal([$"HG-{tag}02/2547", $"VN-{tag}02/2564"], mine);
    }
}

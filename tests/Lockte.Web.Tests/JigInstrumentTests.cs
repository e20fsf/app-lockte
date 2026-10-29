using Lockte.Calib;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Components.Pages.Calib;

namespace Lockte.Web.Tests;

/// <summary>The screen fields of a JIG (#196, data-model T-jig): no ยี่ห้อ, พิกัด, ความละเอียด or หน่วย; แกน and คู่มือ instead; free-text name.</summary>
public sealed class JigScreenTests
{
    [Fact]
    public void A_jig_form_shows_the_ten_fields_of_the_legacy_table_and_hides_the_others()
    {
        var jig = InstrumentKindInfo.Of(InstrumentKind.Jig);
        Assert.Equal(("ชื่อ JIG", "เพิ่มรายชื่อ JIG"), (jig.NameLabel, jig.AddHeading));
        Assert.True(jig.ShowAxis && jig.ShowManual && jig.ShowStandard && jig.Internal);
        Assert.False(jig.ShowMaker || jig.ShowRange || jig.ShowResolution || jig.ShowUnit);
        Assert.Contains("เวอร์เนีย", jig.StandardLabel);
    }

    [Fact]
    public void The_other_kinds_do_not_show_the_jig_only_fields()
        => Assert.All(InstrumentKindInfo.All.Where(k => k.Kind != InstrumentKind.Jig), k => Assert.False(k.ShowAxis || k.ShowManual));
}

/// <summary>ทะเบียน JIG (#196): codes of the shape `JG-QA001/2550` and `JG-AS001-2/2560`, text limits like `0.20 mm.`.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class JigInstrumentTests(LocalDbFixture db)
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private async Task<T> AsQaAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    [Fact]
    public async Task A_jig_is_added_with_its_own_fields_edited_and_deleted()
    {
        var code = $"JG-A{Tag()}-2/2560"; // เลขชุด-ลำดับที่ — no format is enforced (BR-020)
        var added = await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(null, InstrumentKind.Jig, code, null, "RX100, MATE'U, BELLE,087, 200",
            new DateOnly(2017, 3, 1), Limit: "0.20 mm.", StandardUsed: "VN-QA014/2566", Axis: "7, 6", ManualNo: "WI-QA049", Note: null)));
        Assert.True(added.Succeeded, string.Join(", ", added.Errors));

        var jig = Assert.Single(await AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.Jig)), i => i.Code == code);
        Assert.Equal(("RX100, MATE'U, BELLE,087, 200", "7, 6", "WI-QA049", "0.20 mm.", "VN-QA014/2566", null, null),
            (jig.Name, jig.Axis, jig.ManualNo, jig.Limit, jig.StandardUsed, jig.Maker, jig.Unit)); // text kept as typed, `0.20 mm.` included

        var edited = await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(jig.Id, InstrumentKind.Jig, code, jig.RowVersion, "CLICK", jig.RegisteredOn,
            Limit: jig.Limit, StandardUsed: jig.StandardUsed, Axis: "12, 6.5", ManualNo: jig.ManualNo)));
        Assert.True(edited.Succeeded);
        var changed = Assert.Single(await AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.Jig)), i => i.Code == code);
        Assert.Equal(("CLICK", "12, 6.5"), (changed.Name, changed.Axis));

        var choices = await AsQaAsync(s => s.Get<InstrumentDirectory>().GetChoicesAsync(InstrumentKind.Jig));
        Assert.Contains("12, 6.5", choices.Axes);
        Assert.Contains("WI-QA049", choices.ManualNos);
        Assert.Contains("0.20 mm.", choices.Limits);
        Assert.Contains("VN-QA014/2566", choices.StandardsUsed);

        Assert.True((await AsQaAsync(s => s.Get<InstrumentRegistry>().DeleteAsync(changed.Id, changed.RowVersion))).Succeeded);
        Assert.DoesNotContain(await AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.Jig)), i => i.Code == code);
    }

    [Fact] // the name is free text: it need not equal a RPT-05/06 form name (#199 links them, not this ticket)
    public async Task A_jig_name_that_matches_no_inspection_form_is_accepted()
    {
        var code = $"JG-Q{Tag()}/2550";
        Assert.True((await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(null, InstrumentKind.Jig, code, null, "PIN GAUGE ใหม่ 2 mm")))).Succeeded);
    }
}

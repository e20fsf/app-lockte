using Lockte.Calib;
using Lockte.Web.Components.Pages.Calib;

namespace Lockte.Web.Tests;

/// <summary>The form of an external instrument (#198): a calibration company instead of an employee, and no standard-instrument field.</summary>
public sealed class ExternalScreenTests
{
    [Fact]
    public void An_external_form_has_a_company_not_an_employee_and_no_standard_instrument()
    {
        var external = InstrumentKindInfo.Of(InstrumentKind.External);
        Assert.False(external.Internal);
        Assert.False(external.ShowStandard);
        Assert.Equal("เพิ่มเครื่องมือวัดสอบเทียบภายนอก", external.AddHeading);
    }
}

/// <summary>ทะเบียนเครื่องมือวัดสอบเทียบภายนอก (#198, SCR-02/03 ชนิดภายนอก, AC-06).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class ExternalInstrumentTests(LocalDbFixture db)
{
    private static string Tag() => Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private async Task<T> AsQaAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    private async Task<int> ProviderAsync()
    {
        var name = "บริษัทภายนอก " + Tag();
        Assert.True((await AsQaAsync(s => s.Get<CalibrationProviderRegistry>().AddAsync(name))).Succeeded);
        return (await AsQaAsync(s => s.Get<CalibrationProviderDirectory>().ListAsync())).Single(p => p.Name == name).SupplierId;
    }

    [Fact]
    public async Task An_external_instrument_is_added_edited_and_deleted_with_its_company()
    {
        var provider = await ProviderAsync();
        var code = $"GB-T{Tag()}/2551";
        var added = await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(null, InstrumentKind.External, code, null, "GAUGE BLOCK (5 mm)",
            new DateOnly(2012, 10, 1), "Mitutoyo", Range: " 5", Unit: "MM", Limit: "0.05", ProviderSupplierId: provider)));
        Assert.True(added.Succeeded, string.Join(", ", added.Errors));

        var instrument = Assert.Single(await AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.External)), i => i.Code == code);
        Assert.Equal(("5", provider, null), (instrument.Range, instrument.ProviderSupplierId, instrument.CalibratorEmployeeId)); // leading blank dropped
        Assert.StartsWith("บริษัทภายนอก", instrument.ProviderName);

        // the choices for names and units come from the external kind only
        Assert.Contains("MM", (await AsQaAsync(s => s.Get<InstrumentDirectory>().GetChoicesAsync(InstrumentKind.External))).Units);

        var other = await ProviderAsync();
        var edited = await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(instrument.Id, InstrumentKind.External, code, instrument.RowVersion,
            instrument.Name, instrument.RegisteredOn, instrument.Maker, Range: "5", Unit: "MM", Limit: "0.05", ProviderSupplierId: other, Note: "สอบเทียบที่โรงงาน")));
        Assert.True(edited.Succeeded);
        var changed = Assert.Single(await AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.External)), i => i.Code == code);
        Assert.Equal((other, "สอบเทียบที่โรงงาน"), (changed.ProviderSupplierId, changed.Note));

        Assert.True((await AsQaAsync(s => s.Get<InstrumentRegistry>().DeleteAsync(changed.Id, changed.RowVersion))).Succeeded);
        Assert.DoesNotContain(await AsQaAsync(s => s.Get<InstrumentDirectory>().ListAsync(InstrumentKind.External)), i => i.Code == code);
    }

    [Fact] // data-model: three codes without a year
    public async Task A_code_without_a_year_is_accepted()
    {
        var code = $"Q01-{Tag()}";
        Assert.True((await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(null, InstrumentKind.External, code, null, "เครื่องทดสอบแรงเสียดทาน")))).Succeeded);
    }

    [Fact]
    public async Task A_provider_that_is_not_a_calibration_company_is_refused()
    {
        var result = await AsQaAsync(s => s.Get<InstrumentRegistry>().SaveAsync(new InstrumentInput(null, InstrumentKind.External, $"GB-T{Tag()}/2551", null, ProviderSupplierId: int.MaxValue)));
        Assert.Equal(["ไม่พบบริษัทที่สอบเทียบที่เลือก"], result.Errors);
    }
}

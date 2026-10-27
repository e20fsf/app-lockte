using Lockte.Prod;
using Lockte.Prod.Data;

namespace Lockte.Web.Tests;

/// <summary>
/// ข้อมูลสเปคตรวจคุณภาพ (#128): ชนิดเอกสาร (เทมเพลต), สเปครายรุ่น and จำนวนสุ่มตามขนาด Lot (BR-017, AC-15). Fictional specs only — the real specs of the company stay out of the repo.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class QaSpecTests(LocalDbFixture db)
{
    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("report1");
        return await action(scope);
    }

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private async Task<int> NewTypeAsync(string? code = null, string? revision = "ฉบับที่ 1")
    {
        var result = await AsAsync(s => s.Get<QaSpecRegistry>().SaveTypeAsync(new QaDocumentTypeInput(null, code ?? Unique("WI-T"), revision, "ใบตรวจทดสอบ", "ไดคาสท์ / หลังฉีด", "ชิ้น",
            QaResultColumns.Pieces8, "1-10,11-20;21-30", QaPageLayout.Full, "A4 นอน", null)));
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Id!.Value;
    }

    private static QaSpecInput Spec(int typeId, string name, string? models = null, IReadOnlyList<QaMeasurePointInput>? points = null, IReadOnlyList<string>? defects = null, byte[]? rowVersion = null, int? id = null)
        => new(id, typeId, name, models, 3, points ?? [], defects ?? [], null, null, false, rowVersion);

    // ---------- จำนวนสุ่ม (BR-017, AC-15) ----------

    [Theory] // every limit and the piece after it
    [InlineData(0, 5)] [InlineData(1, 5)] [InlineData(50, 5)] [InlineData(51, 20)] [InlineData(150, 20)] [InlineData(151, 32)] [InlineData(280, 32)] [InlineData(281, 50)]
    [InlineData(500, 50)] [InlineData(501, 80)] [InlineData(1200, 80)] [InlineData(1201, 125)] [InlineData(3200, 125)] [InlineData(3201, 200)] [InlineData(10000, 200)]
    [InlineData(10001, 315)] [InlineData(35000, 315)] [InlineData(35001, 500)] [InlineData(1000000, 500)]
    public void The_sample_size_follows_the_lot_size(int good, int expected)
        => Assert.Equal(expected, QaSampling.SampleSizeFor(good, QaSampling.Standard));

    [Fact] // the table in the database is the legacy one, and the directory answers from it
    public async Task The_sample_sizes_in_the_database_are_the_table_of_the_legacy()
    {
        var table = await AsAsync(s => s.Get<QaSpecDirectory>().GetSampleSizesAsync());
        Assert.Equal(QaSampling.Standard, table);

        Assert.Equal(20, await AsAsync(s => s.Get<QaSpecDirectory>().SampleSizeAsync(150)));
        Assert.Equal(32, await AsAsync(s => s.Get<QaSpecDirectory>().SampleSizeAsync(151)));
    }

    // ---------- ชนิดเอกสาร ----------

    [Fact]
    public async Task A_document_type_keeps_the_control_number_as_typed_and_can_be_edited()
    {
        var code = Unique("WI-E");
        var id = await NewTypeAsync(code, " ฉบับที่ 02 ");

        var saved = (await AsAsync(s => s.Get<QaSpecDirectory>().GetTypeAsync(id)))!;
        Assert.Equal((code, "ฉบับที่ 02", QaResultColumns.Pieces8, QaPageLayout.Full), (saved.Code, saved.Revision, saved.ResultColumns, saved.PageLayout));

        var changed = await AsAsync(s => s.Get<QaSpecRegistry>().SaveTypeAsync(new QaDocumentTypeInput(id, code, "ฉบับที่ 03", "ใบตรวจแก้", null, "คู่", QaResultColumns.Pairs10, null,
            QaPageLayout.PackingBlankFourStrips, "A4 ตั้ง", saved.RowVersion)));
        Assert.True(changed.Succeeded);
        var again = (await AsAsync(s => s.Get<QaSpecDirectory>().GetTypeAsync(id)))!;
        Assert.Equal(("ฉบับที่ 03", "คู่", QaResultColumns.Pairs10, QaPageLayout.PackingBlankFourStrips), (again.Revision, again.SampleUnit, again.ResultColumns, again.PageLayout));
    }

    [Fact] // the same code, edition and name twice is refused; another edition, or another name under the same control number, is fine
    public async Task A_document_type_with_the_same_code_edition_and_name_is_refused()
    {
        var code = Unique("WI-D");
        await NewTypeAsync(code, "ฉบับที่ 1");

        var duplicate = await AsAsync(s => s.Get<QaSpecRegistry>().SaveTypeAsync(new QaDocumentTypeInput(null, code, "ฉบับที่ 1", "ใบตรวจทดสอบ", null, null, QaResultColumns.Pieces8, null, QaPageLayout.Short, null, null)));
        var otherName = await AsAsync(s => s.Get<QaSpecRegistry>().SaveTypeAsync(new QaDocumentTypeInput(null, code, "ฉบับที่ 1", "ใบตรวจชื่ออื่น", null, null, QaResultColumns.Pieces8, null, QaPageLayout.Short, null, null)));
        var otherEdition = await AsAsync(s => s.Get<QaSpecRegistry>().SaveTypeAsync(new QaDocumentTypeInput(null, code, "ฉบับที่ 2", "ฉบับใหม่", null, null, QaResultColumns.Pieces8, null, QaPageLayout.Short, null, null)));
        var blank = await AsAsync(s => s.Get<QaSpecRegistry>().SaveTypeAsync(new QaDocumentTypeInput(null, " ", null, " ", null, null, QaResultColumns.Pieces8, null, QaPageLayout.Short, null, null)));

        Assert.False(duplicate.Succeeded);
        Assert.Contains("อยู่แล้ว", duplicate.Errors[0]);
        Assert.True(otherName.Succeeded);
        Assert.True(otherEdition.Succeeded);
        Assert.Equal(2, blank.Errors.Count);
    }

    [Fact] // a type that specs use cannot be deleted; one without can
    public async Task A_document_type_that_specs_use_cannot_be_deleted()
    {
        var used = await NewTypeAsync();
        var free = await NewTypeAsync();
        await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(used, "สเปคทดสอบ")));

        var usedType = (await AsAsync(s => s.Get<QaSpecDirectory>().GetTypeAsync(used)))!;
        var freeType = (await AsAsync(s => s.Get<QaSpecDirectory>().GetTypeAsync(free)))!;
        var refused = await AsAsync(s => s.Get<QaSpecRegistry>().DeleteTypeAsync(used, usedType.RowVersion));
        var deleted = await AsAsync(s => s.Get<QaSpecRegistry>().DeleteTypeAsync(free, freeType.RowVersion));

        Assert.False(refused.Succeeded);
        Assert.Contains("สเปครายรุ่นใช้อยู่", refused.Errors[0]);
        Assert.True(deleted.Succeeded);
        Assert.Null(await AsAsync(s => s.Get<QaSpecDirectory>().GetTypeAsync(free)));
        Assert.Equal(1, usedType.Specs);
    }

    // ---------- สเปครายรุ่น ----------

    [Fact] // points keep the text as typed and their order; blank rows are dropped and the rest numbered again
    public async Task A_spec_keeps_its_points_and_defects_in_order()
    {
        var type = await NewTypeAsync();
        var points = new QaMeasurePointInput[]
        {
            new("1", "ความหนา", "5.0 ± 0.2"), new(null, null, null), new("2", "ความกว้าง", "มากกว่า 20.5"), new("3", null, "10.0–10.4"),
        };

        var saved = await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(type, "รุ่นสมมุติ", "AB-1, AB-2", points, ["รอยแตก", " ", "ครีบ"])));

        Assert.True(saved.Succeeded, string.Join(" ", saved.Errors));
        var spec = (await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecAsync(saved.Id!.Value)))!;
        Assert.Equal(("รุ่นสมมุติ", "AB-1, AB-2", 3), (spec.Name, spec.Models, spec.DocumentSequence));
        Assert.Equal([1, 2, 3], spec.Points.Select(p => p.Sequence));
        Assert.Equal(["5.0 ± 0.2", "มากกว่า 20.5", "10.0–10.4"], spec.Points.Select(p => p.Standard));
        Assert.Equal(["รอยแตก", "ครีบ"], spec.Defects);
    }

    [Fact] // editing replaces the points; the old rowversion is refused
    public async Task Editing_a_spec_replaces_its_points_and_checks_the_rowversion()
    {
        var type = await NewTypeAsync();
        var id = (await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(type, "แก้ได้", points: [new("1", "เดิม", "1")], defects: ["เดิม"])))).Id!.Value;
        var first = (await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecAsync(id)))!;

        var edited = await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(type, "แก้ได้", points: [new("9", "ใหม่", "9"), new("8", "ใหม่สอง", "8")], defects: [], rowVersion: first.RowVersion, id: id)));

        Assert.True(edited.Succeeded, string.Join(" ", edited.Errors));
        var second = (await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecAsync(id)))!;
        Assert.Equal(["ใหม่", "ใหม่สอง"], second.Points.Select(p => p.Description));
        Assert.Empty(second.Defects);
        await Assert.ThrowsAsync<Lockte.SharedKernel.Persistence.ConcurrencyConflictException>(() => AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(type, "แก้ได้", rowVersion: first.RowVersion, id: id))));
    }

    [Fact] // a name is unique within its document type, not across types
    public async Task A_spec_name_is_unique_within_its_document_type()
    {
        var one = await NewTypeAsync();
        var two = await NewTypeAsync();
        await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(one, "ชื่อเดียวกัน")));

        var same = await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(one, "ชื่อเดียวกัน")));
        var other = await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(two, "ชื่อเดียวกัน")));

        Assert.False(same.Succeeded);
        Assert.True(other.Succeeded);
    }

    [Fact] // the picture is kept, replaced and removed; one too large is refused
    public async Task The_picture_of_the_piece_is_kept_replaced_and_removed()
    {
        var type = await NewTypeAsync();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
        var id = (await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(type, "มีรูป") with { Image = png, ImageContentType = "image/png" }))).Id!.Value;

        var image = await AsAsync(s => s.Get<QaSpecDirectory>().GetImageAsync(id));
        Assert.Equal((png, "image/png"), image);
        Assert.True((await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecAsync(id)))!.HasImage);

        var keep = (await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecAsync(id)))!;
        await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(type, "มีรูป", rowVersion: keep.RowVersion, id: id)));
        Assert.NotNull(await AsAsync(s => s.Get<QaSpecDirectory>().GetImageAsync(id))); // saved without a picture: the old one stays

        var current = (await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecAsync(id)))!;
        await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(type, "มีรูป", rowVersion: current.RowVersion, id: id) with { RemoveImage = true }));
        Assert.Null(await AsAsync(s => s.Get<QaSpecDirectory>().GetImageAsync(id)));

        var tooLarge = await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(type, "รูปใหญ่") with { Image = new byte[QaSpecRegistry.MaxImageBytes + 1] }));
        Assert.False(tooLarge.Succeeded);
    }

    [Fact] // the specs that serve a model: by one of its models or by the name of the spec, case-insensitive
    public async Task Specs_are_found_by_model()
    {
        var type = await NewTypeAsync();
        var model = Unique("MD");
        var other = Unique("MX");
        await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(type, Unique("รวม"), $"{model}, {other}")));
        await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(type, Unique("เดี่ยว"), model.ToLowerInvariant())));
        await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(type, Unique("อื่น"), "ZZ")));

        var found = await AsAsync(s => s.Get<QaSpecDirectory>().FindByModelAsync(model));
        var both = await AsAsync(s => s.Get<QaSpecDirectory>().FindByModelAsync(other));

        Assert.Equal(2, found.Count);
        Assert.Single(both);
    }

    [Fact] // deleting a spec deletes its points and defects
    public async Task A_spec_can_be_deleted_with_its_points()
    {
        var type = await NewTypeAsync();
        var id = (await AsAsync(s => s.Get<QaSpecRegistry>().SaveSpecAsync(Spec(type, "ลบได้", points: [new("1", "x", "1")], defects: ["x"])))).Id!.Value;
        var spec = (await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecAsync(id)))!;

        var deleted = await AsAsync(s => s.Get<QaSpecRegistry>().DeleteSpecAsync(id, spec.RowVersion));

        Assert.True(deleted.Succeeded);
        Assert.Null(await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecAsync(id)));
        Assert.DoesNotContain(await AsAsync(s => s.Get<QaSpecDirectory>().GetSpecsAsync(type)), x => x.Id == id);
    }
}

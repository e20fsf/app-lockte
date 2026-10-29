using Lockte.Org;
using Lockte.Purch;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>แก้ไข / ลบรายการ / ค้นใบขอซื้อ (#219, SCR-06, SCR-08, BR-012, BR-013, AC-35).</summary>
[Collection(LocalDbCollection.Name)]
public sealed class PurchaseRequestEditTests(LocalDbFixture db)
{
    private static readonly DateOnly Oct2026 = new(2026, 10, 29);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsBuyerAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    private async Task<int> DepartmentAsync()
    {
        await using var scope = db.Scope("hr1");
        var org = scope.Get<OrgRegistry>();
        var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
        return (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
    }

    private async Task<int> MaterialAsync(decimal cost, string name)
        => (await AsBuyerAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("ed").ToUpperInvariant(), name, null, "กก.", Cost: cost)))).Id!.Value;

    /// <summary>A request of three items (O69-xxxx style number), returned as read back.</summary>
    private async Task<(int Department, PurchaseRequestDto Request, int[] Materials)> NewRequestAsync()
    {
        var department = await DepartmentAsync();
        var materials = new[] { await MaterialAsync(10, "รายการหนึ่ง"), await MaterialAsync(20, "รายการสอง"), await MaterialAsync(30, "รายการสาม") };
        var no = Unique("E") + "69-0040";
        var saved = await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput(no, Oct2026, department, null, "F", null, null,
            materials.Select(m => new PurchaseRequestLineInput(m, 5)).ToList())));
        Assert.True(saved.Succeeded);
        return (department, (await AsBuyerAsync(s => s.Get<PurchaseRequestDirectory>().FindByNoAsync(no)))!, materials);
    }

    private Task<PurchaseRequestDto> ReadAsync(int id) => AsBuyerAsync(async s => (await s.Get<PurchaseRequestDirectory>().GetAsync(id))!);

    private static PurchaseRequestUpdate Update(PurchaseRequestDto r, IReadOnlyList<PurchaseRequestLineUpdate>? lines = null, string? no = null, string? reason = null)
        => new(r.Id, r.RowVersion, no ?? r.RequestNo, r.RequestDate, r.DepartmentId, r.DesiredDate, r.PurchasingLocation ?? "F", reason ?? r.Reason, r.Note,
            lines ?? r.Lines.Select(l => new PurchaseRequestLineUpdate(l.Id, l.RowVersion, l.MaterialId, l.Quantity, l.LineNo, l.MaterialName, l.Unit, l.UnitPrice, l.TankWeight)).ToList());

    [Fact]
    public async Task A_request_is_found_by_number_and_every_field_can_be_edited_with_history()
    {
        var (_, request, materials) = await NewRequestAsync();
        Assert.Contains(await AsBuyerAsync(s => s.Get<PurchaseRequestDirectory>().SearchAsync(request.RequestNo[1..])), r => r.RequestNo == request.RequestNo);

        var first = request.Lines[0];
        var lines = request.Lines.Select(l => new PurchaseRequestLineUpdate(l.Id, l.RowVersion, l.MaterialId, l == first ? 8 : l.Quantity, l.LineNo, l == first ? "ชื่อที่แก้ในใบ" : l.MaterialName, l.Unit, l.UnitPrice, l.TankWeight)).ToList();
        Assert.True((await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().UpdateAsync(Update(request, lines, reason: "ซ่อมเครื่อง")))).Succeeded);

        var edited = await ReadAsync(request.Id);
        Assert.Equal("ซ่อมเครื่อง", edited.Reason);
        var line = edited.Lines.Single(l => l.Id == first.Id);
        Assert.Equal((8m, "ชื่อที่แก้ในใบ", 80m), (line.Quantity, line.MaterialName, line.NetAmount));

        await using var scope = db.Scope();
        var rows = await scope.Get<HistoryService>().SearchAsync(new(Table: "รายการใบขอซื้อ", RowKey: request.RequestNo));
        Assert.Contains(rows, r => r is { Action: HistoryActions.Modified, Column: "จำนวน", UserName: "buyer1" } && decimal.Parse(r.OldValue!, System.Globalization.CultureInfo.InvariantCulture) == 5m && decimal.Parse(r.NewValue!, System.Globalization.CultureInfo.InvariantCulture) == 8m);
        Assert.Equal(3, materials.Length);
    }

    [Fact]
    public async Task Changing_the_material_refills_its_name_unit_and_price_and_a_kept_line_needs_a_quantity()
    {
        var (_, request, _) = await NewRequestAsync();
        var other = await MaterialAsync(77, "วัตถุดิบใหม่");
        var target = request.Lines[1];
        var lines = request.Lines.Select(l => l.Id == target.Id
            ? new PurchaseRequestLineUpdate(l.Id, l.RowVersion, other, l.Quantity, l.LineNo) : new PurchaseRequestLineUpdate(l.Id, l.RowVersion, l.MaterialId, l.Quantity, l.LineNo, l.MaterialName, l.Unit, l.UnitPrice)).ToList();
        Assert.True((await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().UpdateAsync(Update(request, lines)))).Succeeded);
        var changed = (await ReadAsync(request.Id)).Lines.Single(l => l.Id == target.Id);
        Assert.Equal(("วัตถุดิบใหม่", 77m, 385m), (changed.MaterialName, changed.UnitPrice, changed.NetAmount));

        var fresh = await ReadAsync(request.Id);
        var zero = fresh.Lines.Select(l => new PurchaseRequestLineUpdate(l.Id, l.RowVersion, l.MaterialId, l.Id == target.Id ? 0 : l.Quantity)).ToList();
        var refused = await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().UpdateAsync(Update(fresh, zero)));
        Assert.Contains("จำนวนของรายการต้องมากกว่า 0 (ถ้าไม่ต้องการรายการนี้ ใช้ปุ่มลบรายการ)", refused.Errors);
    }

    [Fact]
    public async Task A_new_line_can_be_added_and_a_duplicate_number_is_refused_and_a_stale_edit_conflicts()
    {
        var (_, request, materials) = await NewRequestAsync();
        var (_, other, _) = await NewRequestAsync();
        var plus = request.Lines.Select(l => new PurchaseRequestLineUpdate(l.Id, l.RowVersion, l.MaterialId, l.Quantity, l.LineNo, l.MaterialName, l.Unit, l.UnitPrice))
            .Append(new PurchaseRequestLineUpdate(null, null, materials[0], 2)).Append(new PurchaseRequestLineUpdate(null, null, materials[1], 0)).ToList();
        Assert.True((await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().UpdateAsync(Update(request, plus)))).Succeeded);
        Assert.Equal(4, (await ReadAsync(request.Id)).Lines.Count); // the new line without quantity was dropped

        var fresh = await ReadAsync(request.Id);
        var duplicate = await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().UpdateAsync(Update(fresh, no: other.RequestNo)));
        Assert.Equal([$"มีเลขที่ใบขอซื้อ '{other.RequestNo}' อยู่แล้ว"], duplicate.Errors);

        Assert.True((await AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().UpdateAsync(Update(fresh, reason: "ครั้งแรก")))).Succeeded);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => AsBuyerAsync(s => s.Get<PurchaseRequestRegistry>().UpdateAsync(Update(fresh, reason: "จากหน้าเก่า"))));
    }

    [Fact]
    public async Task Deleting_items_keeps_the_number_in_use_until_the_last_one_AC_35()
    {
        var (_, request, _) = await NewRequestAsync();
        var registry = (TestScope s) => s.Get<PurchaseRequestRegistry>();

        var first = request.Lines[0];
        Assert.True((await AsBuyerAsync(s => registry(s).DeleteLineAsync(first.Id, first.RowVersion))).Succeeded);
        var after = await ReadAsync(request.Id);
        Assert.Equal(2, after.Lines.Count);
        Assert.NotNull(await AsBuyerAsync(s => s.Get<PurchaseRequestDirectory>().FindByNoAsync(request.RequestNo)));
        Assert.False((await AsBuyerAsync(s => registry(s).SaveAsync(new PurchaseRequestInput(request.RequestNo, Oct2026, request.DepartmentId, null, "F", null, null,
            [new PurchaseRequestLineInput(after.Lines[0].MaterialId, 1)])))).Succeeded); // the number is still taken

        foreach (var line in after.Lines)
            Assert.True((await AsBuyerAsync(s => registry(s).DeleteLineAsync(line.Id, line.RowVersion))).Succeeded);
        Assert.Null(await AsBuyerAsync(s => s.Get<PurchaseRequestDirectory>().FindByNoAsync(request.RequestNo)));
        Assert.True((await AsBuyerAsync(s => registry(s).SaveAsync(new PurchaseRequestInput(request.RequestNo, Oct2026, request.DepartmentId, null, "F", null, null,
            [new PurchaseRequestLineInput(first.MaterialId, 1)])))).Succeeded); // the number is free again
        Assert.True((await AsBuyerAsync(s => registry(s).DeleteLineAsync(first.Id, first.RowVersion))).Succeeded); // already gone: fine
    }
}

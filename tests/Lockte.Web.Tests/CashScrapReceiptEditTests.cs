using Lockte.Cash;
using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;

namespace Lockte.Web.Tests;

/// <summary>แก้ไข พิมพ์ซ้ำ และยกเลิกใบเสร็จขายของเก่า (#239, SCR-10, SCR-11, BR-015, BR-017). Each test has a year code of its own.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CashScrapReceiptEditTests(LocalDbFixture db)
{
    private static DateOnly DateInYy(int yy) => CashScrapReceiptMathTests.DateInYy(yy);

    private async Task<ScrapReceiptDto> NewAsync(int yy, string? no = null, IReadOnlyList<ScrapReceiptLineInput>? lines = null)
    {
        await using var scope = db.Scope("qa1");
        var today = DateInYy(yy);
        var result = await scope.Get<ScrapReceiptRegistry>().SaveAsync(new(null, no, today, "คุณตัวอย่าง ข", "ที่อยู่ตัวอย่าง", "000-000-0000", null,
            lines ?? [new(1, "เศษเหล็ก", null, "กก.", 1, 400, null), new(2, "ฝุ่น", null, "กก.", 1, 600, null)], null, today));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return (await scope.Get<ScrapReceiptDirectory>().GetAsync(result.Id!.Value))!;
    }

    private async Task<ScrapReceiptDto> GetAsync(int id)
    {
        await using var scope = db.Scope();
        return (await scope.Get<ScrapReceiptDirectory>().GetAsync(id))!;
    }

    private static ScrapReceiptInput Edit(ScrapReceiptDto r, IReadOnlyList<ScrapReceiptLineInput> lines, DateOnly? date = null, string? address = null)
        => new(r.Id, r.No, date ?? r.Date, r.BuyerName, address ?? r.BuyerAddress, r.BuyerTel, r.ScrapBuyerId, lines, r.RowVersion);

    private static ScrapReceiptLineInput Same(ScrapReceiptLineDto l, decimal? price = null)
        => new(l.Seq, l.ItemName, l.ScrapItemId, l.Unit, l.Qty, price ?? l.UnitPrice, null, l.Id);

    [Fact] // AC-14: the price of a line is edited and the receipt is printed again with the new total
    public async Task AC14_editing_a_price_gives_the_new_total_and_leaves_the_number()
    {
        var receipt = await NewAsync(31, "31028");
        Assert.Equal(1000m, receipt.Total);

        await using (var scope = db.Scope("qa2"))
        {
            var result = await scope.Get<ScrapReceiptRegistry>().SaveAsync(Edit(receipt, [Same(receipt.Lines[0]), Same(receipt.Lines[1], price: 700)]));
            Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        }

        var edited = await GetAsync(receipt.Id);
        Assert.Equal((1100m, "31028"), (edited.Total, edited.No));
        Assert.Equal("(หนึ่งพันหนึ่งร้อยบาทถ้วน)", edited.TotalInWords);
        Assert.Equal([receipt.Lines[0].Id, receipt.Lines[1].Id], edited.Lines.Select(l => l.Id)); // the same lines, not new ones
    }

    [Fact]
    public async Task An_edit_changes_the_head_and_the_lines_and_records_old_and_new_in_history()
    {
        var receipt = await NewAsync(32, "32001");
        var added = new ScrapReceiptLineInput(3, "น้ำมันเก่า", null, "ถัง", 2, 50, null);
        await using (var scope = db.Scope("qa2"))
            Assert.True((await scope.Get<ScrapReceiptRegistry>().SaveAsync(Edit(receipt, [Same(receipt.Lines[0]), added], DateInYy(32).AddDays(1), "ที่อยู่ใหม่"))).Succeeded);

        var edited = await GetAsync(receipt.Id);
        Assert.Equal(("ที่อยู่ใหม่", DateInYy(32).AddDays(1)), (edited.BuyerAddress, edited.Date));
        Assert.Equal(["เศษเหล็ก", "น้ำมันเก่า"], edited.Lines.Select(l => l.ItemName)); // the second line is gone (left out), a third is added
        Assert.Equal(100m, edited.Lines[1].Amount);

        await using var read = db.Scope();
        var history = read.Get<HistoryService>();
        var head = await history.SearchAsync(new(Table: "ใบเสร็จขายของเก่า", RowKey: "32001"));
        var address = head.Single(r => r.Action == HistoryActions.Modified && r.Column == "ที่อยู่" && r.UserName == "qa2");
        Assert.Equal(("ที่อยู่ตัวอย่าง", "ที่อยู่ใหม่"), (address.OldValue, address.NewValue));
        Assert.Contains(await history.SearchAsync(new(Table: "บรรทัดใบเสร็จขายของเก่า", RowKey: "32001 ลำดับ 2")), r => r.Action == HistoryActions.Deleted && r.UserName == "qa2");
        Assert.Contains(await history.SearchAsync(new(Table: "บรรทัดใบเสร็จขายของเก่า", RowKey: "32001 ลำดับ 3")), r => r.Action == HistoryActions.Added && r.UserName == "qa2");
    }

    [Fact]
    public async Task The_number_cannot_be_changed_and_a_repeated_sequence_is_refused()
    {
        var receipt = await NewAsync(33, "33001");
        await using var scope = db.Scope();
        var registry = scope.Get<ScrapReceiptRegistry>();
        var renumbered = await registry.SaveAsync(Edit(receipt, [Same(receipt.Lines[0])]) with { No = "33002" });
        Assert.False(renumbered.Succeeded);
        Assert.Contains("เลขที่", renumbered.Errors.Single());
        var repeated = await registry.SaveAsync(Edit(receipt, [Same(receipt.Lines[0]), Same(receipt.Lines[1]) with { Seq = 1 }]));
        Assert.False(repeated.Succeeded);
        Assert.Contains("ซ้ำ", repeated.Errors.Single());
        Assert.Equal(2, (await GetAsync(receipt.Id)).Lines.Count);
    }

    [Fact]
    public async Task Two_people_editing_the_same_receipt_are_told()
    {
        var receipt = await NewAsync(34, "34001");
        await using (var scope = db.Scope("qa2"))
            Assert.True((await scope.Get<ScrapReceiptRegistry>().SaveAsync(Edit(receipt, [Same(receipt.Lines[0], price: 410), Same(receipt.Lines[1])]))).Succeeded);
        await using (var scope = db.Scope("qa3")) // the lines changed, not the head: the head's version moves all the same
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => scope.Get<ScrapReceiptRegistry>().SaveAsync(Edit(receipt, [Same(receipt.Lines[0], price: 420), Same(receipt.Lines[1])])));
    }

    [Fact] // BR-017, assumption ง: the receipt stays, marked with the reason, who and when; its number stays used
    public async Task Cancelling_keeps_the_receipt_and_its_number_and_stops_edit_and_print()
    {
        var receipt = await NewAsync(35, "35007");
        await using (var scope = db.Scope("qa4"))
            Assert.True((await scope.Get<ScrapReceiptRegistry>().CancelAsync(receipt.Id, "ออกผิดคน", receipt.RowVersion)).Succeeded);

        var cancelled = await GetAsync(receipt.Id);
        Assert.True(cancelled.IsCancelled);
        Assert.Equal(("ออกผิดคน", "qa4"), (cancelled.CancelReason, cancelled.CancelledBy));
        Assert.NotNull(cancelled.CancelledAt);
        Assert.Equal(2, cancelled.Lines.Count); // nothing deleted

        await using var scope2 = db.Scope();
        Assert.Equal("35008", await scope2.Get<ScrapReceiptDirectory>().NextNumberAsync(DateInYy(35))); // the number is not given again
        Assert.Contains(await scope2.Get<ScrapReceiptDirectory>().ListNumbersAsync(), h => h.No == "35007" && h.IsCancelled);
        var again = await scope2.Get<ScrapReceiptRegistry>().SaveAsync(new(null, "35007", DateInYy(35), "ก", null, null, null, [new(1, "ก", null, null, 1, 1, null)], null, DateInYy(35)));
        Assert.False(again.Succeeded); // out and used: not issued again
        var edit = await scope2.Get<ScrapReceiptRegistry>().SaveAsync(Edit(cancelled, [Same(cancelled.Lines[0])]));
        Assert.False(edit.Succeeded);
        Assert.Contains("ยกเลิก", edit.Errors.Single());
    }

    [Fact]
    public async Task Cancelling_needs_a_reason_and_a_receipt_cancelled_already_is_refused_and_recorded_in_history()
    {
        var receipt = await NewAsync(36, "36001");
        await using var scope = db.Scope("qa5");
        var registry = scope.Get<ScrapReceiptRegistry>();
        Assert.False((await registry.CancelAsync(receipt.Id, "  ", receipt.RowVersion)).Succeeded);
        Assert.True((await registry.CancelAsync(receipt.Id, "ออกซ้ำ", receipt.RowVersion)).Succeeded);

        await using var scope2 = db.Scope("qa5");
        var current = await GetAsync(receipt.Id);
        var second = await scope2.Get<ScrapReceiptRegistry>().CancelAsync(receipt.Id, "อีกครั้ง", current.RowVersion);
        Assert.False(second.Succeeded);
        Assert.Contains("ยกเลิกไปแล้ว", second.Errors.Single());

        var rows = await scope2.Get<HistoryService>().SearchAsync(new(Table: "ใบเสร็จขายของเก่า", RowKey: "36001"));
        var change = rows.Single(r => r.Action == HistoryActions.Modified && r.Column == "ยกเลิก");
        Assert.Equal(("ไม่ใช่", "ใช่"), (change.OldValue, change.NewValue));
        Assert.Single(rows, r => r.Column == "เหตุผลที่ยกเลิก" && r.NewValue == "ออกซ้ำ");
    }
}

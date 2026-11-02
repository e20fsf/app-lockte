using System.Globalization;
using Lockte.Cash.Data;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Cash;

public sealed record ScrapReceiptLineDto(int Id, int Seq, string ItemName, int? ScrapItemId, string? Unit, decimal Qty, decimal UnitPrice, decimal Amount);

/// <summary>A receipt as the screens and the print read it; <see cref="Total"/> is the amount printed (RND-A) and <see cref="TotalInWords"/> its words (RND-C).</summary>
public sealed record ScrapReceiptDto(int Id, string No, DateOnly Date, string BuyerName, string? BuyerAddress, string? BuyerTel, int? ScrapBuyerId, bool IsCancelled,
    string? CancelReason, string? CancelledBy, DateTimeOffset? CancelledAt, IReadOnlyList<ScrapReceiptLineDto> Lines, byte[] RowVersion, int? RemittanceId = null, DateOnly? RemittedOn = null)
{
    /// <summary>Whether the receipt has been closed into a การนำส่ง (ตัดยอดนำส่งแล้ว): it can still be edited, but not cancelled.</summary>
    public bool IsRemitted => RemittanceId is not null;

    public decimal Total => ScrapReceiptMath.Total(Lines.Select(l => l.Amount));

    public string TotalInWords => ScrapReceiptMath.InWords(Total);
}

/// <summary>A receipt in the list of numbers (SCR-10): no lines.</summary>
public sealed record ScrapReceiptHeadDto(int Id, string No, DateOnly Date, string BuyerName, bool IsCancelled);

/// <summary>
/// A line on the screen; <see cref="Seq"/> left out is the next number, an item name left out skips the line (BR-012 #2). <see cref="Amount"/> left out is quantity × price. <see cref="Id"/> is
/// the line being edited; when a receipt is edited a saved line left out, or left without an item, is removed.
/// </summary>
public sealed record ScrapReceiptLineInput(int? Seq, string? ItemName, int? ScrapItemId, string? Unit, decimal? Qty, decimal? UnitPrice, decimal? Amount, int? Id = null);

/// <summary>
/// A receipt on the screen. <see cref="Id"/> null = a new one; <see cref="No"/> left blank is the next number, one that is typed must be digits, at least 5, and unused. <see cref="Today"/> is
/// the date the number's year comes from (the day the receipt is made, not the date written on it).
/// </summary>
public sealed record ScrapReceiptInput(int? Id, string? No, DateOnly? Date, string? BuyerName, string? BuyerAddress, string? BuyerTel, int? ScrapBuyerId,
    IReadOnlyList<ScrapReceiptLineInput> Lines, byte[]? RowVersion, DateOnly? Today = null);

/// <summary>A line that passed the checks, ready to be saved.</summary>
public sealed record ScrapReceiptReadyLine(int Seq, string ItemName, int? ScrapItemId, string? Unit, decimal Qty, decimal UnitPrice, decimal Amount, int? Id = null);

/// <summary>What saving a receipt did. A number that was taken meanwhile is not an exception: <see cref="NextNo"/> is the number to try instead.</summary>
public sealed record ScrapReceiptSaveResult(bool Succeeded, IReadOnlyList<string> Errors, int? Id = null, string? No = null, string? NextNo = null);

/// <summary>The numbering and the money of a receipt (BR-011, BR-012, RND-A, RND-C), without a database.</summary>
public static class ScrapReceiptMath
{
    public const int MinNumberLength = 5;
    public const int MaxNumberLength = 10;

    /// <summary>
    /// <c>YY</c>: the last two digits of the Buddhist year of the date, whatever the regional setting of the machine (the legacy read it from the setting, so a machine on the Gregorian
    /// calendar gave <c>26001</c>, Q-13).
    /// </summary>
    public static string YearCode(DateOnly date) => ((date.Year + 543) % 100).ToString("00", CultureInfo.InvariantCulture);

    /// <summary>
    /// The next number: the highest running number of this year's <c>YY</c> among <paramref name="used"/>, compared as a number, plus one, in at least three digits; a new year starts at 001.
    /// 69029 → 69030, 69009 → 69010, 69999 → 691000 (no clash — nothing is glued to it any more).
    /// </summary>
    public static string Next(IEnumerable<string> used, DateOnly today)
    {
        var yy = YearCode(today);
        var highest = used.Where(n => n.Length > 2 && n.StartsWith(yy, StringComparison.Ordinal) && n.All(char.IsAsciiDigit))
            .Select(n => long.Parse(n[2..], NumberStyles.None, CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max();
        return yy + (highest + 1).ToString("000", CultureInfo.InvariantCulture);
    }

    /// <summary>Whether a number typed by hand can be used: digits only, 5 to 10 of them.</summary>
    public static bool IsValidNumber(string? no) => no is { Length: >= MinNumberLength and <= MaxNumberLength } && no.All(char.IsAsciiDigit);

    /// <summary>The order of numbers: by year code, then by the running number as a number (so 691000 follows 69999, which text order would not give).</summary>
    public static (string Year, long Seq) SortKey(string no) => no.Length > 2 && no.All(char.IsAsciiDigit) ? (no[..2], long.Parse(no[2..], NumberStyles.None, CultureInfo.InvariantCulture)) : (no, 0);

    /// <summary>The order of two numbers: year code, then the running number as a number. Negative when <paramref name="a"/> comes first.</summary>
    public static int Compare(string a, string b) => Comparer<(string, long)>.Create((x, y) => x.Item1 != y.Item1 ? string.CompareOrdinal(x.Item1, y.Item1) : x.Item2.CompareTo(y.Item2)).Compare(SortKey(a), SortKey(b));

    /// <summary>
    /// RND-A of this program (<c>CutDecimal(x) = ((x*100) \ 1) / 100</c>): the sum to two decimals by banker's rounding — 100.125 → 100.12, 100.135 → 100.14. It is not the RND-A of the
    /// employee program (whole numbers, half up).
    /// </summary>
    public static decimal Total(IEnumerable<decimal> amounts) => Math.Round(amounts.Sum(), 2, MidpointRounding.ToEven);

    /// <summary>"(สองพันสองร้อยเจ็ดสิบบาทถ้วน)" — RND-C, in brackets.</summary>
    public static string InWords(decimal total) => $"({ThaiNumber.Baht((double)total)})";
}

/// <summary>The public, read-only face of ใบเสร็จขายของเก่า (ADR 0002): SCR-09–SCR-12, the print RPT-06 and the reports RPT-07–RPT-09 read it.</summary>
public sealed class ScrapReceiptDirectory(CashDbContext db)
{
    /// <summary>The number the next receipt gets, for the year of <paramref name="today"/>.</summary>
    public async Task<string> NextNumberAsync(DateOnly today)
    {
        var yy = ScrapReceiptMath.YearCode(today);
        return ScrapReceiptMath.Next(await db.ScrapReceipts.AsNoTracking().Where(r => r.No.StartsWith(yy)).Select(r => r.No).ToListAsync(), today);
    }

    public async Task<ScrapReceiptDto?> GetAsync(int id) => (await ReadAsync(db.ScrapReceipts.AsNoTracking().Where(r => r.Id == id))).FirstOrDefault();

    public async Task<ScrapReceiptDto?> GetByNoAsync(string no) => (await ReadAsync(db.ScrapReceipts.AsNoTracking().Where(r => r.No == no))).FirstOrDefault();

    /// <summary>The receipts dated from <paramref name="from"/> to <paramref name="to"/> inclusive, cancelled ones too, by number.</summary>
    public async Task<IReadOnlyList<ScrapReceiptDto>> ListAsync(DateOnly from, DateOnly to)
        => (await ReadAsync(db.ScrapReceipts.AsNoTracking().Where(r => r.Date >= from && r.Date <= to))).OrderBy(r => ScrapReceiptMath.SortKey(r.No)).ToList();

    /// <summary>Every number (SCR-10), by number, cancelled ones marked.</summary>
    public async Task<IReadOnlyList<ScrapReceiptHeadDto>> ListNumbersAsync()
        => (await db.ScrapReceipts.AsNoTracking().Select(r => new ScrapReceiptHeadDto(r.Id, r.No, r.Date, r.BuyerName, r.IsCancelled)).ToListAsync()).OrderBy(r => ScrapReceiptMath.SortKey(r.No)).ToList();

    internal async Task<IReadOnlyList<ScrapReceiptDto>> ReadAsync(IQueryable<ScrapReceipt> receipts)
    {
        var heads = await receipts.ToListAsync();
        var remittanceIds = heads.Where(h => h.RemittanceId is not null).Select(h => h.RemittanceId!.Value).Distinct().ToList();
        var remitted = await db.ScrapRemittances.AsNoTracking().Where(r => remittanceIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.MadeOn);
        var ids = heads.Select(h => h.Id).ToList();
        var lines = (await db.ScrapReceiptLines.AsNoTracking().Where(l => ids.Contains(l.ReceiptId)).ToListAsync()).ToLookup(l => l.ReceiptId);
        return heads.Select(h => new ScrapReceiptDto(h.Id, h.No, h.Date, h.BuyerName, h.BuyerAddress, h.BuyerTel, h.ScrapBuyerId, h.IsCancelled, h.CancelReason, h.CancelledBy, h.CancelledAt,
            lines[h.Id].OrderBy(l => l.Seq).Select(l => new ScrapReceiptLineDto(l.Id, l.Seq, l.ItemName, l.ScrapItemId, l.Unit, l.Qty, l.UnitPrice, l.Amount)).ToList(), h.RowVersion,
            h.RemittanceId, h.RemittanceId is { } rid && remitted.TryGetValue(rid, out var madeOn) ? madeOn : null)).ToList();
    }
}

/// <summary>
/// ออกเลขและบันทึกใบเสร็จขายของเก่า (SCR-09, BR-011–BR-014). The head and the lines are saved in one go and the number is taken when they are: two people who were offered the same number
/// do not both get it — the second is told and offered the next. Leaving the screen without printing saves nothing, so no number is used up and no half receipt is left (BR-014).
/// Lines without an item are skipped; a repeated ลำดับที่ is refused with a message instead of one line being dropped silently (BR-013).
/// </summary>
public sealed class ScrapReceiptRegistry(CashDbContext db, ICurrentUser user, TimeProvider clock)
{
    public const int BuyerNameLength = ScrapBuyerRegistry.NameLength;
    public const int AddressLength = ScrapBuyerRegistry.AddressLength;
    public const int TelLength = ScrapBuyerRegistry.TelLength;
    public const int ItemNameLength = ScrapItemRegistry.NameLength;
    public const int UnitLength = ScrapItemRegistry.UnitLength;
    public const int CancelReasonLength = 500;
    private const int Attempts = 5;

    public async Task<ScrapReceiptSaveResult> SaveAsync(ScrapReceiptInput input)
    {
        var errors = new List<string>();
        var name = CashText.Clean(input.BuyerName);
        var address = CashText.Clean(input.BuyerAddress);
        var tel = CashText.Clean(input.BuyerTel);
        if (input.Date is null) errors.Add("กรุณาเลือกวันที่");
        if (name?.Length > BuyerNameLength) errors.Add($"ชื่อผู้ซื้อยาวได้ไม่เกิน {BuyerNameLength} ตัว");
        if (address?.Length > AddressLength) errors.Add($"ที่อยู่ยาวได้ไม่เกิน {AddressLength} ตัว");
        if (tel?.Length > TelLength) errors.Add($"เบอร์โทรศัพท์ยาวได้ไม่เกิน {TelLength} ตัว");
        var typed = string.IsNullOrWhiteSpace(input.No) ? null : input.No.Trim();
        if (typed is not null && !ScrapReceiptMath.IsValidNumber(typed)) errors.Add($"เลขที่ใบเสร็จต้องเป็นตัวเลข {ScrapReceiptMath.MinNumberLength}–{ScrapReceiptMath.MaxNumberLength} หลัก");

        var lines = BuildLines(input.Lines, errors);
        if (errors.Count > 0) return new ScrapReceiptSaveResult(false, errors);
        if (input.Id is { } editing) return await UpdateAsync(editing, input, name, address, tel, typed, lines);
        var today = input.Today ?? DateOnly.FromDateTime(DateTime.Now);

        for (var attempt = 1; ; attempt++)
        {
            var no = typed ?? await new ScrapReceiptDirectory(db).NextNumberAsync(today);
            if (await db.ScrapReceipts.AnyAsync(r => r.No == no)) return await TakenAsync(no, typed is not null, today, attempt);
            var receipt = new ScrapReceipt { No = no, Date = input.Date!.Value, BuyerName = name ?? "", BuyerAddress = address, BuyerTel = tel, ScrapBuyerId = input.ScrapBuyerId };
            receipt.Lines.AddRange(lines.Select(l => new ScrapReceiptLine { Seq = l.Seq, ItemName = l.ItemName, ScrapItemId = l.ScrapItemId, Unit = l.Unit, Qty = l.Qty, UnitPrice = l.UnitPrice, Amount = l.Amount }));
            db.ScrapReceipts.Add(receipt);
            try
            {
                await db.SaveChangesAsync();
                return new ScrapReceiptSaveResult(true, [], receipt.Id, no);
            }
            catch (DbUpdateException)
            {
                if (!await TakenMeanwhileAsync(no)) throw;
                // someone saved this number between the check and the save: ask again (a typed number is refused, an offered one moves on)
                if (typed is not null || attempt >= Attempts) return await TakenAsync(no, typed is not null, today, attempt);
            }
        }
    }

    /// <summary>
    /// แก้ใบเสร็จ (SCR-10): the head and the lines by the rules of a new receipt; the number cannot change and a cancelled receipt cannot be edited. Saved on the button, with the old and
    /// the new value of every column in the history (the legacy saved when the row was left, with no trace). The row version of the head moves with every save, lines included, so two
    /// people editing the same receipt are told.
    /// </summary>
    private async Task<ScrapReceiptSaveResult> UpdateAsync(int id, ScrapReceiptInput input, string? name, string? address, string? tel, string? typedNo, List<ScrapReceiptReadyLine> lines)
    {
        var receipt = await db.ScrapReceipts.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id);
        if (receipt is null) return new ScrapReceiptSaveResult(false, ["ไม่พบใบเสร็จนี้แล้ว"]);
        receipt.ExpectRowVersion(input.RowVersion ?? throw new ArgumentException("Editing needs the rowversion the screen loaded.", nameof(input)));
        if (receipt.IsCancelled) return new ScrapReceiptSaveResult(false, [$"ใบเสร็จเลขที่ {receipt.No} ถูกยกเลิกแล้ว แก้ไขไม่ได้"]);
        if (typedNo is not null && typedNo != receipt.No) return new ScrapReceiptSaveResult(false, ["แก้เลขที่ใบเสร็จไม่ได้"]);

        receipt.Date = input.Date!.Value;
        receipt.BuyerName = name ?? "";
        receipt.BuyerAddress = address;
        receipt.BuyerTel = tel;
        receipt.ScrapBuyerId = input.ScrapBuyerId;
        var saved = receipt.Lines.ToDictionary(l => l.Id);
        var kept = new HashSet<int>();
        foreach (var line in lines)
        {
            if (line.Id is { } lineId && saved.TryGetValue(lineId, out var row)) kept.Add(lineId);
            else receipt.Lines.Add(row = new ScrapReceiptLine());
            (row.Seq, row.ItemName, row.ScrapItemId, row.Unit, row.Qty, row.UnitPrice, row.Amount) = (line.Seq, line.ItemName, line.ScrapItemId, line.Unit, line.Qty, line.UnitPrice, line.Amount);
        }
        foreach (var gone in receipt.Lines.Where(l => l.Id != 0 && !kept.Contains(l.Id)).ToList()) db.ScrapReceiptLines.Remove(gone);
        db.Entry(receipt).State = EntityState.Modified; // the version of the head moves whatever was edited
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return new ScrapReceiptSaveResult(false, ["บันทึกไม่สำเร็จ ลำดับที่ของบรรทัดชนกัน — ลองแก้ลำดับทีละบรรทัด"]);
        }
        return new ScrapReceiptSaveResult(true, [], receipt.Id, receipt.No);
    }

    /// <summary>
    /// ยกเลิกใบเสร็จ (SCR-11, BR-017, assumption ง): the receipt stays with its number and lines, marked cancelled with the reason, who and when. Needs a reason; a receipt already
    /// cancelled is refused. A cancelled receipt cannot be edited or printed and is not counted in the unremitted amount or the sales reports.
    /// </summary>
    public async Task<ServiceResult> CancelAsync(int id, string? reason, byte[] loadedRowVersion)
    {
        var text = CashText.Clean(reason);
        if (text is null) return ServiceResult.Fail("กรุณาระบุเหตุผลที่ยกเลิกใบเสร็จ");
        if (text.Length > CancelReasonLength) return ServiceResult.Fail($"เหตุผลยาวได้ไม่เกิน {CancelReasonLength} ตัว");
        var receipt = await db.ScrapReceipts.FirstOrDefaultAsync(r => r.Id == id);
        if (receipt is null) return ServiceResult.Fail("ไม่พบใบเสร็จนี้แล้ว");
        receipt.ExpectRowVersion(loadedRowVersion);
        if (receipt.IsCancelled) return ServiceResult.Fail($"ใบเสร็จเลขที่ {receipt.No} ถูกยกเลิกไปแล้ว");
        if (receipt.RemittanceId is { } remittanceId)
        {
            var madeOn = await db.ScrapRemittances.AsNoTracking().Where(r => r.Id == remittanceId).Select(r => (DateOnly?)r.MadeOn).FirstOrDefaultAsync();
            return ServiceResult.Fail($"ใบเสร็จเลขที่ {receipt.No} นำส่งสำนักงานแล้ว{(madeOn is { } on ? $" (วันที่ {ThaiDate.Format(on)})" : "")} ยกเลิกไม่ได้");
        }
        receipt.IsCancelled = true;
        receipt.CancelReason = text;
        receipt.CancelledBy = user.UserName;
        receipt.CancelledAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        return ServiceResult.Ok(receipt.Id);
    }

    private async Task<ScrapReceiptSaveResult> TakenAsync(string no, bool typed, DateOnly today, int attempt)
    {
        var next = await new ScrapReceiptDirectory(db).NextNumberAsync(today);
        return new ScrapReceiptSaveResult(false, [typed || attempt == 1 ? $"เลขที่ {no} มีการใช้แล้ว (มีผู้ใช้อื่นพิมพ์ไปก่อน) — เลขถัดไปคือ {next}" : $"ออกเลขที่ใบเสร็จไม่สำเร็จ ลองใหม่อีกครั้ง (เลขถัดไป {next})"], No: no, NextNo: next);
    }

    private async Task<bool> TakenMeanwhileAsync(string no)
    {
        db.ChangeTracker.Clear();
        return await db.ScrapReceipts.AsNoTracking().AnyAsync(r => r.No == no);
    }

    /// <summary>The lines to save: those with an item (BR-012 #2), numbered, each checked; errors name the line.</summary>
    public static List<ScrapReceiptReadyLine> BuildLines(IReadOnlyList<ScrapReceiptLineInput> input, List<string> errors)
    {
        var result = new List<ScrapReceiptReadyLine>();
        var seen = new HashSet<int>();
        var next = 1;
        var row = 0;
        foreach (var line in input)
        {
            row++;
            var item = CashText.Clean(line.ItemName);
            if (item is null) continue;
            var label = $"บรรทัดที่ {row.ToString(CultureInfo.InvariantCulture)}";
            var unit = CashText.Clean(line.Unit);
            var seq = line.Seq ?? next;
            if (seq < 1) errors.Add($"{label}: ลำดับที่ต้องเป็น 1 ขึ้นไป");
            else if (!seen.Add(seq)) errors.Add($"{label}: ลำดับที่ {seq.ToString(CultureInfo.InvariantCulture)} ซ้ำกับบรรทัดอื่นในใบเดียวกัน");
            if (line.Qty is not > 0) errors.Add($"{label}: จำนวนต้องมากกว่า 0");
            if (item.Length > ItemNameLength) errors.Add($"{label}: ชื่อรายการยาวได้ไม่เกิน {ItemNameLength} ตัว");
            if (unit?.Length > UnitLength) errors.Add($"{label}: หน่วยยาวได้ไม่เกิน {UnitLength} ตัว");
            if (line.UnitPrice < 0 || line.Amount < 0) errors.Add($"{label}: ราคาและจำนวนเงินต้องไม่ติดลบ");
            next = Math.Max(next, seq) + 1;
            var price = line.UnitPrice ?? 0m;
            result.Add(new ScrapReceiptReadyLine(seq, item, line.ScrapItemId, unit, line.Qty ?? 0, price, line.Amount ?? ExpenseLineMath.AmountOf(line.Qty ?? 0, price) ?? 0m, line.Id));
        }
        if (result.Count == 0 && errors.Count == 0) errors.Add("กรุณากรอกรายการอย่างน้อยหนึ่งบรรทัด");
        return result;
    }
}

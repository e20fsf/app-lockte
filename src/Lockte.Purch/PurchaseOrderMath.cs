using Lockte.SharedKernel;

namespace Lockte.Purch;

/// <summary>ยอดของรายการหนึ่งบรรทัดในใบสั่งซื้อ: จำนวนเงินรายการ (NET_AMT) และยอดรายการรวม VAT (TAX, ใช้ใน RPT-11).</summary>
public sealed record PurchaseOrderLineAmount(decimal Net, decimal WithTax);

/// <summary>ยอดรวมของใบสั่งซื้อ (BR-022): ยอดก่อนภาษี, ภาษี (TA), ยอดรวม (TO) และจำนวนเงินตัวอักษร.</summary>
public sealed record PurchaseOrderTotals(
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    IReadOnlyList<PurchaseOrderLineAmount> Lines,
    string TotalInWords);

/// <summary>
/// การคำนวณยอดเงินและ VAT ของใบสั่งซื้อให้ตรงระบบเดิมทุกสตางค์ (BR-022, RND-A/B). The legacy did the sums in <see cref="double"/>,
/// so the same is done here and only the stored/printed results are <see cref="decimal"/>: a half-satang such as 136.425 goes
/// up or down by what the double product holds, exactly as before. Unlike the legacy there is no overflow above 21,474,836.47 baht
/// (BR-022 #5). A VAT choice other than NO — blank included — charges VAT.
/// </summary>
public static class PurchaseOrderMath
{
    /// <summary>
    /// RND-A — <c>CutDecimal(x)</c> = <c>((x*100) \ 1) / 100</c>: to 2 decimals, halves to even (the VBA <c>\</c> rounds its
    /// operand), judged on the double value of <c>x*100</c>.
    /// </summary>
    public static decimal Cut(double x) => (decimal)Math.Round(x * 100, 0, MidpointRounding.ToEven) / 100m;

    /// <summary>RND-B — จำนวนเงินรายการ = ราคาต่อหน่วย × จำนวน, not rounded.</summary>
    public static decimal LineNet(decimal unitPrice, decimal quantity) => (decimal)((double)unitPrice * (double)quantity);

    /// <summary>
    /// ยอดรวมของใบ: <paramref name="nets"/> are the จำนวนเงินรายการ, <paramref name="chargesVat"/> false only for VAT = NO,
    /// <paramref name="vatRate"/> from the settings (0.07).
    /// </summary>
    public static PurchaseOrderTotals Calculate(IReadOnlyList<decimal> nets, bool chargesVat, decimal vatRate)
    {
        var rate = (double)vatRate;
        var sum = nets.Sum(n => (double)n);
        var tax = chargesVat ? Cut(sum * rate) : 0m;
        var total = Cut(chargesVat ? sum + sum * rate : sum);
        var lines = nets.Select(n => new PurchaseOrderLineAmount(n, Cut(chargesVat ? (double)n + (double)n * rate : (double)n))).ToList();
        return new PurchaseOrderTotals(Cut(sum), tax, total, lines, InWords(total));
    }

    /// <summary>VAT = NO (any case) is the only value that charges no VAT.</summary>
    public static bool ChargesVat(string? vat) => !string.Equals(vat?.Trim(), "NO", StringComparison.OrdinalIgnoreCase);

    /// <summary>BR-064 — "(…)" on the order: the amount in Thai words, satang cut not rounded.</summary>
    public static string InWords(decimal amount) => ThaiNumber.Baht((double)amount);
}

/// <summary>คำนวณยอดใบสั่งซื้อด้วยอัตรา VAT จากค่าตั้งค่า (#212).</summary>
public sealed class PurchaseOrderCalculator(PurchSettingsDirectory settings)
{
    public async Task<PurchaseOrderTotals> CalculateAsync(IReadOnlyList<decimal> nets, bool chargesVat)
        => PurchaseOrderMath.Calculate(nets, chargesVat, (await settings.GetAsync()).VatRate);
}

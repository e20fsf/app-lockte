using Lockte.Purch;

namespace Lockte.Web.Tests;

/// <summary>ยอดใบสั่งซื้อและจำนวนเงินตัวอักษร (#221, BR-022, BR-064, AC-06/08/29/33) with made-up amounts from the spec's examples.</summary>
public sealed class PurchaseOrderMathTests
{
    private const decimal Vat = 0.07m;

    [Fact]
    public void Two_lines_with_vat_BR_022_example_1()
    {
        var t = PurchaseOrderMath.Calculate([950m, 5430m], true, Vat);
        Assert.Equal((6380.00m, 446.60m, 6826.60m), (t.Subtotal, t.Tax, t.Total));
        Assert.Equal([1016.50m, 5810.10m], t.Lines.Select(l => l.WithTax));
        Assert.Equal("หกพันแปดร้อยยี่สิบหกบาทหกสิบสตางค์", t.TotalInWords);
    }

    [Fact]
    public void Half_satang_rounding_follows_the_double_value_BR_022_example_2()
    {
        var t = PurchaseOrderMath.Calculate([129.20m, 127.50m], true, Vat);
        Assert.Equal((256.70m, 17.97m, 274.67m), (t.Subtotal, t.Tax, t.Total));
        Assert.Equal([138.24m, 136.43m], t.Lines.Select(l => l.WithTax));
    }

    // 300.135 → 300.13 and 500.225 → 500.23 are in the spec too, but they came from Single-stored inputs whose double value is not the
    // literal's: a half-satang literal cannot reproduce them here. The AC-07 comparison with the real orders (#271) is where they are checked.
    [Theory]
    [InlineData(138.244, 138.24)]
    [InlineData(130.968, 130.97)]
    [InlineData(374.50428, 374.50)]
    [InlineData(136.425, 136.43)]
    [InlineData(1064.115, 1064.12)]
    public void CutDecimal_gives_the_real_values_listed_for_RND_A(double input, double expected)
        => Assert.Equal((decimal)expected, PurchaseOrderMath.Cut(input));

    [Fact]
    public void No_vat_and_blank_vat()
    {
        var no = PurchaseOrderMath.Calculate([288000m], PurchaseOrderMath.ChargesVat("NO"), Vat);
        Assert.Equal((0m, 288000.00m, 288000m), (no.Tax, no.Total, no.Lines[0].WithTax));
        var blank = PurchaseOrderMath.Calculate([10000m], PurchaseOrderMath.ChargesVat(null), Vat);
        Assert.Equal(10700.00m, blank.Total);
        Assert.True(PurchaseOrderMath.ChargesVat("YES"));
        Assert.False(PurchaseOrderMath.ChargesVat(" no "));
    }

    [Fact]
    public void A_total_over_the_legacy_overflow_limit_is_calculated()
    {
        var t = PurchaseOrderMath.Calculate([25_000_000m], true, Vat);
        Assert.Equal((1_750_000m, 26_750_000m), (t.Tax, t.Total));
    }

    [Fact]
    public void Line_amount_is_price_times_quantity_without_rounding()
        => Assert.Equal(950m * 1m, PurchaseOrderMath.LineNet(950m, 1m));

    [Theory]
    [InlineData(6826.60, "หกพันแปดร้อยยี่สิบหกบาทหกสิบสตางค์")]
    [InlineData(1021, "หนึ่งพันยี่สิบเอ็ดบาทถ้วน")]
    [InlineData(11, "สิบเอ็ดบาทถ้วน")]
    [InlineData(0, "ศูนย์บาทถ้วน")]
    [InlineData(100.129, "หนึ่งร้อยบาทสิบสองสตางค์")]
    [InlineData(10.11, "สิบบาทสิบเอ็ดสตางค์")]
    [InlineData(0.01, "หนึ่งสตางค์")]
    [InlineData(1000001, "หนึ่งล้านหนึ่งบาทถ้วน")]
    [InlineData(-15.50, "ลบสิบห้าบาทห้าสิบสตางค์")]
    public void Amount_in_words_BR_064(double amount, string expected)
        => Assert.Equal(expected, Lockte.SharedKernel.ThaiNumber.Baht(amount));
}

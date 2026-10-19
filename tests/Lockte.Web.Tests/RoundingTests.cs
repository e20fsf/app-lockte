using Lockte.SharedKernel;

namespace Lockte.Web.Tests;

/// <summary>RND-A / RND-B (specs/employee/business-rules.md "การปัดเศษ").</summary>
public sealed class RoundingTests
{
    [Theory] // RND-A, as tested on Access: Val(Format(x, "#"))
    [InlineData(2.5, 3)]
    [InlineData(262.5, 263)]
    [InlineData(0.4, 0)]
    [InlineData(-2.5, -3)]
    [InlineData(205.06125, 205)]
    [InlineData(7500, 7500)]
    public void RndA_rounds_halves_away_from_zero_like_Val_Format(double value, double expected)
        => Assert.Equal(expected, Rounding.RndA(value));

    [Fact] // Format reads the Double to 15 digits first: a half that the binary value misses by a hair still rounds up
    public void RndA_reads_the_double_to_15_digits_before_rounding()
    {
        var almostWhole = 1.005 * 1000; // 1004.9999999999999
        Assert.True(almostWhole < 1005);
        Assert.Equal(1005, Rounding.RndA(almostWhole));
        Assert.Equal(1005, Rounding.RndA(1004.5));
    }

    [Theory]
    [InlineData(2.5, 0, 2)]
    [InlineData(3.5, 0, 4)]
    [InlineData(11.325, 2, 11.32)]
    [InlineData(409.8333333333333, 2, 409.83)]
    public void RndB_rounds_halves_to_even_like_VBA_Round(double value, int digits, double expected)
        => Assert.Equal(expected, Rounding.RndB(value, digits));
}

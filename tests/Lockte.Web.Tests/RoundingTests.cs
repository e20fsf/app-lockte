using Lockte.SharedKernel;

namespace Lockte.Web.Tests;

/// <summary>RND-B (specs/employee/business-rules.md "การปัดเศษ"): VBA Round, banker's on the double value.</summary>
public sealed class RoundingTests
{
    [Theory]
    [InlineData(2.5, 0, 2)]
    [InlineData(3.5, 0, 4)]
    [InlineData(11.325, 2, 11.32)]
    [InlineData(409.8333333333333, 2, 409.83)]
    public void RndB_rounds_halves_to_even_like_VBA_Round(double value, int digits, double expected)
        => Assert.Equal(expected, Rounding.RndB(value, digits));
}

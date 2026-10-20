namespace Lockte.SharedKernel;

/// <summary>
/// The legacy rounding rules (specs/employee/business-rules.md, "การปัดเศษ"). Each call site uses the rule the
/// Access code used there, so money comes out the same to the satang.
/// </summary>
public static class Rounding
{
    /// <summary>
    /// RND-A — <c>Val(Format(x, "#"))</c>: a whole number, half away from zero (2.5 → 3, −2.5 → −3). Format reads the
    /// <see cref="double"/> to 15 significant digits first (as <see cref="decimal"/> does), so 1.005 × 1000 =
    /// 1004.9999999999999 is 1005 as it was in Access.
    /// </summary>
    public static double RndA(double value) => (double)Math.Round((decimal)value, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// RND-B — VBA <c>Round(x, n)</c>: banker's rounding on the binary <see cref="double"/> value
    /// (2.5 → 2, 3.5 → 4, 11.325 → 11.32 because 11.325 is stored just below it).
    /// </summary>
    public static double RndB(double value, int digits) => Math.Round(value, digits, MidpointRounding.ToEven);
}

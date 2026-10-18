namespace Lockte.SharedKernel;

/// <summary>
/// The legacy rounding rules (specs/employee/business-rules.md, "การปัดเศษ"). Each call site uses the rule the
/// Access code used there, so money comes out the same to the satang.
/// </summary>
public static class Rounding
{
    /// <summary>
    /// RND-B — VBA <c>Round(x, n)</c>: banker's rounding on the binary <see cref="double"/> value
    /// (2.5 → 2, 3.5 → 4, 11.325 → 11.32 because 11.325 is stored just below it).
    /// </summary>
    public static double RndB(double value, int digits) => Math.Round(value, digits, MidpointRounding.ToEven);
}

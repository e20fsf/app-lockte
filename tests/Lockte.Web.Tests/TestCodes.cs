namespace Lockte.Web.Tests;

/// <summary>
/// Codes that must be unique across the whole test run: the classes of <see cref="LocalDbCollection"/> share one database, so two classes
/// that each counted up from their own random start (with the same letter) met on the same territory code (#381).
/// </summary>
public static class TestCodes
{
    private static int territory;
    private static int documentNumber = 10000;

    /// <summary>A sales-territory code ("T" + 3 hex digits, within the 4-character limit), never given out twice in a run.</summary>
    public static string Territory()
    {
        var next = Interlocked.Increment(ref territory);
        if (next > 0xFFF) throw new InvalidOperationException("Out of territory codes for this test run");
        return "T" + next.ToString("X3");
    }

    /// <summary>A production-document number (department letter + 5 digits) never given out twice in a run: those numbers are unique in the shared database.</summary>
    public static string ProductionNumber(string department)
        => department + Interlocked.Increment(ref documentNumber).ToString("00000");
}

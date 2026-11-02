using System.Text.RegularExpressions;

namespace Lockte.Calib;

/// <summary>
/// The rules the migration of <c>สอบเทียบ.accdb</c> applies to a legacy value (#209, data-model "ปัญหาคุณภาพข้อมูล"), kept here as plain functions so they are tested without a file or a
/// database. Nothing in them guesses: a value they cannot fix one way comes back as a problem for a person to decide.
/// </summary>
public static partial class CalibMigrationRules
{
    /// <summary>The first and last year a registration date can have; anything else was typed with the wrong calendar (Q-08).</summary>
    public const int FirstYear = 1990;

    /// <summary>
    /// The one answer for a date typed with the wrong calendar: the year 19xx is a Buddhist year read as a Gregorian one, so +57 (the computer was set to the Buddhist calendar: 1962 → 2019); the year 25xx
    /// is the Buddhist year itself, so −543. Other years stay. A year the rules do not cover (<c>4722</c>) comes back as <see cref="DateFix.Undecided"/>: no date, until a person decides.
    /// </summary>
    public static DateFix FixDate(DateTime date)
    {
        var year = date.Year;
        if (year is >= 1900 and <= 1999) return DateFix.Fixed(Shift(date, +57), "ปี 19xx → +57 ปี");
        if (year is >= 2500 and <= 2599) return DateFix.Fixed(Shift(date, -543), "ปี 25xx → −543 ปี");
        if (year is >= 2000 and <= 2100) return DateFix.Same(DateOnly.FromDateTime(date));
        return DateFix.Undecided;
    }

    private static DateOnly Shift(DateTime date, int years)
    {
        var day = DateOnly.FromDateTime(date);
        return day.Day == 29 && day.Month == 2 && !DateTime.IsLeapYear(day.Year + years) ? new DateOnly(day.Year + years, 2, 28) : day.AddYears(years);
    }

    /// <summary>The unit a temperature controller without one gets: all the others have it.</summary>
    public const string TempControlUnit = "°C";

    /// <summary>The abbreviation of a department and the machine codes in the brackets: <c>DC (D02)</c> → (DC, [D02]); <c>BL(32,33)</c> → (BL, [32, 33]); <c>QA</c> → (QA, []).</summary>
    public static (string Abbreviation, IReadOnlyList<string> Machines) SplitDepartment(string text)
    {
        var match = DepartmentText().Match(text.Trim());
        if (!match.Success) return (text.Trim(), []);
        var machines = match.Groups[2].Success
            ? match.Groups[2].Value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        return (match.Groups[1].Value.Trim(), machines);
    }

    /// <summary>
    /// What is wrong with the "เครื่องมือมาตรฐานที่ใช้" text (BR-021, Q-07), or null: a code with a year of other than four digits, two codes with no separator between them, a code that is not in the register of
    /// the external instruments. The text itself is never changed.
    /// </summary>
    public static string? StandardProblem(string text, ISet<string> externalCodes)
    {
        var codes = StandardCode().Matches(text);
        if (codes.Count == 0) return "ไม่พบหมายเลขเครื่องมือมาตรฐานในข้อความ";
        var rest = StandardCode().Replace(text, " ").Trim();
        if (rest.Length > 0 && !Regex.IsMatch(rest, @"^[\s\-–,&และ]*$")) return "ข้อความมีส่วนที่ไม่ใช่หมายเลขเครื่อง";
        for (var i = 0; i < codes.Count - 1; i++)
        {
            var between = text[(codes[i].Index + codes[i].Length)..codes[i + 1].Index];
            if (between.Trim().Length == 0 && between.Length == 0) return "หมายเลขสองตัวติดกันไม่มีขีดคั่น";
        }
        if (codes.Any(c => c.Groups[1].Value.Length != 4)) return "ปีท้ายหมายเลขไม่ครบ 4 หลัก";
        var missing = codes.Select(c => c.Value).FirstOrDefault(c => !externalCodes.Contains(c));
        return missing is null ? null : $"หมายเลข {missing} ไม่อยู่ในทะเบียนเครื่องมือภายนอก";
    }

    [GeneratedRegex(@"^(.*?)\s*(?:\((.*)\))?\s*$")]
    private static partial Regex DepartmentText();

    [GeneratedRegex(@"[A-Za-z]{1,3}-[A-Za-z]{1,3}\d{3}(?:-\d+)?/(\d+)")]
    private static partial Regex StandardCode();
}

/// <summary>The outcome of <see cref="CalibMigrationRules.FixDate"/>.</summary>
public sealed record DateFix(DateOnly? Date, bool Changed, string? Rule, bool IsUndecided)
{
    public static DateFix Same(DateOnly date) => new(date, false, null, false);
    public static DateFix Fixed(DateOnly date, string rule) => new(date, true, rule, false);
    public static DateFix Undecided { get; } = new(null, false, null, true);
}

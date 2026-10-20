using System.Globalization;
using Lockte.Hr;

namespace Lockte.Migration;

/// <summary>One row of the legacy table SLIP as exported from Access (the golden master of RPT-04, AC-07).</summary>
public sealed record BaselineSlip(string Code, IReadOnlyDictionary<string, string> Fields);

/// <summary>
/// เทียบสลิปกับตาราง SLIP ของระบบเดิม ทุกช่องตัวเลข (AC-07). The baseline is a CSV the team exports from
/// <c>source/พนักงาน.accdb</c> (kept out of git): a header row of SLIP's field names (any of <see cref="Columns"/>), dates
/// <c>yyyy-MM-dd</c>, numbers with a dot, blank for Null, UTF-8.
/// </summary>
public static class SlipBaseline
{
    /// <summary>The fields compared, as the slip prints them (hours to 0.1, money to the baht).</summary>
    public static IReadOnlyList<string> Columns { get; } =
        ["W", "L", "O1", "S", "O2", "H", "O3", "H_D", "SUN_D", "SALA", "OT", "S_W", "OT2", "H_W", "OT3", "DATE", "FROM_D", "TO_D", "ATM", "TT",
            "NET", "SA_D", "TT_D", "W_D", "N", "HOT"];

    public static IReadOnlyList<BaselineSlip> Read(string path)
    {
        var lines = File.ReadAllLines(path);
        var header = lines[0].TrimStart('﻿').Split(',').Select(h => h.Trim().ToUpperInvariant()).ToArray();
        var id = Array.IndexOf(header, "ID");
        if (id < 0) throw new FormatException($"{path}: ไม่มีคอลัมน์ ID");
        return lines.Skip(1).Where(l => l.Trim().Length > 0).Select(l =>
        {
            var f = Csv(l);
            return new BaselineSlip(f[id].Trim().ToUpperInvariant(),
                header.Select((h, i) => (h, i)).Where(x => Columns.Contains(x.h)).ToDictionary(x => x.h, x => f[x.i].Trim()));
        }).ToList();
    }

    /// <summary>Every field of every slip that differs, as the slip prints it; an employee on one side only.</summary>
    public static IReadOnlyList<PayMismatch> Compare(PayPeriodDates period, IEnumerable<BaselineSlip> legacy, IEnumerable<PaySlip> slips)
    {
        var old = legacy.ToDictionary(b => b.Code, StringComparer.OrdinalIgnoreCase);
        var now = slips.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        var mismatches = new List<PayMismatch>();
        foreach (var code in old.Keys.Union(now.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            if (!now.TryGetValue(code, out var slip)) { mismatches.Add(new(period.From, code, "แถว", "มี", "ไม่มี")); continue; }
            if (!old.TryGetValue(code, out var row)) { mismatches.Add(new(period.From, code, "แถว", "ไม่มี", "มี")); continue; }
            foreach (var (field, value) in Printed(period, slip))
            {
                if (!row.Fields.TryGetValue(field, out var raw)) continue;
                var legacyValue = Normalise(field, raw);
                if (legacyValue != value) mismatches.Add(new(period.From, code, field, legacyValue, value));
            }
        }
        return mismatches;
    }

    /// <summary>The new slip's fields in the form <see cref="Normalise"/> brings the legacy ones to.</summary>
    private static IEnumerable<(string Field, string Value)> Printed(PayPeriodDates period, PaySlip s)
    {
        yield return ("W", Hours(s.Normal));
        yield return ("L", Hours(s.PaidLeave));
        yield return ("O1", Hours(s.Overtime));
        yield return ("S", Hours(s.Sunday));
        yield return ("O2", Hours(s.SundayOvertime));
        yield return ("H", Hours(s.Holiday));
        yield return ("O3", Hours(s.HolidayOvertime));
        yield return ("H_D", Whole(s.PublicHolidays));
        yield return ("SUN_D", Whole(s.WeeklyHolidays));
        yield return ("SALA", Whole(s.PayNormal));
        yield return ("OT", Whole(s.Parts.Overtime));
        yield return ("S_W", Whole(s.Parts.Sunday));
        yield return ("OT2", Whole(s.Parts.SundayOvertime));
        yield return ("H_W", Whole(s.Parts.Holiday));
        yield return ("OT3", Whole(s.Parts.HolidayOvertime));
        yield return ("DATE", Date(period.PayDate));
        yield return ("FROM_D", Date(period.From));
        yield return ("TO_D", Date(period.To));
        yield return ("ATM", s.Atm is { } atm ? Whole(atm) : "");
        yield return ("TT", Whole(s.Total));
        yield return ("NET", Whole(s.Net));
        yield return ("SA_D", Money(s.Rate));
        yield return ("TT_D", Whole(s.Days));
        yield return ("W_D", Whole(s.WorkingDays));
        yield return ("N", Whole(s.Bonus));
        yield return ("HOT", Whole(s.Hot));
    }

    /// <summary>A legacy value as the report printed it: hours Fixed 1, money and counts whole, SA_D 2 decimals, Null blank.</summary>
    private static string Normalise(string field, string raw)
    {
        if (raw.Length == 0) return field is "N" or "HOT" ? "0" : ""; // Long default 0
        if (field is "DATE" or "FROM_D" or "TO_D") return raw[..Math.Min(10, raw.Length)];
        var value = double.Parse(raw, CultureInfo.InvariantCulture);
        return field switch
        {
            "W" or "L" or "O1" or "S" or "O2" or "H" or "O3" => Hours(value),
            "SA_D" => Money(value),
            _ => Whole((decimal)value),
        };
    }

    private static string Hours(double value) => Math.Round((decimal)value, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture);
    private static string Money(double value) => Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
    private static string Whole(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
    private static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>One CSV line with "quoted, fields".</summary>
    public static string[] Csv(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else current.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }
}

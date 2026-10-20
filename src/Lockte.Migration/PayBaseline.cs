using System.Globalization;
using Lockte.Hr;

namespace Lockte.Migration;

/// <summary>
/// One row of the legacy ค่าแรง history (สำเนาของ ค่าแรง) as exported from Access: the golden master that คำนวณค่าแรง
/// must match (AC-03, AC-04). W, TT_OT and OT were Long there, PAY_W/PAY_OT Long, HOT Double.
/// </summary>
public sealed record BaselinePay(string Code, DateOnly From, DateOnly To, DateOnly? PayDate, double Normal, double HolidayHours,
    double PayNormal, double PayOvertime, double Hot, double Overtime);

/// <summary>One field of one employee in one period that differs, with both values.</summary>
public sealed record PayMismatch(DateOnly From, string Code, string Field, string Legacy, string New);

/// <summary>
/// เทียบผลค่าแรงกับ baseline จาก Access รายคนรายช่อง (#53 golden master; the report for the go-live decision is #57).
/// The baseline is a CSV the team exports from <c>source/พนักงาน.accdb</c> (kept out of git, D-02):
/// <c>ID,FROM_DATE,TO_DATE,PAY_D,W,TT_OT,PAY_W,PAY_OT,HOT,OT</c>, dates <c>yyyy-MM-dd</c>, numbers with a dot, UTF-8.
/// </summary>
public static class PayBaseline
{
    public const string Header = "ID,FROM_DATE,TO_DATE,PAY_D,W,TT_OT,PAY_W,PAY_OT,HOT,OT";

    public static IReadOnlyList<BaselinePay> Read(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0 || !string.Equals(lines[0].Trim().TrimStart('﻿'), Header, StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"{path}: บรรทัดแรกต้องเป็น {Header}");
        return lines.Skip(1).Where(l => l.Trim().Length > 0).Select(l =>
        {
            var f = l.Split(',');
            DateOnly Date(string s) => DateOnly.ParseExact(s.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            double Number(string s) => s.Trim().Length == 0 ? 0 : double.Parse(s, CultureInfo.InvariantCulture);
            return new BaselinePay(f[0].Trim().ToUpperInvariant(), Date(f[1]), Date(f[2]), f[3].Trim().Length == 0 ? null : Date(f[3]),
                Number(f[4]), Number(f[5]), Number(f[6]), Number(f[7]), Number(f[8]), Number(f[9]));
        }).ToList();
    }

    /// <summary>
    /// Every difference between one period's baseline rows and the new results: an employee on one side only, PAY_W,
    /// PAY_OT, HOT and TT_OT to the baht/hour, W and OT within the rounding of the Long field Access stored them in.
    /// </summary>
    public static IReadOnlyList<PayMismatch> Compare(DateOnly from, IEnumerable<BaselinePay> legacy, IEnumerable<EmployeePay> calculated)
    {
        var old = legacy.ToDictionary(b => b.Code, StringComparer.OrdinalIgnoreCase);
        var now = calculated.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        var mismatches = new List<PayMismatch>();
        static string Text(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
        foreach (var code in old.Keys.Union(now.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            if (!now.TryGetValue(code, out var pay)) { mismatches.Add(new(from, code, "แถว", "มี", "ไม่มี")); continue; }
            if (!old.TryGetValue(code, out var row)) { mismatches.Add(new(from, code, "แถว", "ไม่มี", "มี")); continue; }
            void Exact(string field, double legacyValue, double newValue)
            {
                if (legacyValue != newValue) mismatches.Add(new(from, code, field, Text(legacyValue), Text(newValue)));
            }
            void Stored(string field, double legacyValue, double newValue)
            {
                if (Math.Abs(legacyValue - newValue) > 0.5) mismatches.Add(new(from, code, field, Text(legacyValue), Text(newValue)));
            }
            Exact("PAY_W", row.PayNormal, (double)pay.PayNormal);
            Exact("PAY_OT", row.PayOvertime, (double)pay.PayOvertime);
            Exact("HOT", row.Hot, (double)pay.Hot);
            Exact("TT_OT", row.HolidayHours, pay.HolidayHours);
            Stored("W", row.Normal, pay.Normal);
            Stored("OT", row.Overtime, pay.Overtime);
        }
        return mismatches;
    }

    /// <summary>The mismatches as CSV, for the team to read (real data: write it under source/ only).</summary>
    public static void Write(string path, IEnumerable<PayMismatch> mismatches)
        => File.WriteAllLines(path, mismatches.Select(m => string.Join(',', m.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            m.Code, m.Field, m.Legacy, m.New)).Prepend("FROM_DATE,ID,FIELD,ACCESS,NEW"), new System.Text.UTF8Encoding(true));
}

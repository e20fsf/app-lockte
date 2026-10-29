using System.Globalization;
using Lockte.Hr;
using Lockte.Migration;

namespace Lockte.Web.Tests;

/// <summary>#57 รายงานผลเทียบย้อนหลัง, without a database: the comparisons, the bank lists and what the report says.</summary>
public sealed class PeriodComparisonTests
{
    private static readonly DateOnly From = new(2026, 8, 27);

    private static EmployeeTimeTotal Time(string code, double w = 14, double l = 1, double o1 = 3.5, int sun = 2)
        => new(1, code, "นาย ทดสอบ", w, l, 0, o1, 0, 0, 0, 0, 0, sun, 0);

    private static Dictionary<string, string> Row(string id, string w = "14", string l = "1", string o1 = "3.5", string sun = "2")
        => new() { ["ID"] = id, ["W"] = w, ["L"] = l, ["O1"] = o1, ["SUN"] = sun };

    [Fact] // AC-02: hours to 0.1 as printed, counts whole
    public void Time_totals_compare_field_by_field()
    {
        var mismatches = TimeBaseline.Compare(From, [Row("A001"), Row("A002", o1: "4.0"), Row("A004")],
            [Time("A001", w: 13.96), Time("A002", o1: 3.5), Time("A003")]);

        Assert.Equal(["A002 O1 4.0 3.5", "A003 แถว ไม่มี มี", "A004 แถว มี ไม่มี"], mismatches.Select(m => $"{m.Code} {m.Field} {m.Legacy} {m.New}"));
    }

    [Fact] // BR-033 ตัวอย่าง 1–4
    public void Bank_lists_follow_BR_033()
    {
        var rows = BankTransfers.Build([
            new("A001", "111-1", "901", 7500, 875, 0),
            new("A002", "222-2", null, 5600, 0, 0),
            new("A003", null, "903", 5000, 263, 0),
            new("A004", "444-4", null, 5600, 0, 140),
        ]);

        Assert.Equal([("KBANK", "901", 875m), ("KBANK", "903", 263m), ("LETTER", "1111", 8375m), ("LETTER", "2222", 5600m), ("LETTER", "4444", 5740m),
            ("TTB", "1111", 7500m), ("TTB", "2222", 5600m), ("TTB", "4444", 5600m)], rows.Select(r => (r.Bank, r.Account, r.Amount)));
        var letter = BankTransfers.Summary(rows).Single(s => s.Bank == BankTransfers.Letter);
        Assert.Equal((3, 19715m, 30m), (letter.Items, letter.Total, letter.Fee)); // fee 10 baht an item
    }

    private static PeriodComparisonResult Result(bool slipMissing = false)
    {
        var time = new PeriodCriterion("AC-02", "เวลา", "inputs.csv", true, ["A001", "A002"], [new(From, "A002", "O1", "4.0", "3.5")]);
        var pay = new PeriodCriterion("AC-03", "ค่าแรง", "pay-history.csv", true, ["A001", "A002"], []);
        var slip = slipMissing ? new PeriodCriterion("AC-07", "สลิป", "slip.csv", false, [], [], "ไม่พบ slip.csv") : new PeriodCriterion("AC-07", "สลิป", "slip.csv", true, ["A001"], []);
        return new PeriodComparisonResult(From, new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 16), [time, pay, slip],
            [new("TTB", 2, 13100m, 20m)], [new("TTB", 2, 13100m, 20m)]);
    }

    [Fact] // a criterion with no baseline file is "ไม่ได้เทียบ", never a pass
    public void A_missing_baseline_file_is_not_a_pass()
    {
        Assert.False(Result().Passed);
        Assert.Equal("–", Result(slipMissing: true).Criteria[2].Verdict("A001"));
        Assert.Equal(("ไม่ตรง", "ตรง"), (Result().Criteria[0].Verdict("A002"), Result().Criteria[0].Verdict("A001")));
    }

    [Fact] // the report: summary for HR, one row per person per criterion, every difference with both values
    public void Report_lists_each_person_and_each_difference()
    {
        var folder = Path.Combine(Path.GetTempPath(), "period-compare-" + Guid.NewGuid().ToString("N"));
        try
        {
            PeriodComparison.WriteReport(folder, Result(slipMissing: true));

            var summary = File.ReadAllText(Path.Combine(folder, "period-compare-summary.md"));
            Assert.Contains("ยังไม่ผ่าน", summary);
            Assert.Contains("| AC-07 สลิป | ไม่ได้เทียบ |", summary);
            Assert.Contains("| A002 | O1 | 4.0 | 3.5 |", summary);
            Assert.Equal(["รหัส,AC-02 เวลา,AC-03 ค่าแรง,AC-07 สลิป", "A001,ตรง,ตรง,–", "A002,ไม่ตรง,ตรง,–"],
                File.ReadAllLines(Path.Combine(folder, "period-compare-people.csv")).Select(l => l.TrimStart('﻿')));
            Assert.Contains("AC-02,A002,O1,4.0,3.5", File.ReadAllText(Path.Combine(folder, "period-compare-details.csv")));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public void The_command_line_asks_for_the_baseline_and_the_period()
    {
        const string conn = "Server=x;Database=LockteCopy;Integrated Security=true";
        Assert.Contains("--baseline", MigrationOptions.Parse(["--program", "compare-period", "--from", "2026-08-27", "--out", "o", "--connection", conn]).Error);
        Assert.Contains("--from", MigrationOptions.Parse(["--program", "compare-period", "--baseline", "b", "--out", "o", "--connection", conn]).Error);
        var ok = MigrationOptions.Parse(["--program", "compare-period", "--baseline", "b", "--from", "2026-08-27", "--calculate", "yes", "--out", "o", "--connection", conn]).Options!;
        Assert.Equal((new DateOnly(2026, 8, 27), true), (ok.PeriodFrom, ok.Calculate));
    }
}

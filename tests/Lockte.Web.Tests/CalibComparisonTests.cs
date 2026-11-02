using Lockte.Calib;
using Lockte.Migration;

namespace Lockte.Web.Tests;

/// <summary>The comparison of the printed register with the Access baseline (#213) without a database: what is accepted, what fails, the order.</summary>
public sealed class CalibComparisonTests
{
    private static CalibPrintedRow Row(string code, string? name = "Vernier Digital", DateOnly? registered = null, string? department = "DC (D02)", string? unit = "mm", string? calibrator = "ช่าง ทดสอบ", string? note = null)
        => new(name, code, registered ?? new DateOnly(2024, 5, 3), "Mitutoyo", department, "0-150", "0.01", "0.1", unit, calibrator, note);

    private static CalibKindComparison Compare(IReadOnlyList<CalibPrintedRow> baseline, IReadOnlyList<CalibPrintedRow> now, InstrumentKind kind = InstrumentKind.Caliper)
        => CalibComparison.Compare(kind, baseline, now);

    [Fact]
    public void Identical_rows_match_and_pass()
    {
        var rows = new[] { Row("VN-A001/2567"), Row("VN-A002/2567") };
        var result = Compare(rows, rows);
        Assert.Equal((2, 2, 2, true, true), (result.BaselineRows, result.NewRows, result.Matched, result.SameOrder, result.Passed));
        Assert.Empty(result.Differences);
    }

    [Fact] // AC-02: the year fixed the one possible way is accepted and counted with its reason
    public void A_date_fixed_by_the_rules_is_accepted()
    {
        var result = Compare([Row("PG-A001/2562", registered: new DateOnly(1962, 8, 17))], [Row("PG-A001/2562", registered: new DateOnly(2019, 8, 17))]);
        Assert.True(result.Passed);
        var accepted = Assert.Single(result.Differences);
        Assert.Equal(("วันที่ขึ้นบัญชี", true), (accepted.Column, accepted.Accepted));
        Assert.StartsWith("แก้ปี", accepted.Category, StringComparison.Ordinal);
        Assert.Equal(0, result.Matched); // not an exact match: it is listed
    }

    [Fact] // a wrong date that the rules do not explain fails
    public void A_date_that_is_not_the_fixed_one_fails()
    {
        var result = Compare([Row("PG-A001/2562", registered: new DateOnly(1962, 8, 17))], [Row("PG-A001/2562", registered: new DateOnly(2018, 8, 17))]);
        Assert.False(result.Passed);
        Assert.Equal("วันที่ไม่ตรง", Assert.Single(result.Unaccepted).Category);
    }

    [Fact] // Q-08: year 4722 is left blank until decided
    public void An_undecided_date_left_blank_is_accepted()
    {
        var result = Compare([Row("PG-DC107/2565", registered: new DateOnly(4722, 3, 1))], [Row("PG-DC107/2565") with { Registered = null }]);
        Assert.True(result.Passed);
        Assert.Contains("Q-08", Assert.Single(result.Differences).Category, StringComparison.Ordinal);
    }

    [Fact] // reports.md: `DC (D02)` printed by the register is the same words as the legacy text; spaces do not count
    public void The_department_is_compared_without_spaces_and_an_unlinked_one_is_accepted()
    {
        Assert.True(Compare([Row("X1", department: "BL(32,33)")], [Row("X1", department: "BL (32,33)")]).Passed);
        var unlinked = Compare([Row("X1", department: "DC(44)")], [Row("X1", department: null)]);
        Assert.True(unlinked.Passed);
        Assert.Contains("DQ-Q05", Assert.Single(unlinked.Differences).Category, StringComparison.Ordinal);
        Assert.False(Compare([Row("X1", department: "DC(44)")], [Row("X1", department: "BL (32)")]).Passed);
    }

    [Fact] // the calibrator: the same printed name, or not yet linked (DQ report), but never another name
    public void A_calibrator_not_linked_yet_is_accepted_and_another_name_fails()
    {
        Assert.True(Compare([Row("X1")], [Row("X1", calibrator: null)]).Passed);
        Assert.False(Compare([Row("X1")], [Row("X1", calibrator: "คนอื่น")]).Passed);
    }

    [Fact] // the temperature controller without a unit got °C
    public void A_blank_temp_control_unit_filled_with_celsius_is_accepted_only_for_that_kind()
    {
        Assert.True(Compare([Row("TC-A1", unit: null)], [Row("TC-A1", unit: "°C")], InstrumentKind.TempControl).Passed);
        Assert.False(Compare([Row("VN-A1", unit: null)], [Row("VN-A1", unit: "°C")], InstrumentKind.Caliper).Passed);
    }

    [Fact] // the words the migration put in the note for a department or machine it could not link do not count
    public void The_migration_note_in_brackets_is_ignored_and_other_note_changes_fail()
    {
        Assert.True(Compare([Row("X1", note: "ตรวจประจำปี")], [Row("X1", note: "ตรวจประจำปี [แผนกเดิม: ZZ]")]).Passed);
        Assert.False(Compare([Row("X1", note: "ตรวจประจำปี")], [Row("X1", note: "แก้แล้ว")]).Passed);
    }

    [Fact] // text is compared as printed: a trimmed value is the same, another value is not
    public void Text_columns_must_equal_after_trimming()
    {
        Assert.True(Compare([Row("X1", name: " Vernier  Digital ")], [Row("X1", name: "Vernier Digital")]).Passed);
        var result = Compare([Row("X1", name: "Vernier Digital")], [Row("X1", name: "Vernier Digital 2")]);
        Assert.Equal("ข้อความไม่ตรง", Assert.Single(result.Unaccepted).Category);
    }

    [Fact] // the rows the migration did not load for a DQ reason are accepted; a row lost without one is not
    public void A_missing_row_fails_unless_its_code_was_blank_or_repeated()
    {
        var baseline = new[] { Row("A1"), Row("A1"), Row(""), Row("A2") };
        var result = Compare(baseline, [Row("A1")]);
        Assert.Equal(3, result.Differences.Count);
        Assert.Equal(2, result.Differences.Count(d => d.Accepted));
        Assert.Equal("แถวหาย", Assert.Single(result.Unaccepted).Category);
        Assert.Equal("A2", result.Unaccepted[0].Code);
    }

    [Fact]
    public void A_row_only_in_the_database_and_a_different_order_fail()
    {
        Assert.False(Compare([Row("A1")], [Row("A1"), Row("A9")]).Passed);
        var swapped = Compare([Row("A1"), Row("A2")], [Row("A2"), Row("A1")]);
        Assert.False(swapped.SameOrder);
        Assert.False(swapped.Passed);
    }

    [Fact]
    public void The_expected_counts_are_the_222_of_the_data_model()
    {
        Assert.Equal(222, CalibComparison.ExpectedCounts.Values.Sum());
        Assert.Equal(Enum.GetValues<InstrumentKind>().Order(), CalibComparison.ExpectedCounts.Keys.Order());
        Assert.Equal("calib-pressuregauge.csv", CalibComparison.BaselineFile(InstrumentKind.PressureGauge));
    }

    [Fact]
    public void A_baseline_file_is_read_with_its_dates()
    {
        var folder = Directory.CreateTempSubdirectory("lockte-calib-baseline-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, CalibComparison.BaselineFile(InstrumentKind.Caliper)),
                "name,code,registered,maker,department,range,resolution,limit,unit,calibrator,note\r\nVernier Digital,VN-A001/2567,1964-02-29,Mitutoyo,DC (D02),0-150,0.01,0.1,mm,ช่าง ทดสอบ,\r\n");
            var row = Assert.Single(CalibComparison.ReadBaseline(folder, InstrumentKind.Caliper));
            Assert.Equal(("VN-A001/2567", new DateOnly(1964, 2, 29), "DC (D02)"), (row.Code, row.Registered, row.Department));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}

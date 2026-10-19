using System.Globalization;
using Lockte.Hr;

namespace Lockte.Web.Tests;

/// <summary>
/// BR-020 ตรวจโควตาลาที่ได้ค่าแรง (SAL_AfterUpdate of ฟอร์ม LEAVE_A) as a rule on its own: AC-08 = the five cases of
/// the Input/Expected table in specs/employee/business-rules.md, then every SR of the legacy Select Case.
/// </summary>
public sealed class LeaveQuotaTests
{
    private const string Saved = "คลิกปุ่ม OK เพื่อทำการบันทึก";

    private static LeaveQuotaCheck Check(string type, bool paid, double days, Dictionary<string, double>? used = null,
        string? sex = "F", bool hasVacation = true)
        => LeaveQuota.Check(type, paid, days, used ?? [], sex, hasVacation);

    [Fact] // AC-08 #1: SR=1, ใช้ไปแล้ว (SR1+SR9) = 5, ลาใหม่ 1 → บันทึก (=6 พอดี)
    public void Case_1_personal_leave_up_to_exactly_six_days_is_saved()
        => Assert.Equal(new LeaveQuotaCheck(true, Saved), Check("1", true, 1, new() { ["1"] = 3, ["9"] = 2 }));

    [Fact] // AC-08 #2: SR=9, ใช้ไปแล้ว 5.5, ลาใหม่ 1 → ไม่บันทึก
    public void Case_2_emergency_leave_over_six_days_with_personal_leave_is_not_saved()
        => Assert.Equal(new LeaveQuotaCheck(false, "ไม่สามารถบันทึกได้ เนื่องจากลากิจได้ค่าแรงเกิน 6 วัน"),
            Check("9", true, 1, new() { ["1"] = 5.5 }));

    [Fact] // AC-08 #3: SR=6, อายุงาน 200 วัน → ไม่บันทึก
    public void Case_3_vacation_before_a_year_of_work_is_not_saved()
        => Assert.Equal(new LeaveQuotaCheck(false, "ไม่สามารถบันทึกได้ เนื่องจากยังไม่มีพักร้อน"),
            Check("6", true, 1, hasVacation: false));

    [Fact] // AC-08 #4: SR=4, SEX=M → ไม่บันทึก
    public void Case_4_maternity_leave_of_a_man_is_not_saved()
        => Assert.Equal(new LeaveQuotaCheck(false, "ไม่สามารถบันทึกได้ เนื่องจากเพศชายลาคลอดไม่ได้"), Check("4", true, 1, sex: "M"));

    [Fact] // AC-08 #5: SR=8, SAL=N → บันทึก (ไม่ตรวจ)
    public void Case_5_unpaid_leave_is_never_checked()
        => Assert.Equal(new LeaveQuotaCheck(true, Saved), Check("8", false, 100, new() { ["8"] = 100 }));

    [Theory] // the limits: SUM_D ใหม่ + ใช้ไปแล้ว ≤ limit passes, above fails
    [InlineData("2", "3", 30, "ไม่สามารถบันทึกได้ เนื่องจากลาป่วยได้ค่าแรงเกิน 30 วัน")]
    [InlineData("3", "2", 30, "ไม่สามารถบันทึกได้ เนื่องจากลาป่วยได้ค่าแรงเกิน 30 วัน")]
    [InlineData("6", "6", 12, "ไม่สามารถบันทึกได้ เนื่องจากลาพักร้อนเกิน 12 วัน")]
    [InlineData("4", "4", 60, "ไม่สามารถบันทึกได้ เนื่องจากลาคลอดได้ค่าแรงเกิน 60 วัน")]
    [InlineData("10", "10", 60, "ไม่สามารถบันทึกได้ เนื่องจากลาได้ค่าแรงเกิน 60 วัน")]
    [InlineData("1", "9", 6, "ไม่สามารถบันทึกได้ เนื่องจากลากิจได้ค่าแรงเกิน 6 วัน")]
    public void Each_limited_type_counts_its_group_up_to_the_limit(string type, string usedType, double limit, string message)
    {
        Assert.Equal(new LeaveQuotaCheck(true, Saved), Check(type, true, 0.5, new() { [usedType] = limit - 0.5 }));
        Assert.Equal(new LeaveQuotaCheck(false, message), Check(type, true, 0.625, new() { [usedType] = limit - 0.5 }));
    }

    [Fact] // the used days of other groups do not count
    public void Other_types_do_not_use_the_quota()
        => Assert.True(Check("1", true, 6, new() { ["2"] = 30, ["6"] = 12, ["8"] = 50 }).CanSave);

    [Theory] // อุบัติเหตุในงาน, ฝึกอบรมของบริษัท, ทำหมัน: ไม่จำกัด
    [InlineData("5")]
    [InlineData("11")]
    [InlineData("12")]
    public void Some_types_have_no_limit(string type)
        => Assert.Equal(new LeaveQuotaCheck(true, Saved), Check(type, true, 500, new() { [type] = 500 }));

    [Theory] // ห้ามได้ค่าแรง
    [InlineData("7", "ไม่สามารถบันทึกได้ เนื่องจากลาอื่นๆไม่ได้ค่าแรง")]
    [InlineData("8", "ไม่สามารถบันทึกได้ เนื่องจากขาดงานไม่ได้ค่าแรง")]
    [InlineData("13", "ไม่สามารถบันทึกได้ เนื่องจากเป็นการลาที่ไม่ได้ค่าแรง")]
    [InlineData("14", "ไม่สามารถบันทึกได้ เนื่องจากเป็นการลาที่ไม่ได้ค่าแรง")]
    [InlineData("15", "ไม่สามารถบันทึกได้ เนื่องจากเป็นการลาที่ไม่ได้ค่าแรง")]
    public void Some_types_are_never_paid(string type, string message)
    {
        Assert.Equal(new LeaveQuotaCheck(false, message), Check(type, true, 0.125));
        Assert.Equal(new LeaveQuotaCheck(true, Saved), Check(type, false, 0.125));
    }

    [Fact] // SR=16 ลาป่วยเป็นโรคระบาด is not in the legacy Select Case: no message, saved, no limit (⚠ kept)
    public void Epidemic_sick_leave_has_no_rule_and_no_message()
        => Assert.Equal(new LeaveQuotaCheck(true, null), Check("16", true, 500, new() { ["16"] = 500 }));

    [Fact] // ลาคลอดของผู้หญิง: the SEX check comes first, then the 60 days
    public void Maternity_leave_needs_a_woman_and_then_the_limit()
    {
        Assert.True(Check("4", true, 60, sex: "F").CanSave);
        Assert.False(Check("4", true, 1, sex: null).CanSave); // Text70 = "F" is false for a blank SEX
    }

    [Fact] // ลาพักร้อน: อายุงานก่อน แล้วจึงเพดาน 12 วัน
    public void Vacation_needs_a_year_of_work_and_then_the_limit()
        => Assert.Equal(new LeaveQuotaCheck(false, "ไม่สามารถบันทึกได้ เนื่องจากยังไม่มีพักร้อน"),
            Check("6", true, 20, hasVacation: false));

    [Theory] // อายุงาน ≥ 365 วัน = DateDiff("d", START, Date()) ≥ 365; a blank START counts as YES (IIf(Null < 365) is false)
    [InlineData("2025-10-19", true)]
    [InlineData("2025-10-20", false)]
    [InlineData("2026-04-02", false)] // 200 days
    [InlineData(null, true)]
    public void A_year_of_work_is_365_days_counted_to_today(string? start, bool expected)
        => Assert.Equal(expected, LeaveQuota.HasVacation(start is null ? null : DateOnly.Parse(start, CultureInfo.InvariantCulture), new DateOnly(2026, 10, 19)));
}

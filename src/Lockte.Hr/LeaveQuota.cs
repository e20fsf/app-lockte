namespace Lockte.Hr;

/// <summary>ประเภทการลา (SR of การลา, T-ประเภทการลางาน): codes 1–16 with their legacy names (data-model).</summary>
public static class LeaveTypes
{
    public const int Length = 2;

    public static IReadOnlyDictionary<string, string> Names { get; } = new Dictionary<string, string>
    {
        ["1"] = "ลากิจ", ["2"] = "ลาป่วยมีใบแพทย์", ["3"] = "ลาป่วยไม่มีใบแพทย์", ["4"] = "ลาคลอด",
        ["5"] = "ลาอุบัติเหตุในงาน", ["6"] = "ลาพักร้อน", ["7"] = "ลาอื่นๆ", ["8"] = "ขาดงาน", ["9"] = "ลากิจฉุกเฉิน",
        ["10"] = "ลาเพื่อระดมพล", ["11"] = "ลาเพื่อการศึกษาฝึกอบรมของบริษัท", ["12"] = "ลาทำหมัน",
        ["13"] = "ลากิจโดยไม่ได้แจ้งก่อนล่วงหน้า", ["14"] = "ลากิจเกินกำหนด", ["15"] = "ลาเพื่อการศึกษาฝึกอบรมส่วนตัว",
        ["16"] = "ลาป่วยเป็นโรคระบาด",
    };

    /// <summary>Codes in their legacy order 1–16 (the dropdown of SCR-30).</summary>
    public static IReadOnlyList<string> Codes { get; } = [.. Names.Keys];
}

/// <summary>
/// ผลตรวจโควตา (BR-020): whether the row may be saved, and the legacy MsgBox text — null when the legacy showed none
/// (SR 16). <see cref="Title"/> is the MsgBox title.
/// </summary>
public sealed record LeaveQuotaCheck(bool CanSave, string? Message)
{
    public string Title => CanSave ? "บันทึก" : "ไม่บันทึก";
}

/// <summary>
/// BR-020 ตรวจโควตาลาที่ได้ค่าแรง, as <c>SAL_AfterUpdate</c> of ฟอร์ม LEAVE_A: the same Select Case, limits and messages.
/// "ใช้ไปแล้ว" comes from the caller: the employee's ได้ค่าแรง days by SR of the calendar year (Q-27, #23).
/// </summary>
public static class LeaveQuota
{
    public const string SaveMessage = "คลิกปุ่ม OK เพื่อทำการบันทึก";
    public const string NoVacationMessage = "ไม่สามารถบันทึกได้ เนื่องจากยังไม่มีพักร้อน";
    public const string MaleMaternityMessage = "ไม่สามารถบันทึกได้ เนื่องจากเพศชายลาคลอดไม่ได้";

    /// <summary>The types with a limit, in the order of the quota table on SCR-30. Types of one group count together.</summary>
    public static IReadOnlyList<LeaveQuotaRule> Limits { get; } =
    [
        new("ลากิจ", ["1", "9"], 6, "ไม่สามารถบันทึกได้ เนื่องจากลากิจได้ค่าแรงเกิน 6 วัน"),
        new("ลาป่วย", ["2", "3"], 30, "ไม่สามารถบันทึกได้ เนื่องจากลาป่วยได้ค่าแรงเกิน 30 วัน"),
        new("ลาพักร้อน", ["6"], 12, "ไม่สามารถบันทึกได้ เนื่องจากลาพักร้อนเกิน 12 วัน"),
        new("ลาคลอด", ["4"], 60, "ไม่สามารถบันทึกได้ เนื่องจากลาคลอดได้ค่าแรงเกิน 60 วัน"),
        new("ลาเพื่อระดมพล", ["10"], 60, "ไม่สามารถบันทึกได้ เนื่องจากลาได้ค่าแรงเกิน 60 วัน"),
    ];

    private static readonly Dictionary<string, string> NeverPaid = new()
    {
        ["7"] = "ไม่สามารถบันทึกได้ เนื่องจากลาอื่นๆไม่ได้ค่าแรง",
        ["8"] = "ไม่สามารถบันทึกได้ เนื่องจากขาดงานไม่ได้ค่าแรง",
        ["13"] = "ไม่สามารถบันทึกได้ เนื่องจากเป็นการลาที่ไม่ได้ค่าแรง",
        ["14"] = "ไม่สามารถบันทึกได้ เนื่องจากเป็นการลาที่ไม่ได้ค่าแรง",
        ["15"] = "ไม่สามารถบันทึกได้ เนื่องจากเป็นการลาที่ไม่ได้ค่าแรง",
    };

    private static readonly HashSet<string> Unlimited = ["5", "11", "12"];

    /// <summary>
    /// SAL = N is never checked. SAL = Y: ลาพักร้อน needs <paramref name="hasVacation"/> first and ลาคลอด SEX = F first,
    /// then <paramref name="days"/> + the group's used days ≤ its limit. SR 16 (and any SR not in the legacy Select
    /// Case) shows no message and is saved without a limit (⚠ kept).
    /// </summary>
    public static LeaveQuotaCheck Check(string type, bool paid, double days, IReadOnlyDictionary<string, double> usedPaidDays,
        string? sex, bool hasVacation)
    {
        if (!paid || Unlimited.Contains(type)) return new(true, SaveMessage);
        if (NeverPaid.TryGetValue(type, out var never)) return new(false, never);
        if (Limits.FirstOrDefault(l => l.Types.Contains(type)) is not { } limit) return new(true, null);
        if (type == "6" && !hasVacation) return new(false, NoVacationMessage);
        if (type == "4" && sex != "F") return new(false, MaleMaternityMessage);
        return days + limit.Used(usedPaidDays) <= limit.Days ? new(true, SaveMessage) : new(false, limit.OverMessage);
    }

    /// <summary>อายุงาน ≥ 365 วัน (คิวรี สรุปการลา00): DateDiff("d", START, Date()) ≥ 365; a blank START counts as YES (⚠ kept).</summary>
    public static bool HasVacation(DateOnly? startDate, DateOnly today)
        => startDate is not { } start || today.DayNumber - start.DayNumber >= 365;
}

/// <summary>One limit of BR-020: the days of <see cref="Types"/> ได้ค่าแรง together may reach <see cref="Days"/>.</summary>
public sealed record LeaveQuotaRule(string Name, IReadOnlyList<string> Types, double Days, string OverMessage)
{
    public double Used(IReadOnlyDictionary<string, double> usedPaidDays) => Types.Sum(t => usedPaidDays.GetValueOrDefault(t));
}

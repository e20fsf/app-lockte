namespace Lockte.Hr;

/// <summary>
/// What HR types on SCR-52 ปรับค่าแรง (the legacy kept it in the one-row table ลางานทั้งแผนก): the leaves and ใบเตือน of
/// <see cref="From"/>–<see cref="To"/> are counted, อายุงาน is to <see cref="To"/>; <see cref="DepartmentId"/> null = ทุกแผนก
/// (the legacy "*"); เพดานวันลา (TTL) and ขั้น % ตามอายุงาน STEP1–STEP5 (<see cref="Steps"/>, อายุงาน &lt; 1, &lt; 2, &lt; 4,
/// &lt; 7, ≥ 7 ปี).
/// </summary>
public sealed record PayAdjustmentInput(DateOnly From, DateOnly To, int? DepartmentId, double LeaveCap, IReadOnlyList<double> Steps)
{
    /// <summary>The legacy table's values: TTL 25, STEP 2.83 / 2.83 / 4 / 5 / 6 (data-model.md, ค่าที่ต้องจำ).</summary>
    public const double DefaultLeaveCap = 25;

    public static IReadOnlyList<double> DefaultSteps { get; } = [2.83, 2.83, 4, 5, 6];
}

/// <summary>
/// RPT-37 รายละเอียดการปรับเงินเดือน (รายเดือน, report ปรับค่าแรง1) and การปรับค่าแรง (รายวัน, report ปรับค่าแรง2) of BR-032,
/// each by รหัส. Only a report: nothing is written to the employees (Q-32 — HR keys the new pay on SCR-03, as before).
/// </summary>
public sealed record PayAdjustmentReport(PayAdjustmentInput Input, IReadOnlyList<PayAdjustmentLine> Monthly,
    IReadOnlyList<PayAdjustmentLine> Daily);

/// <summary>
/// One employee of RPT-37, as the legacy tables ปรับค่าแรง(รายเดือน) / ปรับค่าแรง(รายวัน) held it (BR-032):
/// <see cref="Wage"/> is SALARY (รายเดือน) or SA_DAY (รายวัน) and <see cref="NewWage"/> เงินเดือนใหม่ / ค่าแรงใหม่, both whole
/// baht as those Long columns stored them; <see cref="LeaveDays"/> รวมวันลา (Expr1); <see cref="ServiceYears"/> อายุงาน
/// (Expr2, null without วันเริ่มงาน); <see cref="Rate"/> the % of อายุงาน printed in column "%" (Expr6) even when nothing is
/// added; <see cref="Increase"/> ปรับเพิ่ม (Expr3). ใบเตือน are counted for รายวัน only (the รายเดือน report has no column).
/// </summary>
public sealed record PayAdjustmentLine(
    string Code,
    string Name,
    DateOnly? StartDate,
    double? ServiceYears,
    double Wage,
    double LeaveDays,
    int VerbalWarnings,
    int WrittenWarnings,
    double Rate,
    double Increase,
    double NewWage);

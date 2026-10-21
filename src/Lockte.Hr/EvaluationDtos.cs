namespace Lockte.Hr;

/// <summary>
/// What SCR-53 บันทึกการประเมินผล saves on the employee (BR-055, subform บันทึกการประเมินผล bound to EMPLO): ครั้งที่ 1
/// (DATE_1 / POINT_1 / GRED_1), ครั้งที่ 2 (DATE_2 / POINT_2 / GRED_2), วันที่บรรจุ (DATE_3) and หมายเหตุ (MEM).
/// ⚠ The legacy fields mean other things too: POINT_1 is also a set wage per period (BR-003, Q-09), GRED_1 keeps the
/// employee out of ค่าแรงสิ้นเดือน (BR-063), GRED_2 is cleared by ใบขอ OT (BR-012), MEM is also เหตุผลลาออก (BR-044).
/// </summary>
public sealed record EvaluationInput(
    DateOnly? Evaluation1Date,
    int? Evaluation1Score,
    string? Evaluation1Grade,
    DateOnly? Evaluation2Date,
    int? Evaluation2Score,
    string? Evaluation2Grade,
    DateOnly? ProbationPassedDate,
    string? Memo);

/// <summary>One row of RPT-43 รายงานการประเมินผลพนักงาน (report การประเมินผลพนักงาน): an employee with DATE_1 set, not resigned.</summary>
public sealed record EvaluationListLine(
    string Code,
    string Name,
    string? DepartmentName,
    string? Position,
    DateOnly? StartDate,
    int? EmployeeType,
    string? Education,
    string? ShiftCode,
    DateOnly? Evaluation1Date,
    int? Evaluation1Score,
    string? Evaluation1Grade,
    DateOnly? Evaluation2Date,
    int? Evaluation2Score,
    string? Evaluation2Grade,
    DateOnly? ProbationPassedDate,
    string? Memo);

/// <summary>Which RPT-43 ใบประเมินประจำปี (SCR-54): ปุ่ม "พนักงาน" (รายวัน) or "หัวหน้าหรือรองฯ" (รายเดือน).</summary>
public enum YearlyEvaluationKind
{
    /// <summary>ใบรายงานการประเมินผลงานพนักงานประจำปี: ตำแหน่ง Like "พนักงาน*" (คิวรี พิมพ์ลางานทั้งแผนกA201).</summary>
    Staff,
    /// <summary>ใบรายงานการประเมินผลงานหัวหน้าหรือรองหัวหน้าประจำปี: ตำแหน่ง Not Like "พนักงาน*" (คิวรี A101).</summary>
    Supervisor,
}

/// <summary>
/// RPT-43 ใบรายงานการประเมินผลงานประจำปี (BR-067) for the leaves, late days and ใบเตือน of <see cref="From"/>–<see cref="To"/>:
/// one sheet per employee, by รหัส. <see cref="DepartmentName"/> null = ทั้งหมด (the legacy "*").
/// </summary>
public sealed record YearlyEvaluationReport(YearlyEvaluationKind Kind, DateOnly From, DateOnly To, string? DepartmentName,
    IReadOnlyList<YearlyEvaluationSheet> Sheets);

/// <summary>
/// สถิติการลางานและใบเตือน of one employee on the ใบประเมินประจำปี (BR-067): ลากิจ (SR 1 — the report's box is bound to
/// SumOf1), ลาป่วยมีใบรับรองแพทย์ (SR 2), ไม่มีใบ (SR 3), มาสาย (rows of เวลาทำงาน with LATE), ขาดงาน (SR 8), ใบเตือนวาจา /
/// หนังสือ (CODE 1 / 2 with DEPART = the แผนก chosen — none for ทั้งหมด, Q-31).
/// </summary>
public sealed record YearlyEvaluationSheet(
    string Code,
    string Name,
    string? SectionName,
    double PersonalLeave,
    double SickLeaveWithCertificate,
    double SickLeaveWithoutCertificate,
    int LateCount,
    double Absent,
    int VerbalWarnings,
    int WrittenWarnings)
{
    /// <summary>คะแนนที่หักต่อหน่วย as printed (labels; the ใบประเมิน's ControlSources multiply by the same).</summary>
    public static class Points
    {
        public const double PersonalLeave = -1;
        public const double SickLeaveWithCertificate = -1;
        public const double SickLeaveWithoutCertificate = -1;
        public const double Late = -0.05;
        public const double Absent = -3;
        public const double VerbalWarning = -5;
        public const double WrittenWarning = -10;
    }

    /// <summary>คะแนนหักรวม (2) (Text97 = the sum of the seven คะแนนที่หัก) — negative, as printed.</summary>
    public double TotalDeduction
        => PersonalLeave * Points.PersonalLeave + SickLeaveWithCertificate * Points.SickLeaveWithCertificate
           + SickLeaveWithoutCertificate * Points.SickLeaveWithoutCertificate + LateCount * Points.Late + Absent * Points.Absent
           + VerbalWarnings * Points.VerbalWarning + WrittenWarnings * Points.WrittenWarning;
}

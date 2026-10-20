namespace Lockte.Hr;

/// <summary>
/// RPT-50 ใบลางาน (BR-065, คิวรี พิมพ์ใบลางาน2): the employee, the วันลาสะสม of this calendar year by ประเภทการลา
/// (Q-27: the year replaces the legacy "current" LEAVE table), ใบเตือน counts and the two entitlement texts.
/// </summary>
/// <param name="OnProbation">วันนี้ − START &lt; 90: the X goes in "รอบรรจุเป็นพนักงาน", else in "บรรจุเป็นพนักงาน".</param>
/// <param name="TotalDays">จำนวนวันลาทั้งหมด; null when there is no leave this year (prints blank).</param>
/// <param name="DaysByType">ประเภทการลา (SR "1".."16") → days; a type without leave is missing (prints blank).</param>
public sealed record LeaveFormData(
    string Code,
    string Name,
    string? DepartmentName,
    string? ShiftCode,
    DateOnly? StartDate,
    bool OnProbation,
    string VacationText,
    string PersonalLeaveText,
    int Year,
    double? TotalDays,
    IReadOnlyDictionary<string, double> DaysByType,
    int VerbalWarnings,
    int WrittenWarnings);

/// <summary>
/// RPT-53 หนังสือรับรองการทำงาน / เงินเดือน (BR-070, คิวรี พิมพ์หนังสือรับรองเงินเดือน1): texts as the legacy query
/// built them (two spaces around เดือน / พ.ศ.).
/// </summary>
/// <param name="BirthText">Expr13, e.g. "28  เดือน  กันยายน  พ.ศ.  2525"; blank without a birth date.</param>
/// <param name="Age">Expr12 = Int((Now − BORN) / 365).</param>
/// <param name="StartText">Expr11, as <paramref name="BirthText"/>.</param>
/// <param name="ServiceText">Expr10, e.g. "7  ปี  3  เดือน".</param>
/// <param name="MonthlyPay">Expr4: รายเดือน = SALARY, otherwise SA_DAY × 26.</param>
/// <param name="IssuedText">"ให้ไว้ ณ วันที่" = today as <paramref name="BirthText"/>.</param>
public sealed record CertificateData(
    string Name,
    string BirthText,
    int? Age,
    string? Position,
    string? DepartmentName,
    string StartText,
    string ServiceText,
    double MonthlyPay,
    string? FactoryManagerName,
    string IssuedText);

/// <summary>
/// The ตาราง เกษียณ of ฟอร์ม เกษียณ (BR-070): วันชดเชย of the six steps (columns 1–6) and the seven lines printed under
/// "การจ่ายเงินชดเชยตามกฎหมายแรงงาน" (column 13 first, then 7–12).
/// </summary>
public sealed record SeveranceScale(IReadOnlyList<int> Days, IReadOnlyList<string> Lines)
{
    /// <summary>Service days at which each step of <see cref="Days"/> starts.</summary>
    public static IReadOnlyList<int> StepStarts { get; } = [120, 365, 1095, 2190, 3650, 7300];

    /// <summary>The values in the legacy table (data-model "ค่าที่ต้องจำ", print-samples RPT-54).</summary>
    public static SeveranceScale Legacy { get; } = new(
        [30, 90, 180, 240, 300, 400],
        [
            "อายุงานน้อยกว่า 120 วัน ไม่จ่ายค่าชดเชย",
            "อายุงานตั้งแต่ 120 วันถึง 1 ปี",
            "อายุงานตั้งแต่ 1 ปีถึง 3 ปี",
            "อายุงานตั้งแต่ 3 ปีถึง 6 ปี",
            "อายุงานตั้งแต่ 6 ปีถึง 10 ปี",
            "อายุงานตั้งแต่ 10 ปีถึง 20 ปี",
            "อายุงานตั้งแต่ 20 ปีขึ้นไป",
        ]);

    /// <summary>วันชดเชย for a service of <paramref name="serviceDays"/> (Expr13): 0 below 120 days.</summary>
    public int DaysFor(double serviceDays)
    {
        var days = 0;
        for (var i = 0; i < StepStarts.Count; i++)
            if (serviceDays >= StepStarts[i]) days = Days[i];
        return days;
    }
}

/// <summary>RPT-54 ฟอร์มเกษียณ / ฟอร์มปลดออก (คิวรี เกษียณ3): the certificate's texts plus ค่าชดเชย.</summary>
/// <param name="CompensationDays">Expr13 from the scale by อายุงาน (วัน).</param>
/// <param name="Compensation">Expr14 = RND-A(วันชดเชย × SA_DAY).</param>
/// <param name="Reason">เหตุผล (MARK) — printed on ฟอร์มปลดออก only.</param>
public sealed record SeveranceData(
    CertificateData Person,
    double DailyWage,
    int CompensationDays,
    decimal Compensation,
    string? Reason,
    SeveranceScale Scale);

/// <summary>
/// RPT-55 ใบลาออก (คิวรี พิมพ์ใบลาออก): the employee and the names printed for the employee's แผนก in their กะ (หัวหน้าแผนก /
/// ผู้จัดการฝ่าย / ผู้จัดการโรงงาน), with the date and reason typed on SCR-33.
/// </summary>
public sealed record ResignationLetterData(
    string Code,
    string Name,
    string? DepartmentName,
    string? ShiftCode,
    DateOnly? StartDate,
    DateOnly? ResignDate,
    string? Reason,
    string? SupervisorName,
    string? ManagerName,
    string? FactoryManagerName);

/// <summary>One row of RPT-55 รายงานพนักงานที่ลาออก (คิวรี พิมพ์ใบลาออก3).</summary>
/// <param name="Years">อายุงาน(ปี) = (RESIGN − START) / 365; null without a start date.</param>
public sealed record ResignedEmployee(string Code, string Name, DateOnly? StartDate, DateOnly ResignDate, double? Years, string? Reason);

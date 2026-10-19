namespace Lockte.Hr;

/// <summary>
/// คำนวณเวลาทำงานต่อรายการ (BR-001, คิวรี คำนวณเวลาทำงาน1/2). เวลาออกก่อนเวลาเข้า = วันถัดไป, as the legacy W_OUT on
/// 1899-12-31 (กะ B 20:10–05:10); the same times = 0.
/// </summary>
public static class WorkDuration
{
    /// <summary>W counts as a whole day from 7.75 ชม. (<c>ชั่วโมง/8 ≥ 0.96875</c>).</summary>
    public const double FullDayFraction = 0.96875;

    /// <summary><c>(W_OUT − W_IN) × 24 − REST_T / 60</c>, not rounded.</summary>
    public static double Hours(TimeOnly workIn, TimeOnly workOut, int restMinutes)
        => ((workOut - workIn).TotalMinutes - restMinutes) / 60;

    /// <summary>WORK_T: TYPE W = วัน (1 from <see cref="FullDayFraction"/>, else ชั่วโมง/8), other types = ชั่วโมง.</summary>
    public static double WorkT(string? type, TimeOnly workIn, TimeOnly workOut, int restMinutes)
    {
        var hours = Hours(workIn, workOut, restMinutes);
        if (type != WorkTypes.Normal) return hours;
        return hours / 8 >= FullDayFraction ? 1 : hours / 8;
    }
}

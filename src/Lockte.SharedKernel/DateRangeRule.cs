namespace Lockte.SharedKernel;

/// <summary>
/// The one rule of every ตั้งแต่วันที่ – ถึงวันที่ pair (UAT-27, #405): the start must not be after the end. The screens (the shared
/// DateRangePicker) and the services show and return this same Thai message. A pair with a blank end is not wrong yet.
/// </summary>
public static class DateRangeRule
{
    public const string Message = "ตั้งแต่วันที่ต้องไม่หลังถึงวันที่ กรุณาแก้วันที่";

    public static bool IsInvalid(DateOnly? from, DateOnly? to) => from is { } f && to is { } t && f > t;

    public static bool IsInvalid(DateTime? from, DateTime? to) => from is { } f && to is { } t && f.Date > t.Date;

    /// <summary>The message when the pair is wrong, else null.</summary>
    public static string? Check(DateOnly? from, DateOnly? to) => IsInvalid(from, to) ? Message : null;
}

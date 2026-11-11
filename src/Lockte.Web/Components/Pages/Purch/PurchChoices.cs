namespace Lockte.Web.Components.Pages.Purch;

/// <summary>The fixed choices of the purchasing screens that tests can check without rendering a page (UAT วัตถุดิบ ครั้งที่ 1, #421).</summary>
public static class PurchChoices
{
    /// <summary>ไตรมาสที่ 1–4 with its months, for the quarter dropdown (it once listed "5" four times).</summary>
    public static IReadOnlyList<(int Quarter, string Text)> Quarters { get; } =
    [
        (1, "ไตรมาสที่ 1 (ม.ค. – มี.ค.)"), (2, "ไตรมาสที่ 2 (เม.ย. – มิ.ย.)"), (3, "ไตรมาสที่ 3 (ก.ค. – ก.ย.)"), (4, "ไตรมาสที่ 4 (ต.ค. – ธ.ค.)"),
    ];

    public const int BuddhistOffset = 543;

    public static int ToBuddhist(int christianYear) => christianYear + BuddhistOffset;

    public static int ToChristian(int buddhistYear) => buddhistYear - BuddhistOffset;

    /// <summary>ผลตรวจรับ: ผ่าน is the default; it is kept as the same yes/no flag as before (Failed = ไม่ผ่าน, BR-032/035), so no migration.</summary>
    public static IReadOnlyList<(bool Failed, string Text)> InspectionResults { get; } = [(false, "ผ่าน"), (true, "ไม่ผ่าน")];

    public const bool DefaultFailed = false;
}

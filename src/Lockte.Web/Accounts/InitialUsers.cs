namespace Lockte.Web.Accounts;

/// <summary>One user to create: user name, display name and the programs ticked for it.</summary>
public sealed record InitialUser(string UserName, string DisplayName, IReadOnlyList<string> Programs);

/// <summary>
/// ผู้ใช้งานจริงชุดแรก (ADR 0005) ตามใบ "จำนวนคนใช้ program" ที่ผู้ใช้ส่งมา 2026-11-24: office 1–8, factory 9–18. แถว "สำรอง"
/// (7, 8, 17, 18) สร้างไว้แต่ยังไม่มีสิทธิ์เข้าโปรแกรมใด — admin ติ๊กและแก้ชื่อที่แสดงภายหลัง. ไม่มีใครได้ "ดูข้อมูลค่าจ้างได้"
/// ตั้งแต่แรก admin ติ๊กเอง. Created by <c>Lockte.Web seed-users</c> (<see cref="SeedUsersCommand"/>).
/// </summary>
public static class InitialUsers
{
    private const string Hr = "hr", Order = "order", Purch = "purch", Calib = "calib", Cash = "cash";
    private static readonly string[] All = [Hr, Order, Purch, Cash, Calib];
    private static readonly string[] None = [];
    private static readonly string[] Qa = [Order, Calib];

    /// <summary>Row n of the sheet is user{n:00}: two digits, so it never clashes with the test users user1 / user2.</summary>
    private static InitialUser Row(int number, string displayName, IReadOnlyList<string> programs)
        => new($"user{number:00}", displayName, programs);

    public static IReadOnlyList<InitialUser> Sheet { get; } =
    [
        // office
        Row(1, "au", All),
        Row(2, "aoe", All),
        Row(3, "ploy", All),
        Row(4, "p'hem", All),
        Row(5, "p'su", [Order]),
        Row(6, "p'jean", [Order]),
        Row(7, "สำรอง", None), // "account" crossed out on the sheet
        Row(8, "สำรอง", None),
        // factory
        Row(9, "คุณนิวาส", All),
        Row(10, "แต๋ว", [Hr, Order, Purch]),
        Row(11, "นก", [Hr, Order, Purch, Cash]),
        Row(12, "บอม", [Order, Purch]),
        Row(13, "QA1", Qa),
        Row(14, "QA2", Qa),
        Row(15, "QA3", Qa),
        Row(16, "QA4", Qa),
        Row(17, "สำรอง", None),
        Row(18, "สำรอง", None),
    ];
}

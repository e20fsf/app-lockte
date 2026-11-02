namespace Lockte.Web.Menus;

/// <summary>
/// โครงเมนูเดิมของ <c>เงินสดย่อย.accdb</c> (Switchboard) ตาม <c>specs/เงินสดย่อย/screens.md</c> "แผนผังเมนู" (SCR-01) — same pages, order and
/// "กลับเมนูเดิม" targets (ทุกเมนูย่อย → เมนูหลัก). Page numbers are the legacy Switchboard numbers (หลัก 1, พิมพ์ใบเสร็จ 2, พิมพ์รายงาน 3). Left out as decided in
/// #227/#228: ลบข้อมูลปีที่แล้ว (SCR-13, BR-020), ย้ายข้อมูลกลับ (SCR-14, BR-021), จบโปรแกรม — ข้อมูลทุกปีอยู่ในฐานข้อมูลเดียว.
/// Labels use the terms of CONTEXT.md (หมวด "เงินสดย่อยและของเก่า"): the legacy "พิมพ์ตัดยอดส่งออฟฟิสและยอดคงค้าง" is spelt
/// "พิมพ์ตัดยอดนำส่งสำนักงานและยอดรอนำส่ง", and "พิมพ์ใบเสร็จ,แก้ไขและลบข้อมูล" is "พิมพ์ใบเสร็จ แก้ไขและยกเลิก" (a receipt is cancelled, not deleted — BR-017).
/// SCR-05 is three screens of the legacy (one date-range form each for RPT-02, RPT-04, RPT-09) and SCR-07/SCR-08 each an add and an edit page, so their codes carry a suffix.
/// Every item is a placeholder until the ticket that builds its screen sets <c>Page</c>.
/// </summary>
public static class CashMenu
{
    public static string HrefOf(int number) => Menu.HrefOf(number);

    // The legacy Switchboard numbers of the menu pages.
    public const int Receipts = 2;
    public const int Reports = 3;

    public static ProgramMenu Menu { get; } = new("cash", "เงินสดย่อย", "cash", "cash/menu", "cash",
    [
        new(1, "เมนูหลัก", null,
        [
            new("เพิ่ม,แก้ไข,ลบและพิมพ์รายการค่าใช้จ่าย", Code: "SCR-02"),
            new("บันทึกค่าใช้จ่ายเงินสดย่อย", Code: "SCR-03"),
            new("แก้ไข,ลบบันทึกค่าใช้จ่ายเงินสดย่อยและพิมพ์ใบสำคัญจ่ายเงิน", Code: "SCR-04"),
            new("พิมพ์สรุปค่าใช้จ่ายเงินสดย่อย", Code: "SCR-05-expense"),
            new("พิมพ์รายการซื้อของ", SubMenu: Reports),
            new("พิมพ์ใบเสร็จรับเงินขายของเก่า", SubMenu: Receipts),
        ]),
        new(Receipts, "พิมพ์ใบเสร็จรับเงินขายของเก่า", 1,
        [
            new("เพิ่มข้อมูลผู้ซื้อ", Code: "SCR-07-add"),
            new("แก้ไขรายชื่อผู้ซื้อ", Code: "SCR-07-edit"),
            new("เพิ่มรายชื่อของเก่า", Code: "SCR-08-add"),
            new("แก้ไขรายชื่อของเก่า", Code: "SCR-08-edit"),
            new("พิมพ์ใบเสร็จ แก้ไขและยกเลิก", Code: "SCR-09"), // พิมพ์ใบเสร็จ,แก้ไขและลบข้อมูล
            new("พิมพ์ตัดยอดนำส่งสำนักงานและยอดรอนำส่ง", Code: "SCR-12"), // พิมพ์ตัดยอดส่งออฟฟิสและยอดคงค้าง
            new("รายงานยอดขายของเก่า", Code: "SCR-05-scrap"),
        ]),
        new(Reports, "พิมพ์รายงาน", 1,
        [
            new("พิมพ์รายงานการซื้อของ", Code: "SCR-05-purchases"),
            new("พิมพ์สรุปประจำเดือน", Code: "SCR-06"),
        ]),
    ]);
}

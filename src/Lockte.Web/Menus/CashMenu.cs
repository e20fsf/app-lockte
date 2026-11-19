using MudBlazor;
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

    /// <summary>
    /// ธนบัตรซ้อนมี ฿ ตรงกลาง: Material Icons has no baht sign, so this is <c>Payments</c> with the coin swapped for a ฿ cut out
    /// of the front note — drawn as paths (even-odd), not text, so it looks the same whatever fonts the browser has.
    /// </summary>
    public const string BahtNotesIcon =
        "<path d=\"M0 0h24v24H0z\" fill=\"none\"/>" +
        "<path fill-rule=\"evenodd\" d=\"M17 4H3c-1.1 0-2 .9-2 2v8c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2z" +
        "M7.5 7H11c1.4 0 2 .8 2 1.7 0 .6-.3 1.1-.8 1.3.7.2 1.1.8 1.1 1.5 0 .9-.7 1.5-2 1.5H7.5z" +
        "M9 8.2h1.9c.5 0 .7.3.7.65s-.2.65-.7.65H9z" +
        "M9 10.6h2c.6 0 .9.3.9.7s-.3.6-.9.6H9z" +
        "M9.7 5.8h.8V7h-.8z M9.7 8.2h.8v1.3h-.8z M9.7 10.6h.8v1.3h-.8z M9.7 13h.8v1.2h-.8z\"/>" +
        "<path d=\"M23 7v11c0 1.1-.9 2-2 2H4v-2h17V7h2z\"/>";

    public static ProgramMenu Menu { get; } = new("cash", "เงินสดย่อย", BahtNotesIcon, "cash", "cash/menu", "cash",
    [
        new(1, "เมนูหลัก", null,
        [
            new("เพิ่ม,แก้ไข,ลบและพิมพ์รายการค่าใช้จ่าย", Code: "SCR-02", Page: "cash/expense-types"), // built (#229)
            new("บันทึกค่าใช้จ่ายเงินสดย่อย", Code: "SCR-03", Page: "cash/expenses"), // built (#232)
            new("แก้ไข,ลบบันทึกค่าใช้จ่ายเงินสดย่อยและพิมพ์ใบสำคัญจ่ายเงิน", Code: "SCR-04", Page: "cash/expenses/edit"), // built (#233)
            new("พิมพ์สรุปค่าใช้จ่ายเงินสดย่อย", Code: "SCR-05-expense", Page: "cash/ranges/expense"), // built (#242)
            new("พิมพ์รายการซื้อของ", SubMenu: Reports),
            new("พิมพ์ใบเสร็จรับเงินขายของเก่า", SubMenu: Receipts),
        ]),
        new(Receipts, "พิมพ์ใบเสร็จรับเงินขายของเก่า", 1,
        [
            new("เพิ่มข้อมูลผู้ซื้อ", Code: "SCR-07-add", Page: "cash/scrap-buyers/new"), // built (#230)
            new("แก้ไขรายชื่อผู้ซื้อ", Code: "SCR-07-edit", Page: "cash/scrap-buyers"), // built (#230)
            new("เพิ่มรายชื่อของเก่า", Code: "SCR-08-add", Page: "cash/scrap-items/new"), // built (#230)
            new("แก้ไขรายชื่อของเก่า", Code: "SCR-08-edit", Page: "cash/scrap-items"), // built (#230)
            new("พิมพ์ใบเสร็จ แก้ไขและยกเลิก", Code: "SCR-09", Page: "cash/receipts"), // พิมพ์ใบเสร็จ,แก้ไขและลบข้อมูล
            new("พิมพ์ตัดยอดนำส่งสำนักงานและยอดรอนำส่ง", Code: "SCR-12", Page: "cash/remit"), // พิมพ์ตัดยอดส่งออฟฟิสและยอดคงค้าง
            new("รายงานยอดขายของเก่า", Code: "SCR-05-scrap", Page: "cash/ranges/scrap"), // built (#244)
        ]),
        new(Reports, "พิมพ์รายงาน", 1,
        [
            new("พิมพ์รายงานการซื้อของ", Code: "SCR-05-purchases", Page: "cash/ranges/purchases"), // built (#243)
            new("พิมพ์สรุปประจำเดือน", Code: "SCR-06"),
        ]),
    ]);
}

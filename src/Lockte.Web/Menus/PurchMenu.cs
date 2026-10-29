namespace Lockte.Web.Menus;

/// <summary>
/// โครงเมนูเดิมของ <c>วัตถุดิบ.accdb</c> (Switchboard) ตาม <c>specs/วัตถุดิบ/screens.md</c> "แผนผังเมนู" — same pages, order and
/// "กลับเมนูเดิม" targets. Page numbers are the legacy Switchboard numbers (หลัก 1, ผู้ขาย 9, วัตถุดิบ 10, จัดซื้อ 3,
/// รับวัตถุดิบและรายงาน 8, รับวัตถุดิบ 5, รายงาน 2, บันทึก/ประเมิน 7, สต็อก 11). Labels use the <c>CONTEXT.md</c> terms where
/// the legacy ones are listed as _Avoid_ (STOCK → สต็อก, ค้างส่ง → ยอดค้างรับ); the legacy label is in the comment beside each
/// renamed one. Left out as decided in #211/#212: ลบข้อมูลปีที่แล้ว (SCR-19), จบโปรแกรม, and everything no menu item opens (Q-02).
/// Every item is a placeholder until the ticket that builds its screen sets <c>Page</c>.
/// </summary>
public static class PurchMenu
{
    public static string HrefOf(int number) => Menu.HrefOf(number);

    public static ProgramMenu Menu { get; } = new("purch", "วัตถุดิบ", "purch", "purch/menu", "purch",
    [
        new(1, "เมนูหลัก", null,
        [
            new("ข้อมูลผู้ขาย", SubMenu: 9), // ขัอมูลผู้ขาย (สะกดตามต้นฉบับ)
            new("ข้อมูลวัตถุดิบ", SubMenu: 10),
            new("พิมพ์ใบขออนุมัติสั่งซื้อและใบสั่งซื้อ", SubMenu: 3),
            new("รับวัตถุดิบและรายงาน", SubMenu: 8),
            new("สต็อกวัตถุดิบ", SubMenu: 11), // STOCK วัตถุดิบ
            new("ตั้งค่าจัดซื้อ", Code: "SETTINGS", Page: "purch/settings"), // not in the legacy: the values it hard-coded (#212)
        ]),
        new(9, "ข้อมูลผู้ขาย", 1,
        [
            new("เพิ่มข้อมูลผู้ขาย", Code: "SCR-02-1", Page: "purch/suppliers/new"), // เพิ่มขัอมูลผู้ขาย
            new("แก้ไข, พิมพ์รายชื่อผู้ขายและพิมพ์ความมุ่งมั่น", Code: "SCR-02-2", Page: "purch/suppliers"),
        ]),
        new(10, "ข้อมูลวัตถุดิบ", 1,
        [
            new("เพิ่มรายชื่อวัตถุดิบ", Code: "SCR-17-1", Page: "purch/materials/new"),
            new("แก้ไขและพิมพ์รายชื่อวัตถุดิบ", Code: "SCR-17-2", Page: "purch/materials"),
        ]),
        new(3, "ใบขออนุมัติสั่งซื้อและใบสั่งซื้อ", 1,
        [
            new("พิมพ์ใบขออนุมัติสั่งซื้อ", Code: "SCR-03", Page: "purch/requests/new"),
            new("เพิ่ม, แก้ไขและลบใบขออนุมัติสั่งซื้อ", Code: "SCR-06", Page: "purch/requests"),
            new("พิมพ์ใบสั่งซื้อ", Code: "SCR-05", Page: "purch/orders/new"),
            new("แก้ไขและลบใบสั่งซื้อ", Code: "SCR-07", Page: "purch/orders"),
            new("เพิ่มหรือแก้ไขเลขที่ใบขออนุมัติสั่งซื้อ", Code: "SCR-08", Page: "purch/requests"),
            new("เพิ่มหรือแก้ไขเลขที่ใบสั่งซื้อ", Code: "SCR-09", Page: "purch/orders"),
            new("เพิ่ม, แก้ไขและลบรายชื่อแผนก", Code: "SCR-10", Page: "purch/departments"),
        ]),
        new(8, "รับวัตถุดิบและรายงาน", 1,
        [
            new("รับวัตถุดิบ", SubMenu: 5),
            new("รายงานการรับวัตถุดิบ", SubMenu: 2),
            new("รับวัตถุดิบหลัก", Code: "SCR-20-1"),
            new("รับสินค้าซื้อมาขาย", Code: "SCR-20-2"), // รับสินค้าซื้อมาขายไป
        ]),
        new(5, "รับวัตถุดิบ", 8,
        [
            new("จัดซื้อรับวัตถุดิบ", Code: "SCR-11", Page: "purch/receipts/new"),
            new("แก้ไขและลบการรับวัตถุดิบ", Code: "SCR-12"),
            new("QA รับวัตถุดิบ", Code: "SCR-13"),
        ]),
        new(2, "รายงานการรับวัตถุดิบ", 8,
        [
            new("ใบขออนุมัติสั่งซื้อที่ยังไม่ได้พิมพ์ใบสั่งซื้อ", Code: "RPT-03"),
            new("รายงานวัตถุดิบที่ยังไม่ได้รับ", Code: "RPT-04"), // ยอดค้างรับ
            new("พิมพ์บันทึกการรับวัตถุดิบ, แบบการประเมินผู้ขายและสรุป", SubMenu: 7),
            new("พิมพ์รายงานการรับวัตถุดิบแต่ละแผนก", Code: "RPT-12"),
            new("พิมพ์รายงานการรับวัตถุดิบตามชื่อ", Code: "RPT-15"),
            new("พิมพ์รายงานการรับวัตถุดิบตามชื่อผู้ขาย", Code: "RPT-18"),
            new("พิมพ์สรุปการรับวัตถุดิบ", Code: "RPT-20"),
        ]),
        new(7, "บันทึกการรับวัตถุดิบ, แบบการประเมินผู้ขายและสรุป", 2,
        [
            new("พิมพ์บันทึกการรับของ", Code: "RPT-05"),
            new("พิมพ์บันทึกการทำ INCOMING INSPECTION", Code: "RPT-06"),
            new("พิมพ์แบบผลการประเมินติดตามผู้ขายประจำไตรมาส", Code: "RPT-07"),
            new("พิมพ์แบบผลการประเมินผู้ขายประจำปี", Code: "RPT-08"),
            new("พิมพ์สรุปการประเมินผู้ขาย", Code: "RPT-09"),
            new("พิมพ์บันทึกการส่งอลูมิเนียมไปหลอม", Code: "RPT-10"),
            new("พิมพ์ใบสั่งซื้อต่ำกว่า 5000 บาท", Code: "RPT-11"),
        ]),
        new(11, "สต็อกวัตถุดิบ", 1, // STOCK วัตถุดิบ
        [
            new("เบิกและแก้ไขใบเบิกวัตถุดิบ", Code: "SCR-14", Page: "purch/stock/issue"),
            new("พิมพ์สต็อกการ์ดวัตถุดิบ", Code: "RPT-14"), // STOCK CARD
            new("ตัดสต็อกวัตถุดิบ", Code: "SCR-18"), // ตัด STOCK วัตถุดิบ
            new("แก้ไขหรือเพิ่มสต็อกวัตถุดิบ", Code: "SCR-16", Page: "purch/stock/entries"), // แก้ไขหรือเพิ่ม STOCK วัตถุดิบ
        ]),
    ]);
}

namespace Lockte.Web.Menus;

/// <summary>
/// โครงเมนูเดิมของ <c>ผลผลิตและORDER.accdb</c> (Switchboard) ตาม <c>specs/ผลผลิตและORDER/screens.md</c> "แผนผังเมนู" —
/// same pages, order and "กลับเมนูเดิม" targets, including oddities (เมนู 14 lists the departments in another order than
/// เมนู 12 and returns to เมนู 15). Labels use the <c>CONTEXT.md</c> terms where the legacy ones are listed as _Avoid_
/// (เซล → เขตการขาย, ส่งออก → การส่งของ, STOCK → สต็อกสินค้า, พิมพ์ → แม่พิมพ์, SALE ORDER → ORDER); the legacy label is in
/// the comment beside each renamed one. Left out as decided in #27/#102: ลบข้อมูลปีที่แล้ว (SCR-22), ย้ายข้อมูลผลผลิต
/// (BR-019), NCR (SCR-20), จบโปรแกรม, and everything no menu item opens.
/// </summary>
public static class OrderMenu
{
    public static string HrefOf(int number) => Menu.HrefOf(number);

    public static ProgramMenu Menu { get; } = new("order", "ผลผลิตและ ORDER", "order", "order/menu", "order",
    [
        new(1, "เมนูหลัก", null,
        [
            new("ข้อมูลลูกค้า", SubMenu: 7),
            new("ข้อมูลสินค้า", SubMenu: 8),
            new("ORDER ลูกค้าและเขตการขาย", SubMenu: 2), // ORDERลูกค้าและเซล
            new("ผลผลิต", SubMenu: 3),
            new("แม่พิมพ์และรหัสเครื่องจักร", SubMenu: 6),
            new("QA พิมพ์รายงานการตรวจสอบ", SubMenu: 15), // QAพิมพ์รายงานการตรวจสอบ
        ]),
        new(7, "ข้อมูลลูกค้า", 1,
        [
            new("เพิ่มข้อมูลลูกค้า", Code: "SCR-02-1", Page: "order/customers/new"),
            new("แก้ไขและพิมพ์ข้อมูลลูกค้า", Code: "SCR-02-2", Page: "order/customers"),
        ]),
        new(8, "ข้อมูลสินค้า", 1,
        [
            new("เพิ่มข้อมูลสินค้า", Code: "SCR-03-1", Page: "order/products/new"),
            new("แก้ไขและพิมพ์ข้อมูลสินค้า", Code: "SCR-03-2", Page: "order/products"),
        ]),
        new(2, "ORDER", 1,
        [
            new("รับ ORDER และพิมพ์สรุปการรับ", SubMenu: 17),
            new("แก้ไข, ลบ ORDER และเขตการขาย", SubMenu: 9), // แก้ไข,ลบ ORDERและเซล
            new("ยอดค้างส่ง", SubMenu: 13), // ORDER ค้างส่ง
            new("กำหนดส่ง (พิมพ์รายงานให้สโตร์)", Code: "RPT-05", Page: "order/delivery-reports/schedule"),
            new("บันทึกการส่งของ", Code: "SCR-08", Page: "order/deliveries/record"), // บันทึกการส่งออก
            new("รายงานการส่งของ", SubMenu: 5), // รายงานการส่งออก
            new("แก้ไขรายการส่งของ", SubMenu: 10), // แก้ไขรายการส่งออก
        ]),
        new(17, "รับ ORDER", 2, // แยก SALE ORDER
        [
            new("ORDER ใหม่ เลขที่ออกอัตโนมัติ", Code: "SCR-04", Page: "order/orders/new"), // SALE ORDER ใหม่เลขที่เปลี่ยนอัตโนมัติ
            new("ORDER เก่าหรือใบ ORDER ของลูกค้า", Code: "SCR-05", Page: "order/orders/own"), // SALE ORDER เก่าหรือใบ ORDER ของลูกค้า
            new("พิมพ์สรุปการรับ ORDER", Code: "RPT-06-1", Page: "order/reports/receipts-summary"),
            new("รายงานการรับ ORDER ของแต่ละเขตการขาย", Code: "RPT-06-2", Page: "order/reports/receipts-territory"), // รายงานการรับ ORDER ของเซลแต่ละคน
            new("รายงานการรับ ORDER ตามชื่อลูกค้า", Code: "RPT-06-3", Page: "order/reports/receipts-customer"),
        ]),
        new(9, "แก้ไขและลบ ORDER", 2,
        [
            new("เลือกตามเลขที่ ORDER", Code: "SCR-06-1", Page: "order/orders/edit/number"),
            new("เลือกตามวันที่ ORDER", Code: "SCR-06-2", Page: "order/orders/edit/date"),
            new("เลือกตามรหัสลูกค้า", Code: "SCR-06-3", Page: "order/orders/edit/customer"),
            new("แก้ไข ORDER ที่เขตการขายรับมาทั้งหมด", Code: "SCR-06-4", Page: "order/orders/edit/history"), // แก้ไข ORDER ที่เซลรับมาทั้งหมด
            new("เพิ่มเขตการขาย", Code: "SCR-07-1", Page: "order/territories/new"), // เพิ่มรายชื่อเซล
            new("แก้ไขและลบเขตการขาย", Code: "SCR-07-2", Page: "order/territories"), // แก้ไขและลบรายชื่อเซล
        ]),
        new(13, "ยอดค้างส่ง", 2, // ORDER ค้างส่ง
        [
            new("ยอดค้างส่งทั้งหมด", Code: "RPT-02", Page: "order/reports/backorder"), // ORDER ค้างส่งทั้งหมด
            new("ยอดค้างส่งรวม", Code: "RPT-03", Page: "order/reports/backorder-products"), // ORDER ค้างส่งรวม
            new("ยอดค้างส่งตามลูกค้า", Code: "RPT-04-1", Page: "order/reports/backorder-customers"), // ORDER ค้างส่งตามลูกค้า
            new("ยอดค้างส่งตามรุ่น", Code: "RPT-04-2", Page: "order/reports/backorder-models"), // ORDER ค้างส่งตามรุ่น
            new("ยอดค้างส่งของเขตการขาย", Code: "RPT-04-3", Page: "order/reports/backorder-territory"), // ORDER ค้างส่งของเซล
            new("สินค้าที่ลูกค้าสั่งซื้อ", Code: "RPT-04-4", Page: "order/reports/ordered-products"),
            new("ยอดค้างส่งของลูกค้า", Code: "RPT-04-5", Page: "order/reports/backorder-product-customers"), // ORDERค้างส่งของลูกค้า
        ]),
        new(5, "รายงานการส่งของ", 2, // รายงานการส่งออก
        [
            new("การส่งของตามวันที่กำหนด", Code: "RPT-07", Page: "order/delivery-reports/deliveries"), // ส่งออกตามวันที่กำหนด
            new("การส่งของตามลูกค้าและเขตการขาย", Code: "RPT-08-1"), // ส่งออกตามลูกค้าและSALE
            new("การส่งของตามสินค้า", Code: "RPT-08-2"), // ส่งออกตามสินค้า
            new("การส่งของตามชนิดสินค้า", Code: "RPT-08-3"), // ส่งออกตามชนิดสินค้า
            new("สรุปการส่งของตามลูกค้า", Code: "RPT-08-4"), // สรุปการส่งออกตามลูกค้า
            new("รายงานส่งผู้จัดการโรงงาน", Code: "RPT-08-5", Page: "order/delivery-reports/manager"),
            new("รายงานการส่งของตามจังหวัด", Code: "RPT-08-6"), // รายงานส่งออกตามจังหวัด
        ]),
        new(10, "แก้ไขรายการส่งของ", 2, // แก้ไขรายการส่งออก
        [
            new("แก้ไขตามเลขที่บิล", Code: "SCR-09-1", Page: "order/deliveries/edit/invoice"),
            new("แก้ไขตามเลขที่ใบ ORDER", Code: "SCR-09-2", Page: "order/deliveries/edit/order"),
            new("แก้ไขตามวันที่", Code: "SCR-09-3", Page: "order/deliveries/edit/date"),
        ]),
        new(3, "ผลผลิต", 1,
        [
            new("บันทึกผลผลิต", SubMenu: 12),
            // The legacy Switchboard Items row points at เมนู 14 (QA), leaving เมนู 11 unreachable; the user decided
            // (#103) to follow screens.md and #121: เมนู 11.
            new("แก้ไขผลผลิต", SubMenu: 11),
            new("รายงานผลผลิตและพิมพ์ใบโอน", Code: "RPT-10"),
            new("เบิกเพื่อผลิต, แก้ไขใบเบิกงานระหว่างผลิตและพิมพ์รายงาน", Code: "SCR-13", Page: "order/stock/withdrawals"), // เบิกเพื่อผลิต,แก้ไขใบเบิกและพิมพ์รายงาน
            new("สต็อกสินค้าคงเหลือและโอนผลผลิต", SubMenu: 4), // STOCK คงเหลือโอนผลผลิตและNCR (NCR left out)
            new("ตัดสต็อกสินค้าสิ้นเดือน", SubMenu: 16), // ตัดSTOCKสิ้นเดือน
            new("เพิ่มและแก้ไขสต็อกสินค้า", Code: "SCR-14", Page: "order/stock/entries"), // เพิ่มและแก้ไขSTOCK
        ]),
        new(12, "บันทึกผลผลิต", 3,
        [
            new("บันทึกผลผลิตแผนกไดคาสท์", Code: "SCR-10-1", Page: "order/production/report/D"),
            new("บันทึกผลผลิตแผนกตกแต่ง", Code: "SCR-10-2", Page: "order/production/report/T"),
            new("บันทึกผลผลิตแผนกอัดผ้าเบรก", Code: "SCR-10-3", Page: "order/production/report/B"),
            new("บันทึกผลผลิตแผนกฝนผ้าเบรก", Code: "SCR-10-4", Page: "order/production/report/G"),
            new("บันทึกผลผลิตแผนกประกอบ", Code: "SCR-10-5", Page: "order/production/report/A"),
            new("บันทึกผลผลิตแผนกบรรจุ", Code: "SCR-10-6", Page: "order/production/report/P"),
            new("บันทึกผลผลิตแผนกผ้าคลัทช์และดิสเบรก", Code: "SCR-10-7", Page: "order/production/report/K"), // บันทีกผลผลิต… (typo)
        ]),
        new(11, "แก้ไขผลผลิต", 3,
        [
            new("แก้ไขตามเลขที่ใบรายงาน", Code: "SCR-11-1"),
            new("แก้ไขตามวันที่", Code: "SCR-11-2"),
        ]),
        new(4, "สต็อกสินค้าคงเหลือ", 3, // STOCK คงเหลือ
        [
            new("สต็อกสินค้าตามรหัสสินค้า", Code: "RPT-12", Page: "order/stock/reports/card"), // STOCK ตามรหัสสินค้า
            new("สต็อกสินค้าตามกลุ่มสินค้า", Code: "RPT-11", Page: "order/stock/reports/group"), // STOCK กลุ่มสินค้า
            new("โอนผลผลิตเข้าสต็อกสินค้า", Code: "SCR-16", Page: "order/stock/transfer"), // โอนผลผลิตเข้าSTOCK
            new("พิมพ์สรุปสต็อกสินค้า", Code: "RPT-14"), // พิมพ์สรุปSTOCK
        ]),
        new(16, "ตัดสต็อกสินค้า", 3, // ตัดSTOCK
        [
            new("ตัดสต็อกสินค้าครั้งแรก", Code: "SCR-15-1"), // ตัด STOCK ครั้งแรก
            new("ตัดสต็อกสินค้าซ้ำ (ต้องมีการโอนผลผลิตเข้าสต็อกสินค้าก่อน)", Code: "SCR-15-2"), // ตัด STOCK ซ้ำ(ต้องมีการโอนผลผลิตเข้าSTOCKก่อน)
        ]),
        new(6, "รหัสแม่พิมพ์", 1, // รหัสพิมพ์
        [
            new("เพิ่มรหัสแม่พิมพ์", Code: "SCR-17-1", Page: "order/molds/new"),
            new("แก้ไขและพิมพ์รหัสแม่พิมพ์", Code: "SCR-17-2", Page: "order/molds"),
            new("ตัดยอดแม่พิมพ์", Code: "SCR-18"), // ตัดSTOCKแม่พิมพ์
            new("รายงานยอดแม่พิมพ์", Code: "RPT-15"), // รายงานSTOCKแม่พิมพ์
            new("รหัสเครื่องจักร", Code: "SCR-19", Page: "order/machines"),
        ]),
        new(15, "QA พิมพ์รายงานการตรวจสอบ", 1, // QA1
        [
            new("ผลผลิต", SubMenu: 14),
            new("แม่พิมพ์", Code: "RPT-20"),
            new("สินค้าที่ส่งของ", Code: "RPT-07-1", Page: "order/delivery-reports/deliveries"), // สินค้าส่งออก (the same form as เมนู 5 "ส่งออกตามวันที่กำหนด")
        ]),
        new(14, "QA ผลผลิต", 15, // QA
        [
            new("ไดคาสท์", Code: "SCR-12-1"),
            new("ตกแต่ง", Code: "SCR-12-2"),
            new("อัดผ้าเบรก", Code: "SCR-12-3"),
            new("ฝนผ้าเบรก", Code: "SCR-12-4"),
            new("ประกอบ", Code: "SCR-12-5"),
            new("ผ้าคลัทช์และดิสเบรก", Code: "SCR-12-6"),
            new("บรรจุ", Code: "SCR-12-7"),
        ]),
    ]);
}

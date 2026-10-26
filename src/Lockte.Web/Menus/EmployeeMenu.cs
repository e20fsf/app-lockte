namespace Lockte.Web.Menus;

/// <summary>
/// โครงเมนูเดิมของ <c>พนักงาน.accdb</c> (Switchboard) ตาม <c>specs/employee/screens.md</c> "แผนผังเมนู" — same pages,
/// order and "กลับเมนูเดิม" targets, including ⚠ oddities (เมนูหลัก "เวลาทำงาน" opens เมนู 7, เมนู 3 returns to เมนู 7).
/// Labels use the <c>CONTEXT.md</c> terms where the legacy ones are listed as _Avoid_ (ตัด WEEK → ปิดงวดค่าแรง,
/// บรรจุ → ผ่านทดลองงาน); the legacy labels stay in screens.md.
/// Left out as decided in #41/#14: ย้ายข้อมูล (SCR-60), ลบข้อมูลปีที่แล้ว (SCR-62), จบโปรแกรม, and menu pages 8 and 16
/// which no item opens.
/// </summary>
public static class EmployeeMenu
{
    public const int Main = ProgramMenu.Main;

    public static string HrefOf(int number) => Menu.HrefOf(number);

    // Menu pages stay at menu/n, as before the ORDER program joined the app; the main menu moved from "/" to "hr".
    public static ProgramMenu Menu { get; } = new("hr", "พนักงานและค่าแรง", "hr", "menu", "hr",
    [
        new(1, "เมนูหลัก", null,
        [
            new("ข้อมูลพนักงาน", SubMenu: 2),
            new("เวลาทำงาน", SubMenu: 7),
            new("การลางานและใบเตือน", SubMenu: 4),
            new("ปิดงวดค่าแรง", SubMenu: 12),
            new("พิมพ์ใบลางาน, ใบลาออกและทะเบียนผู้ประกันสังคม", SubMenu: 18),
            new("การประเมินผลพนักงาน", SubMenu: 11),
            // New (#52): the legacy typed the numbers into each report; ข้อมูลหลักเลขเอกสารควบคุม has no legacy menu item.
            new("เลขเอกสารควบคุม", Page: "document-numbers"),
        ]),
        new(2, "ข้อมูลพนักงาน", 1,
        [
            new("เพิ่มข้อมูลพนักงาน", Code: "SCR-02", Page: "hr/employees/new"),
            new("แก้ไขข้อมูลพนักงาน", Code: "SCR-03", Page: "hr/employees"),
            new("พนักงานยังไม่ผ่านทดลองงาน", Code: "RPT-20", Page: "reports/hr/employees/on-probation", NewTab: true),
            new("พิมพ์รายชื่อพนักงาน", Code: "SCR-04", Page: "hr/employees/lists"),
            new("เพิ่มและแก้ไขรายชื่อแผนก", Code: "SCR-05", Page: "org/departments"),
            new("พิมพ์หนังสือรับรอง, เกษียณและปลดออก", SubMenu: 19),
            new("พิมพ์ใบอนุมัติผ่านทดลองงาน", Code: "SCR-06", Page: "hr/employees/probation-approval"),
        ]),
        new(7, "วันอาทิตย์และวันหยุด", 1,
        [
            new("บันทึกการทำงาน", SubMenu: 3),
            new("พิมพ์ใบขออนุมัติทำล่วงเวลา", Code: "SCR-14", Page: "hr/time/overtime-sheet"),
            new("เพิ่มและแก้ไขวันอาทิตย์", Code: "SCR-15", Page: "hr/calendar/weekly"),
            new("เพิ่มและแก้ไขวันหยุด", Code: "SCR-16", Page: "hr/calendar/holidays"),
            new("รายงานทำล่วงเวลาจริง", Code: "RPT-14", Page: "hr/reports/overtime"),
            new("รายงานการมาทำงาน", Code: "RPT-15", Page: "hr/reports/attendance"),
        ]),
        new(3, "เวลาทำงาน", 7,
        [
            new("บันทึก, แก้ไขและพิมพ์รายงานพนักงานไม่มาทำงาน", Code: "SCR-10", Page: "hr/time/absences"),
            new("บันทึกเวลาทำงานให้ทุกคน", Code: "SCR-11", Page: "hr/time/normal"),
            new("บันทึกเวลาทำงาน, OT, วันอาทิตย์และวันหยุด", Code: "SCR-12", Page: "hr/time/entry"),
            new("แก้ไขและลบเวลาทำงาน", Code: "SCR-13", Page: "hr/time/edit"),
            new("รายงานพนักงานที่มาทำงานสาย", Code: "RPT-12", Page: "hr/reports/late"),
            new("รายงานสรุปชั่วโมงทำงาน", Code: "RPT-13", Page: "hr/reports/work-hours"),
        ]),
        new(4, "การลางานและใบเตือน", 1,
        [
            new("บันทึกและแก้ไขการลางาน", Code: "SCR-30", Page: "hr/leave"),
            new("พิมพ์รายงานการลางาน", SubMenu: 17),
            new("ลบข้อมูลการลางาน", Code: "SCR-31", Page: "hr/leave/delete"),
            new("บันทึกและแก้ไขใบเตือน", Code: "SCR-32", Page: "hr/warnings"),
            new("พิมพ์รายงานใบเตือน", Code: "RPT-30", Page: "hr/reports/warnings"),
        ]),
        new(17, "พิมพ์รายงานการลางาน", 4,
        [
            new("พิมพ์ลางานทั้งแผนก", Code: "RPT-31", Page: "hr/reports/leave-department"),
            new("พิมพ์ลางานรายบุคคล", Code: "RPT-32", Page: "hr/reports/leave-employee"),
            new("พิมพ์สรุปการลางาน", Code: "RPT-33", Page: "hr/reports/leave-summary"),
            new("พิมพ์พนักงานขาดงาน", Code: "RPT-34", Page: "hr/reports/leave-absent"),
        ]),
        new(12, "ปิดงวดค่าแรง", 1,
        [
            new("ปิดงวดค่าแรงตามปกติ", SubMenu: 6),
            new("ปิดงวดค่าแรงเป็น 2 ช่วง", SubMenu: 13),
            new("วิธีการทำเงินเดือนส่งแบงค์ทหารไทย", Code: "RPT-06-TTB"),
            new("วิธีการทำเงิน OT ส่งแบงค์กสิกรไทย", Code: "RPT-06-KBANK"),
        ]),
        new(6, "ปิดงวดค่าแรงตามปกติ", 12,
        [
            new("พิมพ์เวลาทำงาน", Code: "SCR-40", Page: "hr/pay-periods"),
            new("พิมพ์ค่าแรง", Code: "SCR-41"),
            new("พิมพ์สรุปค่าแรง", SubMenu: 9),
            new("พิมพ์ค่าแรงส่งแบงค์", Code: "SCR-42"),
            new("พิมพ์ SLIP", Code: "RPT-04"),
            new("พิมพ์ค่าแรงสิ้นเดือนและสรุป", Code: "SCR-43", Page: "hr/reports/pay/month-end"),
            new("คำนวณโบนัสและปรับค่าแรง", SubMenu: 10),
        ]),
        new(9, "พิมพ์สรุปค่าแรง", 6,
        [
            new("ค่าแรงทั่วไป", Code: "RPT-03"),
            new("ค่าแรงงานรับจ้าง", Code: "SCR-44", Page: "hr/pay/contract-labor"),
            new("สรุปค่าแรงแต่ละเดือน", Code: "RPT-09", Page: "hr/reports/pay/monthly"),
            new("รวมค่าแรง", Code: "RPT-10", Page: "hr/reports/pay/totals"),
        ]),
        new(10, "คำนวณโบนัสและพักร้อน", 6,
        [
            new("คำนวณโบนัส", Code: "SCR-50"),
            new("คำนวณพักร้อน", Code: "SCR-51", Page: "hr/vacation-pay"),
            new("ปรับค่าแรง", Code: "SCR-52", Page: "hr/pay-adjustment"),
            new("พิมพ์ค่าแรงรวมโบนัสส่งแบงค์ (ทหารไทย)", Code: "RPT-40"),
            new("พิมพ์เงิน OT รวมโบนัสส่งแบงค์ (กสิกร)", Code: "RPT-41"),
            new("พิมพ์ SLIP รวมโบนัส", Code: "RPT-42"),
        ]),
        new(13, "ปิดงวดค่าแรงเป็น 2 ช่วง", 12,
        [
            new("ปิดงวดค่าแรงช่วงที่ 1 (27–31 ธันวาคม ของทุกปี)", SubMenu: 14),
            new("ปิดงวดค่าแรงช่วงที่ 2 (1–11 มกราคม ของทุกปี)", SubMenu: 15),
        ]),
        new(14, "ปิดงวดค่าแรงช่วงที่ 1", 13,
        [
            new("พิมพ์เวลาทำงานช่วงที่ 1", Code: "SCR-40-1"),
            new("พิมพ์ค่าแรงช่วงที่ 1", Code: "SCR-45"),
            new("พิมพ์สรุปค่าแรงช่วงที่ 1", Code: "RPT-11-1"),
            new("พิมพ์ SLIP ช่วงที่ 1", Code: "RPT-04-1"),
        ]),
        new(15, "ปิดงวดค่าแรงช่วงที่ 2", 13,
        [
            new("พิมพ์เวลาทำงานช่วงที่ 2", Code: "SCR-40-2"),
            new("พิมพ์ค่าแรงช่วงที่ 2", Code: "SCR-46"),
            new("พิมพ์สรุปค่าแรงช่วงที่ 2", Code: "RPT-11-2"),
            new("พิมพ์ SLIP ช่วงที่ 2", Code: "RPT-04-2"),
            new("พิมพ์ค่าแรงส่งแบงค์ทั้ง 2 ช่วง", Code: "SCR-47"),
            new("พิมพ์สรุปค่าแรงทั้ง 2 ช่วง", Code: "RPT-11-12"),
        ]),
        new(11, "ประเมินผล", 1,
        [
            new("บันทึกการประเมินผล", Code: "SCR-53", Page: "hr/evaluations"),
            new("ประเมินผลประจำปี", Code: "SCR-54", Page: "hr/yearly-evaluation"),
        ]),
        new(18, "พิมพ์ใบลาและใบลาออก", 1,
        [
            new("พิมพ์ใบลา", Code: "RPT-50", Page: "hr/leave-form"),
            new("พิมพ์ใบลาออกและรายงาน", Code: "SCR-33", Page: "hr/resignation"),
            new("พิมพ์ทะเบียนผู้ประกันตน", Code: "RPT-51", Page: "reports/hr/insured-persons", NewTab: true),
            new("ประกาศการคำนวณพักร้อนพนักงานใหม่", Code: "RPT-52", Page: "reports/hr/vacation-notice", NewTab: true),
        ]),
        new(19, "พิมพ์หนังสือรับรอง", 2,
        [
            new("พิมพ์หนังสือรับรอง", Code: "RPT-53", Page: "hr/certificates"),
            new("พิมพ์หนังสือเกษียณหรือปลดออก", Code: "RPT-54", Page: "hr/severance"),
        ]),
    ]);
}

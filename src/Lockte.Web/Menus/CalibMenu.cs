namespace Lockte.Web.Menus;

/// <summary>
/// โครงเมนูเดิมของ <c>สอบเทียบ.accdb</c> (Switchboard) ตาม <c>specs/สอบเทียบ/screens.md</c> "แผนผังเมนู" (SCR-01) — same pages, order and
/// "กลับเมนูเดิม" targets (ภายนอก/ภายใน → เมนูหลัก, each type → ภายใน). Page numbers are the legacy Switchboard numbers
/// (หลัก 1, ภายนอก 2, ภายใน 3, เวอร์เนีย 4, เพรสเซอร์เกจ 5, เครื่องชั่ง 6, JIG 7, ตัวควบคุมอุณหภูมิ 8). Left out as decided in #191/#192:
/// จบโปรแกรม. The two labels the legacy misspelt ("สอบเทียบภานใน", "สอบเทียบภานนอก") are spelt right here.
/// Items that share one SCR/RPT across the six types carry the type as a suffix on their code. Every item is a placeholder until
/// the ticket that builds its screen sets <c>Page</c>.
/// </summary>
public static class CalibMenu
{
    public static string HrefOf(int number) => Menu.HrefOf(number);

    // The legacy Switchboard numbers of the menu pages.
    public const int External = 2;
    public const int Internal = 3;
    public const int Caliper = 4;
    public const int PressureGauge = 5;
    public const int Scale = 6;
    public const int Jig = 7;
    public const int TempControl = 8;

    public static ProgramMenu Menu { get; } = new("calib", "สอบเทียบ", "calib", "calib/menu", "calib",
    [
        new(1, "เมนูหลัก", null,
        [
            new("แผนการสอบเทียบ", Code: "RPT-01"),
            new("สอบเทียบภายนอก", SubMenu: External), // สอบเทียบภานนอก (สะกดตามต้นฉบับ)
            new("สอบเทียบภายใน", SubMenu: Internal), // สอบเทียบภานใน (สะกดตามต้นฉบับ)
        ]),
        new(External, "สอบเทียบภายนอก", 1,
        [
            new("เพิ่มเครื่องมือวัด", Code: "SCR-02-ext"),
            new("แก้ไขและลบเครื่องมือวัด", Code: "SCR-03-ext"),
            new("บัญชีรายชื่อเครื่องมือวัด", Code: "RPT-02-ext"),
            new("พิมพ์ฟอร์มบันทึกผลการสอบเทียบ", Code: "RPT-03-ext"),
            new("เพิ่มรายชื่อบริษัทที่สอบเทียบ", Code: "SCR-06"),
        ]),
        new(Internal, "สอบเทียบภายใน", 1,
        [
            new("เวอร์เนีย, ไฮเกจ, ไมโครมิเตอร์", SubMenu: Caliper),
            new("เพรสเซอร์เกจ", SubMenu: PressureGauge),
            new("เครื่องชั่ง", SubMenu: Scale),
            new("JIG", SubMenu: Jig),
            new("ตัวควบคุมอุณหภูมิ (TEMP CONTROL)", SubMenu: TempControl),
        ]),
        new(Caliper, "เวอร์เนีย, ไฮเกจ, ไมโครมิเตอร์", Internal,
        [
            new("เพิ่มรายชื่อเครื่องมือวัด", Code: "SCR-02-caliper", Page: "calib/instruments/caliper/new"),
            new("แก้ไขและลบเครื่องมือวัด", Code: "SCR-03-caliper", Page: "calib/instruments/caliper"),
            new("บัญชีรายชื่อเครื่องมือวัด", Code: "RPT-02-caliper"),
            new("พิมพ์บันทึกผลการสอบเทียบ", Code: "RPT-03-caliper"),
            new("พิมพ์สติกเกอร์", Code: "SCR-04-caliper"),
        ]),
        new(PressureGauge, "เพรสเซอร์เกจ", Internal,
        [
            new("เพิ่มรายชื่อเครื่องมือวัด", Code: "SCR-02-pg", Page: "calib/instruments/pg/new"),
            new("แก้ไขและลบรายชื่อเครื่องมือวัด", Code: "SCR-03-pg", Page: "calib/instruments/pg"),
            new("บัญชีรายชื่อเครื่องมือวัด", Code: "RPT-02-pg"),
            new("พิมพ์บันทึกการสอบเทียบ", Code: "RPT-03-pg"),
            new("พิมพ์สติกเกอร์", Code: "SCR-04-pg"),
        ]),
        new(Scale, "เครื่องชั่ง", Internal,
        [
            new("เพิ่มรายชื่อเครื่องชั่ง", Code: "SCR-02-scale", Page: "calib/instruments/scale/new"),
            new("แก้ไขและลบรายชื่อเครื่องชั่ง", Code: "SCR-03-scale", Page: "calib/instruments/scale"),
            new("บัญชีรายชื่อเครื่องชั่ง", Code: "RPT-02-scale"),
            new("พิมพ์บันทึกผลการสอบเทียบ", Code: "RPT-03-scale"),
            new("พิมพ์สติกเกอร์", Code: "SCR-04-scale"),
        ]),
        new(Jig, "JIG", Internal,
        [
            new("เพิ่มรายชื่อ JIG", Code: "SCR-02-jig"),
            new("แก้ไขและลบรายชื่อ JIG", Code: "SCR-03-jig"),
            new("บัญชีรายชื่อ JIG", Code: "RPT-02-jig"),
            new("พิมพ์บันทึกการตรวจสอบ JIG", Code: "SCR-05"),
            new("พิมพ์สติกเกอร์", Code: "SCR-04-jig"),
        ]),
        new(TempControl, "ตัวควบคุมอุณหภูมิ (TEMP CONTROL)", Internal,
        [
            new("เพิ่มรายชื่อตัวควบคุมอุณหภูมิ", Code: "SCR-02-tc", Page: "calib/instruments/tc/new"),
            new("แก้ไขและลบรายชื่อตัวควบคุมอุณหภูมิ", Code: "SCR-03-tc", Page: "calib/instruments/tc"),
            new("บัญชีรายชื่อตัวควบคุมอุณหภูมิ", Code: "RPT-02-tc"),
            new("พิมพ์บันทึกการสอบเทียบ", Code: "RPT-03-tc"),
            new("พิมพ์สติกเกอร์", Code: "SCR-04-tc"),
        ]),
    ]);
}

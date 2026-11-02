"""ส่งออก baseline ของบัญชีรายชื่อเครื่องมือวัด (RPT-02) จากสำเนา สอบเทียบ.accdb เป็น CSV สำหรับเทียบกับระบบใหม่ (สอบเทียบ #213)

ใช้: python -I -X utf8 export_calib_baseline.py <สำเนา.accdb> <โฟลเดอร์ผลลัพธ์>

- เปิดสำเนาแบบอ่านอย่างเดียวผ่าน DAO (ไม่เปิดฟอร์ม/รายงานของ Access — เปิดบน 64-bit ไม่ได้); ชื่อไฟล์ต้องมีคำว่า copy
- SQL ตรงกับ RecordSource ของรายงาน RPT-02 เดิม (reports.md): ทุกแถวของตารางชนิดนั้น เรียงตามหมายเลขเครื่อง (รวมแถวที่หมายเลขว่าง)
- ผลลัพธ์ calib-<ชนิด>.csv (ข้อมูลจริง: ชื่อผู้สอบเทียบ หมายเลขเครื่อง) เก็บใต้ source/ ห้าม commit; หน้าจอพิมพ์เฉพาะจำนวนแถว
- คอลัมน์: name, code, registered (yyyy-MM-dd), maker, department, range, resolution, limit, unit, calibrator, note
  (JIG: name = ชื่อ JIG; maker/range/resolution/unit ว่างตามรายงานเดิม · ภายนอก: department = แผนกที่ใช้งาน)
"""
import csv
import os
import sys
import time

import win32com.client

# ชื่อไฟล์ตรงกับ InstrumentKind ใน Lockte.Migration (CalibComparison.BaselineFile)
KINDS = [
    ("external", "ภายนอก", "ชื่อเครื่องมือวัด", "แผนกที่ใช้งาน", True),
    ("caliper", "CALIPER", "ชื่อเครื่องมือวัด", "แผนก", True),
    ("pressuregauge", "PRESSURE GAUGE", "ชื่อเครื่องมือวัด", "แผนก", True),
    ("scale", "เครื่องชั่ง", "ชื่อเครื่องมือวัด", "แผนก", True),
    ("tempcontrol", "ตัวควบคุมอุณหภูมิ", "ชื่อเครื่องมือวัด", "แผนก", True),
    ("jig", "jig", "ชื่อ JIG", "แผนก", False),
]


def text(value):
    return "" if value is None else str(value).strip()


def day(value):
    return "" if value is None else str(value)[:10]


def fetch(db, sql):
    rs = db.OpenRecordset(sql)
    rows = []
    if not rs.EOF:
        rs.MoveLast()
        rs.MoveFirst()
        rows = [list(r) for r in zip(*rs.GetRows(rs.RecordCount))]
    rs.Close()
    return rows


def main():
    args = sys.argv[1:]
    if len(args) < 2:
        print(__doc__)
        return 2
    path, out = os.path.abspath(args[0]), os.path.abspath(args[1])
    if "copy" not in os.path.basename(path).lower():
        print("ปฏิเสธ: ชื่อไฟล์ต้องมีคำว่า copy (ใช้กับสำเนาเท่านั้น)")
        return 2
    os.makedirs(out, exist_ok=True)
    db = win32com.client.Dispatch("DAO.DBEngine.120").OpenDatabase(path, False, True)
    start = time.time()
    try:
        for file_kind, table, name, department, full in KINDS:
            maker, rng, resolution, unit = ("[ยี่ห้อ/ผู้ผลิต]", "[พิกัดใช้งาน]", "[ค่าความละเอียด]", "[หน่วย]") if full else ("Null", "Null", "Null", "Null")
            rows = fetch(db, f"SELECT [{name}], [หมายเลขเครื่อง], [วันที่ขึ้นบัญชี], {maker}, [{department}], {rng}, {resolution}, [ขีดจำกัดที่ยอมรับได้], {unit}, [ผู้สอบเทียบ], [หมายเหตุ] "
                                 f"FROM [{table}] ORDER BY [หมายเลขเครื่อง]")
            with open(os.path.join(out, f"calib-{file_kind}.csv"), "w", encoding="utf-8-sig", newline="") as f:
                writer = csv.writer(f)
                writer.writerow(["name", "code", "registered", "maker", "department", "range", "resolution", "limit", "unit", "calibrator", "note"])
                writer.writerows([[text(r[0]), text(r[1]), day(r[2]), text(r[3]), text(r[4]), text(r[5]), text(r[6]), text(r[7]), text(r[8]), text(r[9]), text(r[10])] for r in rows])
            print(f"  calib-{file_kind}.csv: {len(rows)} แถว")
    finally:
        db.Close()
    print(f"เสร็จใน {time.time() - start:.0f} วินาที → {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

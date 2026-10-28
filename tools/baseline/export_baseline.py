"""ส่งออก baseline จากสำเนา "หลังปิดปี" ของผลผลิตและORDER.accdb เป็น CSV สำหรับเทียบกับระบบใหม่ (#140)

ใช้: python -I -X utf8 export_baseline.py <สำเนา.accdb> <โฟลเดอร์ผลลัพธ์> --cut YYYY-MM-DD [--from YYYY-MM-DD]

- เปิดสำเนาแบบอ่านอย่างเดียวผ่าน DAO (ไม่ต้องเปิดฟอร์ม/รายงานของ Access); ปฏิเสธถ้าชื่อไฟล์ไม่มีคำว่า copy
- SQL ตรงกับเงื่อนไขของรายงานเดิม (reports.md): RPT-02 ODD join P_NAME · RPT-06 ORDER ประวัติ · RPT-07 ODDส่งออก join P_NAME · RPT-13 ต้นงวดP_ST วันตัด · RPT-15 ตันงวดการใช้งานของพิมพ์0 วันตัด · RPT-16 P_REP 7 แผนก
- ผลลัพธ์ (CSV ข้อมูลจริง) เก็บใน source/ ห้าม commit; หน้าจอพิมพ์เฉพาะจำนวนแถว
"""
import csv
import os
import re
import sys
import time

import win32com.client

DEPARTMENT_TABLES = ["P_REPไดคาสท์", "P_REPตกแต่ง", "P_REPอัดผ้าเบรก", "P_REPฝนผ้าเบรก", "P_REPผ้าคลัทช์และดิสเบรค", "P_REPประกอบ", "P_REPบรรจุ"]


def literal(day):
    return f"#{int(day[5:7])}/{int(day[8:10])}/{day[:4]}#"


def fetch(db, sql):
    rs = db.OpenRecordset(sql)
    names = [f.Name for f in rs.Fields]
    rows = []
    if not rs.EOF:
        rs.MoveLast()
        rs.MoveFirst()
        rows = [list(r) for r in zip(*rs.GetRows(rs.RecordCount))]
    rs.Close()
    return names, rows


def text(value):
    return "" if value is None else str(value).strip()


def number(value):
    return "" if value is None else repr(float(value))


def day(value):
    return "" if value is None else str(value)[:10]


def write(out, name, header, rows):
    with open(os.path.join(out, name), "w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f)
        w.writerow(header)
        w.writerows(rows)
    print(f"  {name}: {len(rows)} แถว")


def main():
    args = sys.argv[1:]
    if len(args) < 2 or "--cut" not in args:
        print(__doc__)
        return 2
    path, out = os.path.abspath(args[0]), os.path.abspath(args[1])
    cut = args[args.index("--cut") + 1]
    first = args[args.index("--from") + 1] if "--from" in args else "2025-01-01"
    if "copy" not in os.path.basename(path).lower():
        print("ปฏิเสธ: ชื่อไฟล์ต้องมีคำว่า copy (ใช้กับสำเนาเท่านั้น)")
        return 2
    for value in (cut, first):
        if not re.fullmatch(r"\d{4}-\d{2}-\d{2}", value):
            print("วันที่ต้องเป็น YYYY-MM-DD")
            return 2
    os.makedirs(out, exist_ok=True)
    db = win32com.client.Dispatch("DAO.DBEngine.120").OpenDatabase(path, False, True)
    start = time.time()
    try:
        # RPT-02 / AC-19: ทุกบรรทัดใน ODD ที่รหัสสินค้ามีใน P_NAME (บรรทัดที่ไม่มีหายจากรายงานเดิมด้วย)
        _, rows = fetch(db, "SELECT o.REF_NO, o.SUF_NO, o.CUS_ID, o.PD_C, o.QTY FROM ODD AS o INNER JOIN P_NAME AS p ON o.PD_C = p.PD_C")
        write(out, "backlog.csv", ["REF_NO", "SUF_NO", "CUS_ID", "PD_C", "QTY"], [[text(r[0]), text(r[1]), text(r[2]), text(r[3]), number(r[4])] for r in rows])
        # RPT-06 / AC-17: ORDER (ประวัติ) ที่ CUS_ID และ CUS_N ไม่ว่าง
        _, rows = fetch(db, "SELECT REF_NO, SUF_NO, OD_DATE, CUS_ID, SALE_ID, PD_C, QTY, AMOUNT FROM [ORDER] WHERE CUS_ID IS NOT NULL AND CUS_N IS NOT NULL AND CUS_ID <> '' AND CUS_N <> ''")
        write(out, "receipts.csv", ["REF_NO", "SUF_NO", "OD_DATE", "CUS_ID", "SALE_ID", "PD_C", "QTY", "AMOUNT"],
              [[text(r[0]), text(r[1]), day(r[2]), text(r[3]), text(r[4]), text(r[5]), number(r[6]), number(r[7])] for r in rows])
        # RPT-07 / AC-16 / AC-23: ODDส่งออก join P_NAME
        _, rows = fetch(db, "SELECT d.INV_NO, d.INV_SUF, d.REF_NO, d.CUS_ID, d.PD_C, d.DL_DATE, d.DL_QTY, d.DL_NET, d.DISC_PS FROM [ODDส่งออก] AS d INNER JOIN P_NAME AS p ON d.PD_C = p.PD_C WHERE d.DL_DATE IS NOT NULL")
        write(out, "deliveries.csv", ["INV_NO", "INV_SUF", "REF_NO", "CUS_ID", "PD_C", "DL_DATE", "DL_QTY", "DL_NET", "DISC_PS"],
              [[text(r[0]), text(r[1]), text(r[2]), text(r[3]), text(r[4]), day(r[5]), number(r[6]), number(r[7]), number(r[8])] for r in rows])
        # RPT-13 / AC-11: ยอดยกมาวันตัด (QTY ≠ 0) พร้อมราคา FML
        _, rows = fetch(db, f"SELECT s.PD_C, s.QTY, p.FML FROM [ต้นงวดP_ST] AS s INNER JOIN P_NAME AS p ON s.PD_C = p.PD_C WHERE s.[DATE] = {literal(cut)} AND s.QTY <> 0")
        write(out, "stock-opening.csv", ["PD_C", "QTY", "FML"], [[text(r[0]), number(r[1]), number(r[2])] for r in rows])
        # AC-09: ยอดคงเหลือปัจจุบัน = Σ QTY ของ P_ST ต่อสินค้า
        _, rows = fetch(db, "SELECT PD_C, Sum(QTY) FROM P_ST WHERE PD_C IS NOT NULL GROUP BY PD_C")
        write(out, "stock-balance.csv", ["PD_C", "QTY"], [[text(r[0]), number(r[1])] for r in rows])
        # รายการที่ระบบเดิมทำเครื่องหมายยกเลิก (MARK de หรือ REF_NO DE) — เครื่องมือย้ายไม่ย้าย; แสดงยอดแยกให้เห็น
        _, rows = fetch(db, "SELECT PD_C, Sum(QTY) FROM P_ST WHERE PD_C IS NOT NULL AND (MARK = 'de' OR REF_NO = 'DE') GROUP BY PD_C")
        write(out, "stock-cancelled.csv", ["PD_C", "QTY"], [[text(r[0]), number(r[1])] for r in rows])
        # RPT-15 / AC-13: ยอดสะสมแม่พิมพ์ที่วันตัด
        _, rows = fetch(db, f"SELECT MOULD_ID, PD_QTY FROM [ตันงวดการใช้งานของพิมพ์0] WHERE [DAY] = {literal(cut)}")
        write(out, "mold-cumulative.csv", ["MOULD_ID", "PD_QTY"], [[text(r[0]), number(r[1])] for r in rows])
        # RPT-16 / AC-18: ผลผลิต 7 แผนก ตั้งแต่ปีที่กำหนด รวมต่อเดือน × สินค้า
        total = []
        for table in DEPARTMENT_TABLES:
            _, rows = fetch(db, f"SELECT REF_NO, SUF_NO, [DATE], PD_C, QTY, NG, LOT FROM [{table}] WHERE [DATE] >= {literal(first)} AND PD_C IS NOT NULL")
            total.extend([table, text(r[0]), text(r[1]), day(r[2]), text(r[3]), number(r[4]), number(r[5]), text(r[6])] for r in rows)
        write(out, "yield.csv", ["TABLE", "REF_NO", "SUF_NO", "DATE", "PD_C", "QTY", "NG", "LOT"], total)
        # RPT-16 อ่านสำเนาที่โอนเข้าสต็อกแล้ว (P_REP, BR-020): ใบที่ยังไม่โอนไม่อยู่ในรายงานผลผลิตของระบบเดิม
        # (เลขที่ใบรายงานของปีเก่าถูกใช้ซ้ำในปีใหม่ได้ จึงจับคู่ด้วยทั้งแถว: เลขที่ + ลำดับ + สินค้า + จำนวน + ของเสีย + Lot + วันที่ ตรงกับที่เครื่องมือย้ายใช้)
        _, rows = fetch(db, "SELECT REF_NO, SUF_NO, PD_C, QTY, NG, LOT, [DATE] FROM P_REP WHERE REF_NO IS NOT NULL")
        write(out, "yield-transferred.csv", ["REF_NO", "SUF_NO", "PD_C", "QTY", "NG", "LOT", "DATE"],
              [[text(r[0]), text(r[1]), text(r[2]), number(r[3]), number(r[4]), text(r[5]), day(r[6])] for r in rows])
    finally:
        db.Close()
    print(f"เสร็จใน {time.time() - start:.0f} วินาที → {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

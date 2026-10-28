"""ซ้อมชุดปิดปีบน "สำเนา" ของผลผลิตและORDER.accdb ผ่าน DAO (ไม่ต้องเปิดฟอร์ม/มาโครของ Access)

ใช้: python -I -X utf8 rehearse.py <สำเนา.accdb> <โฟลเดอร์ผลลัพธ์> [--stock-cut YYYY-MM-DD] [--mold-cut ตั้งแต่ ถึง]

- ปฏิเสธถ้าชื่อไฟล์ไม่มีคำว่า rehearsal (กันแก้ไฟล์ใช้งานจริง); แก้เฉพาะไฟล์ที่ส่งให้ ไม่แตะที่อื่น
- ขั้น A: รัน sql/00–08 (ลบ pd ซ้ำเป๊ะ + แถวขอบเขตไม่ชัด + ตารางสำรอง + รายงานก่อน/หลัง) พร้อมเวลา; หยุดถ้าแถวขอบเขตไม่ชัดเกิน 10
- ขั้น B (ถ้าระบุ --stock-cut): รัน query เดิมของมาโคร `ตัดSTOCK` (ตัดSTOCK1..10) ครั้งเดียวที่วันตัดที่กำหนด แล้วตรวจว่ายอดต่อสินค้าไม่เปลี่ยน
- ขั้น C (ถ้าระบุ --mold-cut): รัน query เดิมของมาโคร `ตัดพิมพ์` ครั้งเดียว (ตั้งแต่วันแรกหลังตัดล่าสุดถึงวันตัด)
- ผลลัพธ์ (CSV มีรหัสจริง) เขียนในโฟลเดอร์ผลลัพธ์เท่านั้น — เก็บนอก git; หน้าจอพิมพ์เฉพาะตัวเลขรวม
"""
import csv
import os
import re
import sys
import time

import win32com.client

HERE = os.path.dirname(os.path.abspath(__file__))
SQL_DIR = os.path.join(HERE, "sql")
FAIL_ON_ERROR = 128


def read_statements(name):
    """คำสั่ง SQL ในไฟล์ (ตัดบรรทัดหมายเหตุ -- ออก); ไฟล์ 00 มีหลายคำสั่ง บรรทัดละคำสั่ง"""
    text = open(os.path.join(SQL_DIR, name), encoding="utf-8").read()
    lines = [line for line in text.splitlines() if line.strip() and not line.lstrip().startswith("--")]
    if name.startswith("00"):
        return [line.strip().rstrip(";") for line in lines]
    return [" ".join(line.strip() for line in lines)]


def fetch(db, sql):
    rs = db.OpenRecordset(sql)
    rows = []
    if not rs.EOF:
        rs.MoveLast()
        rs.MoveFirst()
        rows = [list(r) for r in zip(*rs.GetRows(rs.RecordCount))]
    names = [f.Name for f in rs.Fields]
    rs.Close()
    return names, rows


def scalar_row(db, sql):
    return fetch(db, sql)[1][0]


def write_csv(path, names, rows):
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f)
        w.writerow(names)
        w.writerows(rows)


def timed(label, action):
    start = time.time()
    result = action()
    print(f"  {label}: {time.time() - start:.1f} วินาที")
    return result


def run_year_close(db, out):
    print("A. ลบ pd ซ้ำ (Q-47)")
    for line in read_statements("00-drop-working-tables.sql"):
        try:
            db.Execute(line, FAIL_ON_ERROR)
        except Exception:  # ตารางไม่มี = ปกติ
            pass
    total_before = scalar_row(db, "SELECT Count(*), Sum(QTY) FROM P_ST")
    timed("01 ยอดก่อน", lambda: db.Execute(read_statements("01-balance-before.sql")[0], FAIL_ON_ERROR))
    timed("02 หาแถวซ้ำเป๊ะ", lambda: db.Execute(read_statements("02-find-duplicates.sql")[0], FAIL_ON_ERROR))
    timed("03 หาแถวขอบเขตไม่ชัด", lambda: db.Execute(read_statements("03-find-ambiguous.sql")[0], FAIL_ON_ERROR))
    rows_to_delete, units_to_delete = scalar_row(db, read_statements("04-check-before-delete.sql")[0])
    ambiguous = scalar_row(db, read_statements("04b-check-ambiguous.sql")[0])
    ambiguous = (ambiguous[0], ambiguous[1] or 0)
    names, detail = fetch(db, read_statements("04c-ambiguous-detail.sql")[0])
    write_csv(os.path.join(out, "ambiguous-detail.csv"), names, detail)
    print(f"  ซ้ำเป๊ะ {rows_to_delete} แถว Σ {units_to_delete} หน่วย; ขอบเขตไม่ชัด (จำนวนต่าง) {ambiguous[0]} แถว Σ {ambiguous[1]} หน่วย (รายละเอียดใน ambiguous-detail.csv)")
    if ambiguous[0] > 10:
        print("  หยุด: แถวขอบเขตไม่ชัดมากกว่า 10 แถว — ถามทีมพัฒนาก่อน")
        return {"stopped": True}
    timed("05 ลบซ้ำเป๊ะ", lambda: db.Execute(read_statements("05-delete-duplicates.sql")[0], FAIL_ON_ERROR))
    timed("05b สำรองแถวขอบเขตไม่ชัด", lambda: db.Execute(read_statements("05b-backup-ambiguous.sql")[0], FAIL_ON_ERROR))
    timed("05c ลบแถวขอบเขตไม่ชัด", lambda: db.Execute(read_statements("05c-delete-ambiguous.sql")[0], FAIL_ON_ERROR))
    timed("06 ยอดหลัง", lambda: db.Execute(read_statements("06-balance-after.sql")[0], FAIL_ON_ERROR))
    names, changed = fetch(db, read_statements("07-changed-products.sql")[0])
    write_csv(os.path.join(out, "changed-products.csv"), names, changed)
    remaining = scalar_row(db, read_statements("08-verify-after.sql")[0])[0]
    total_after = scalar_row(db, "SELECT Count(*), Sum(QTY) FROM P_ST")
    print(f"  P_ST ก่อน {total_before[0]} แถว Σ {total_before[1]} → หลัง {total_after[0]} แถว Σ {total_after[1]}; สินค้าที่ยอดเปลี่ยน {len(changed)}; ซ้ำที่เหลือ {remaining}")
    expected = abs((total_before[1] or 0) - (total_after[1] or 0) - (units_to_delete or 0) - ambiguous[1]) < 0.001
    print("  ผลต่างยอดรวม = Σ ที่ลบ (ซ้ำเป๊ะ + ขอบเขตไม่ชัด):", "ตรง" if expected else "ไม่ตรง")
    return {"deleted": rows_to_delete, "units": units_to_delete, "ambiguous_deleted": ambiguous[0], "ambiguous_units": ambiguous[1], "remaining": remaining, "balanced": expected}


def run_stock_cut(db, cut_date, out):
    """ใช้ query เดิมที่บันทึกในไฟล์ (มาโคร ตัดSTOCK → ตัดSTOCK1..10) ไม่คัดลอก SQL มาเขียนใหม่"""
    print(f"B. ตัดสต็อกครั้งเดียว ณ {cut_date} (query เดิม ตัดSTOCK1..10)")
    month, day, year = cut_date[5:7].lstrip("0"), cut_date[8:10].lstrip("0"), cut_date[:4]
    db.Execute(f"UPDATE [วันที่ตัดSTOCK] SET FROM_DATE = #{month}/{day}/{year}#", FAIL_ON_ERROR)  # ตารางหนึ่งแถว (FROM_DATE, TO_DATE) ที่ฟอร์มวันที่ตัดSTOCK แก้
    before_p_st = scalar_row(db, "SELECT Count(*) FROM P_ST")[0]
    before_added = scalar_row(db, "SELECT Count(*) FROM P_ADDST")[0]
    before_start = scalar_row(db, "SELECT Count(*) FROM [ต้นงวดP_ST]")[0]
    names, before = fetch(db, "SELECT PD_C, Sum(QTY) FROM P_ST GROUP BY PD_C")
    before_map = {r[0]: r[1] for r in before}
    start = time.time()
    for n in (1, 2, 3, 4, 5, 7, 8, 9, 10):
        timed(f"ตัดSTOCK{n}", lambda n=n: db.QueryDefs(f"ตัดSTOCK{n}").Execute(FAIL_ON_ERROR))
    print(f"  รวม {time.time() - start:.1f} วินาที")
    names, after = fetch(db, "SELECT PD_C, Sum(QTY) FROM P_ST GROUP BY PD_C")
    after_map = {r[0]: r[1] for r in after}
    different = [(code, before_map.get(code), after_map.get(code)) for code in set(before_map) | set(after_map)
                 if abs((before_map.get(code) or 0) - (after_map.get(code) or 0)) > 0.0001]
    write_csv(os.path.join(out, "stock-cut-differences.csv"), ["PD_C", "BEFORE", "AFTER"], different)
    print(f"  P_ST {before_p_st} → {scalar_row(db, 'SELECT Count(*) FROM P_ST')[0]} แถว; P_ADDST {before_added} → {scalar_row(db, 'SELECT Count(*) FROM P_ADDST')[0]}; "
          f"ต้นงวดP_ST {before_start} → {scalar_row(db, 'SELECT Count(*) FROM [ต้นงวดP_ST]')[0]}; สินค้า {len(before_map)} → {len(after_map)}; ยอดต่อสินค้าเปลี่ยน {len(different)} ราย")
    return {"different": len(different)}


MOLD_QUERIES = ["บันทึกผลผลิต4", "บันทึกผลผลิต41", "บันทึกผลผลิต210", "บันทึกผลผลิต210A", "บันทึกผลผลิต2100", "บันทึกผลผลิต2100A", "บันทึกผลผลิต21000", "บันทึกผลผลิต21000A",
                "บันทึกผลผลิต210000", "บันทึกผลผลิต210000A", "ตัดพิมพ์0", "ตัดพิมพ์1", "ตัดพิมพ์2", "ตัดพิมพ์21", "ตัดพิมพ์3", "ตัดพิมพ์4", "ตัดพิมพ์7", "ตัดพิมพ์8", "ตัดพิมพ์9"]


def run_mold_cut(db, from_date, cut_date, out):
    """ใช้ query เดิมของมาโคร `ตัดพิมพ์` (เฉพาะ query ที่แก้ข้อมูล; query SELECT ถูกเรียกผ่าน query ต่อท้ายอยู่แล้ว)"""
    print(f"C. ตัดยอดแม่พิมพ์ครั้งเดียว {from_date} → {cut_date} (query เดิมของมาโคร ตัดพิมพ์)")

    def literal(d):
        return f"#{d[5:7].lstrip('0')}/{d[8:10].lstrip('0')}/{d[:4]}#"
    db.Execute(f"UPDATE [วันที่ตัดพิมพ์] SET FROM_DATE = {literal(from_date)}, CUT_DATE = {literal(cut_date)}", FAIL_ON_ERROR)
    count = lambda t: scalar_row(db, f"SELECT Count(*) FROM [{t}]")[0]
    before = (count("การใช้งานของพิมพ์0"), count("ตันงวดการใช้งานของพิมพ์0"))
    start = time.time()
    skipped = []
    for name in MOLD_QUERIES:
        qd = db.QueryDefs(name)
        if qd.Type == 0:  # SELECT: ไม่รันเดี่ยว
            continue
        try:
            timed(name, lambda qd=qd: qd.Execute(FAIL_ON_ERROR))
        except Exception as error:  # Q-34: query ผ้าคลัทช์/ดิสเบรกอ้างช่อง QTY ที่ต้นทางไม่มี Access จะถามค่าตอนรัน — ซ้อมแบบไม่มีคนตอบ ข้าม
            if "Too few parameters" not in str(error):
                raise
            skipped.append(name)
            print(f"  {name}: ขอค่าพารามิเตอร์ (Q-34) — ข้ามในการซ้อม")
    print(f"  รวม {time.time() - start:.1f} วินาที")
    names, rows = fetch(db, "SELECT MOULD_ID, Sum(PD_QTY) FROM [การใช้งานของพิมพ์0] GROUP BY MOULD_ID")
    write_csv(os.path.join(out, "mold-cumulative-after-cut.csv"), ["MOULD_ID", "CUMULATIVE"], rows)
    print(f"  การใช้งานของพิมพ์0 {before[0]} → {count('การใช้งานของพิมพ์0')} แถว; ตันงวดการใช้งานของพิมพ์0 {before[1]} → {count('ตันงวดการใช้งานของพิมพ์0')}; แม่พิมพ์ที่มียอดสะสม {len(rows)}; query ที่ข้าม {len(skipped)}")


def main():
    args = sys.argv[1:]
    if len(args) < 2:
        print(__doc__)
        return 2
    path, out = os.path.abspath(args[0]), os.path.abspath(args[1])
    cut = args[args.index("--stock-cut") + 1] if "--stock-cut" in args else None
    mold = (args[args.index("--mold-cut") + 1], args[args.index("--mold-cut") + 2]) if "--mold-cut" in args else None
    if "rehearsal" not in os.path.basename(path).lower():
        print("ปฏิเสธ: ชื่อไฟล์ต้องมีคำว่า rehearsal (ใช้กับ 'สำเนา' ที่ทำไว้ซ้อมเท่านั้น)")
        return 2
    if cut and not re.fullmatch(r"\d{4}-\d{2}-\d{2}", cut):
        print("วันตัดต้องเป็น YYYY-MM-DD")
        return 2
    os.makedirs(out, exist_ok=True)
    size_before = os.path.getsize(path)
    engine = win32com.client.Dispatch("DAO.DBEngine.120")
    db = engine.OpenDatabase(path, False, False)
    try:
        result = run_year_close(db, out)
        if cut:
            run_stock_cut(db, cut, out)
        if mold:
            run_mold_cut(db, mold[0], mold[1], out)
    finally:
        db.Close()
    print(f"ขนาดไฟล์ {size_before / 1048576:.0f} MB → {os.path.getsize(path) / 1048576:.0f} MB (ก่อนบีบอัด)")
    print("เสร็จ:", result)
    return 0


if __name__ == "__main__":
    sys.exit(main())

"""ส่งออก baseline ของรายงานเงินสดย่อยจากสำเนา เงินสดย่อย.accdb เป็น CSV สำหรับเทียบกับระบบใหม่ (เงินสดย่อย #248)

ใช้: python -I -X utf8 export_cash_baseline.py <เงินสดย่อย.accdb> <โฟลเดอร์ผลลัพธ์> [--range รหัส,YYYY-MM-DD,YYYY-MM-DD ...]

- ทำสำเนาไฟล์ไปไว้ในโฟลเดอร์ชั่วคราวก่อน แล้วเปิดสำเนาแบบอ่านอย่างเดียวผ่าน ODBC (ACE) — ไม่เปิดฟอร์ม/รายงานของ Access (เปิดบน 64-bit ไม่ได้) ไม่แก้ต้นฉบับ
- SQL ตรงกับเงื่อนไขของ query รายงานเดิม (reports.md): RPT-02 แบบสอบถาม1/2 (รวม AMOUNT ตาม TYPE ในช่วงวันที่ รวมวันปลาย), RPT-04 รายการซื้อของ (เรียง DATE, SUF),
  RPT-08 ขายของเก่าคงค้าง (รวมตาม NO ของขายของเก่า1), RPT-09 พิมพ์ยอดขายของเก่า4 (เรียง DATE, NO, SUF), ยอดรวมใบเสร็จ พิมพ์ใบเสร็จ4 (CutDecimal = ปัด 2 ตำแหน่งแบบ banker's ของผลรวม)
- ช่วงวันที่: ทุกปีที่มีข้อมูล, ทุกเดือนของ 24 เดือนล่าสุด, และตัวอย่างของ acceptance.md (AC07 AC15 AC20 BR018-2) + --range ที่ระบุเพิ่ม
- ผลลัพธ์ (ข้อมูลจริง เก็บใต้ source/ ห้าม commit; หน้าจอพิมพ์เฉพาะจำนวนแถว):
  cash-ranges.csv (id,from,to) · cash-summary.csv (metric,value) · cash-rpt02.csv (range,type,total) · cash-rpt04.csv (range,date,suf,name,qty,unit,amount,type)
  cash-rpt08.csv (no,total,lines) · cash-rpt09.csv (range,date,no,suf,names,qty,unit,price,amount,named) · cash-receipts.csv (no,lines,sum,access_total)
"""
import csv
import datetime
import os
import re
import shutil
import struct
import sys
import tempfile

import pyodbc

ACCEPTANCE = [
    ("AC07", "2026-09-01", "2026-09-03"),
    ("AC15", "2026-09-01", "2026-09-30"),
    ("AC20", "2026-09-11", "2026-09-11"),
    ("BR018-2", "2026-08-05", "2026-08-05"),
]


def text(value):
    """Trimmed text with line breaks made blanks (62 legacy TYPE values run over two lines): the comparison cleans names the same way, and a CSV cell stays on one line."""
    return "" if value is None else re.sub(r"[\r\n]+", " ", str(value)).strip()


def day(value):
    return "" if value is None else str(value)[:10]


def single(value):
    """A Single column read as the number Access shows (0.33000001 → 0.33): the float32 to 7 significant digits."""
    if value is None:
        return ""
    packed = struct.unpack("f", struct.pack("f", float(value)))[0]
    return "%.7g" % packed


def double(value):
    return "" if value is None else repr(float(value))


def float32_as_double(value):
    return struct.unpack("f", struct.pack("f", float(value)))[0]


def cut_decimal(total):
    """The legacy CutDecimal: ((x * 100) \\ 1) / 100 — VBA rounds the operand to a whole number half to even first."""
    return "%.2f" % (round(total * 100) / 100)


def write(folder, name, header, rows):
    with open(os.path.join(folder, name), "w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f)
        w.writerow(header)
        w.writerows(rows)
    print(f"{name}: {len(rows)} แถว")


def date_literal(iso):
    y, m, d = iso.split("-")
    return f"#{m}/{d}/{y}#"


def months_back(last, count):
    result = []
    y, m = last.year, last.month
    for _ in range(count):
        first = datetime.date(y, m, 1)
        nxt = datetime.date(y + (m == 12), 1 if m == 12 else m + 1, 1)
        result.append((f"M{y:04d}-{m:02d}", first.isoformat(), (nxt - datetime.timedelta(days=1)).isoformat()))
        m -= 1
        if m == 0:
            y, m = y - 1, 12
    return result


def main():
    args = sys.argv[1:]
    if len(args) < 2:
        print(__doc__)
        return 2
    source, out = os.path.abspath(args[0]), os.path.abspath(args[1])
    extra = []
    i = 2
    while i < len(args):
        if args[i] == "--range" and i + 1 < len(args):
            rid, a, b = args[i + 1].split(",")
            extra.append((rid, a, b))
            i += 2
        else:
            print(f"ไม่เข้าใจ {args[i]}")
            return 2
    if not os.path.exists(source):
        print(f"ไม่พบไฟล์ {source}")
        return 2
    os.makedirs(out, exist_ok=True)
    work = tempfile.mkdtemp(prefix="lockte-cash-baseline-")
    try:
        copy = os.path.join(work, "copy.accdb")
        shutil.copy(source, copy)
        connection = pyodbc.connect(f"Driver={{Microsoft Access Driver (*.mdb, *.accdb)}};Dbq={copy};ReadOnly=1;")
        cur = connection.cursor()

        def fetch(sql):
            cur.execute(sql)
            return cur.fetchall()

        bounds = fetch("SELECT MIN([DATE]), MAX([DATE]) FROM [ซื้อของเงินสด] WHERE [DATE] IS NOT NULL")[0]
        sale_bounds = fetch("SELECT MIN([DATE]), MAX([DATE]) FROM [ขายของเก่า] WHERE [DATE] IS NOT NULL")[0]
        first = min(d for d in (bounds[0], sale_bounds[0]) if d)
        last = max(d for d in (bounds[1], sale_bounds[1]) if d)
        ranges = [(f"Y{y}", f"{y}-01-01", f"{y}-12-31") for y in range(first.year, last.year + 1)]
        ranges += months_back(last.date() if hasattr(last, "date") else last, 24)
        ranges += ACCEPTANCE + extra
        write(out, "cash-ranges.csv", ["id", "from", "to"], [list(r) for r in ranges])

        # AC-01: the sizes — every row, and the rows the migration can load (the legacy deleted the others when the screen was left, BR-003/BR-014)
        lines = fetch("SELECT COUNT(*), SUM(AMOUNT) FROM [ซื้อของเงินสด]")[0]
        loadable = fetch("SELECT COUNT(*), SUM(AMOUNT) FROM [ซื้อของเงินสด] WHERE [DATE] IS NOT NULL AND SUF IS NOT NULL AND NAME IS NOT NULL AND [TYPE] IS NOT NULL")[0]
        sales = fetch("SELECT COUNT(*), SUM(AMOUNT) FROM [ขายของเก่า]")[0]
        sales_loadable = fetch("SELECT COUNT(*), SUM(AMOUNT) FROM [ขายของเก่า] WHERE [NO] IS NOT NULL AND NAMES IS NOT NULL AND QTY IS NOT NULL")[0]
        receipts = fetch("SELECT COUNT(*) FROM (SELECT DISTINCT [NO] FROM [ขายของเก่า] WHERE [NO] IS NOT NULL)")[0]
        waiting = fetch("SELECT COUNT(*) FROM [ขายของเก่า1]")[0]
        waiting_receipts = fetch("SELECT COUNT(*) FROM (SELECT DISTINCT [NO] FROM [ขายของเก่า1] WHERE [NO] IS NOT NULL)")[0]
        write(out, "cash-summary.csv", ["metric", "value"], [
            ["expense_lines_all", lines[0]], ["expense_amount_all", "%.2f" % (lines[1] or 0)],
            ["expense_lines_loadable", loadable[0]], ["expense_amount_loadable", "%.2f" % (loadable[1] or 0)],
            ["receipt_numbers", receipts[0]], ["receipt_lines_all", sales[0]], ["receipt_lines_loadable", sales_loadable[0]],
            ["waiting_lines", waiting[0]], ["waiting_receipts", waiting_receipts[0]],
        ])

        rpt02, rpt04, rpt09 = [], [], []
        for rid, a, b in ranges:
            lo, hi = date_literal(a), date_literal(b)
            for r in fetch(f"SELECT [TYPE], SUM(AMOUNT) FROM [ซื้อของเงินสด] WHERE [DATE] >= {lo} AND [DATE] <= {hi} GROUP BY [TYPE]"):
                rpt02.append([rid, text(r[0]) if r[0] is not None else "", "%.4f" % (r[1] or 0)])
            for r in fetch(f"SELECT [DATE], SUF, NAME, QTY, UNIT, AMOUNT, [TYPE] FROM [ซื้อของเงินสด] WHERE [DATE] >= {lo} AND [DATE] <= {hi} ORDER BY [DATE], SUF"):
                rpt04.append([rid, day(r[0]), text(r[1]), text(r[2]), double(r[3]), text(r[4]), double(r[5]), text(r[6])])
            for r in fetch(f"SELECT [DATE], [NO], SUF, NAMES, QTY, UNIT, U_PRICE, AMOUNT, NAMED FROM [ขายของเก่า] WHERE [DATE] >= {lo} AND [DATE] <= {hi} ORDER BY [DATE], [NO], SUF"):
                rpt09.append([rid, day(r[0]), text(r[1]), single(r[2]), text(r[3]), single(r[4]), text(r[5]), single(r[6]), single(r[7]), text(r[8])])
        write(out, "cash-rpt02.csv", ["range", "type", "total"], rpt02)
        write(out, "cash-rpt04.csv", ["range", "date", "suf", "name", "qty", "unit", "amount", "type"], rpt04)
        write(out, "cash-rpt09.csv", ["range", "date", "no", "suf", "names", "qty", "unit", "price", "amount", "named"], rpt09)

        # RPT-08: the receipts waiting, with the sum Access gives each (HAVING > 0 is applied by the comparison, which also wants the numbers whose sum is 0 for AC-23)
        rpt08 = []
        for no, count in fetch("SELECT [NO], COUNT(*) FROM [ขายของเก่า1] WHERE [NO] IS NOT NULL GROUP BY [NO]"):
            amounts = [float32_as_double(r[0]) for r in fetch(f"SELECT AMOUNT FROM [ขายของเก่า1] WHERE [NO] = '{text(no)}'") if r[0] is not None]
            rpt08.append([text(no), cut_decimal(sum(amounts)), count])
        write(out, "cash-rpt08.csv", ["no", "total", "lines"], rpt08)

        # the total of every receipt as Access prints it (พิมพ์ใบเสร็จ4: the sum of the Single amounts as a Double, then CutDecimal); `sum` is the sum as it is, to tell a sum beyond 2 decimals
        by_no = {}
        for r in fetch("SELECT [NO], AMOUNT FROM [ขายของเก่า] WHERE [NO] IS NOT NULL"):
            by_no.setdefault(text(r[0]), []).append(0.0 if r[1] is None else float32_as_double(r[1]))
        write(out, "cash-receipts.csv", ["no", "lines", "sum", "access_total"], [[no, len(v), repr(sum(v)), cut_decimal(sum(v))] for no, v in sorted(by_no.items())])
        connection.close()
    finally:
        shutil.rmtree(work, ignore_errors=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())

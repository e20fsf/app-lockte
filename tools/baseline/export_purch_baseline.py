"""ส่งออก baseline ของรายงานวัตถุดิบจากสำเนา วัตถุดิบ.accdb เป็น CSV สำหรับเทียบกับระบบใหม่ (วัตถุดิบ #271)

ใช้: python -I -X utf8 export_purch_baseline.py <วัตถุดิบ.accdb> <โฟลเดอร์ผลลัพธ์> [--range รหัส,YYYY-MM-DD,YYYY-MM-DD ...] [--cut YYYY-MM-DD ...]

- ทำสำเนาไฟล์ไปไว้ในโฟลเดอร์ชั่วคราวก่อน แล้วเปิดสำเนาแบบอ่านอย่างเดียวผ่าน ODBC (ACE) — ไม่เปิดฟอร์ม/รายงานของ Access ไม่แก้ต้นฉบับ
- SQL ตรงกับเงื่อนไขของ query รายงานเดิม (reports.md, business-rules.md):
  RPT-04 ยังไม่ได้รับวัตถุดิบ (PO_RECEI4 ที่ RE_DATE ว่าง เรียง PO_NO, PO_SUF; แถวที่กรอกวันที่รับค้างไว้ส่งออกด้วย listed = 0) · RPT-20 ยอดรวมการรับของ (PO_RECEI5 ในช่วง PASS <> "ไม่ผ่าน" หรือว่าง รวมตาม MTR_NAME + MTR_UNIT,
  จำนวน PR_NO / PO_NO ไม่ซ้ำ) · RPT-22 รายงานการรับของ1 (รวม NET_AMT ตาม EMP_ID) · RPT-11 ใบสั่งซื้อต่ำกว่า5000บาท1/2 (PO_DATE ในช่วง, PUR_ID ว่าง, TOTAL <= 5000 จากทั้งสองตาราง; คอลัมน์ literal = 1 ถ้า query เดิมตามตัวอักษรแสดงแถวนี้ ·
  PUR_ID ของประวัติรับเป็นข้อความว่าง ไม่ใช่ Null จึงไม่แสดงเลย) ·
  RPT-26 ตัดSTOCK11 (ต้นงวดP_ST วันที่ = วันตัด, QTY <> 0, INNER JOIN MTR_NAME) · AC-07 ทุกแถวของใบสั่งซื้อปีล่าสุดในทั้งสองตาราง (ราคา จำนวน NET_AMT TAX TOTAL VAT) ·
  AC-18 Σ RE_QTY ของ Stock ต่อรหัส · AC-24 T-ประเมินผู้ขาย ของไตรมาสที่ระบุ และผลนับของ บันทึกINCOMING (LOT ไม่ว่าง JOIN SP ด้วยชื่อ) บนข้อมูลปัจจุบัน
- ช่วงวันที่: ทุกปีตั้งแต่วันที่สั่งซื้อแรก, ทุกเดือนของ 24 เดือนล่าสุด, ตัวอย่างของ acceptance.md (AC25 AC26 AC28) + --range ที่ระบุเพิ่ม
- วันตัดสต็อก: ทุกวันตัดตั้งแต่ 2025-01-01 (ที่การย้ายข้อมูลโหลด) + --cut
- ผลลัพธ์ (ข้อมูลจริง เก็บใต้ source/ ห้าม commit; หน้าจอพิมพ์เฉพาะจำนวนแถว):
  purch-ranges.csv (id,from,to) · purch-summary.csv (metric,value) · purch-rpt04.csv · purch-rpt20.csv · purch-rpt20-heads.csv · purch-rpt22.csv · purch-rpt11.csv ·
  purch-rpt26.csv · purch-cut-balances.csv · purch-stock-backdated.csv · purch-orders.csv · purch-stock.csv · purch-eval.csv · purch-eval-now.csv
"""
import csv
import datetime
import os
import shutil
import struct
import sys
import tempfile

import pyodbc

ACCEPTANCE = [
    ("AC25-26", "2026-08-01", "2026-08-31"),
    ("AC28", "2026-09-01", "2026-09-30"),
]
EVALUATION = ("2026-04-01", "2026-06-30")  # AC-24 ไตรมาส 2/2026
CUT_FROM = "2025-01-01"  # LegacyDatabase.PurchCutFrom: the cuts the migration loads


def text(value):
    return "" if value is None else str(value).replace("\r", " ").replace("\n", " ").strip()


def day(value):
    return "" if value is None else str(value)[:10]


def single(value):
    """A Single column read as the number Access shows (0.33000001 → 0.33): the float32 to 7 significant digits."""
    if value is None:
        return ""
    return "%.7g" % struct.unpack("f", struct.pack("f", float(value)))[0]


def double(value):
    return "" if value is None else repr(float(value))


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
    extra, cuts = [], []
    i = 2
    while i < len(args):
        if args[i] == "--range" and i + 1 < len(args):
            rid, a, b = args[i + 1].split(",")
            extra.append((rid, a, b))
            i += 2
        elif args[i] == "--cut" and i + 1 < len(args):
            cuts.append(args[i + 1])
            i += 2
        else:
            print(f"ไม่เข้าใจ {args[i]}")
            return 2
    if not os.path.exists(source):
        print(f"ไม่พบไฟล์ {source}")
        return 2
    os.makedirs(out, exist_ok=True)
    work = tempfile.mkdtemp(prefix="lockte-purch-baseline-")
    try:
        copy = os.path.join(work, "copy.accdb")
        shutil.copy(source, copy)
        connection = pyodbc.connect(f"Driver={{Microsoft Access Driver (*.mdb, *.accdb)}};Dbq={copy};ReadOnly=1;")
        cur = connection.cursor()

        def fetch(sql):
            cur.execute(sql)
            return cur.fetchall()

        last = fetch("SELECT MAX(RE_DATE) FROM PO_RECEI5 WHERE RE_DATE IS NOT NULL")[0][0]
        first = min(fetch(f"SELECT MIN(PO_DATE) FROM {t} WHERE PO_DATE IS NOT NULL")[0][0] for t in ("PO_RECEI4", "PO_RECEI5"))  # the orders reach back further than the deliveries kept
        ranges = [(f"Y{y}", f"{y}-01-01", f"{y}-12-31") for y in range(first.year, last.year + 1)]
        ranges += months_back(last.date() if hasattr(last, "date") else last, 24)
        ranges += ACCEPTANCE + extra
        write(out, "purch-ranges.csv", ["id", "from", "to"], [list(r) for r in ranges])

        # the sizes (AC-30) — for the gate's table of the migration
        def count(sql):
            return fetch(sql)[0][0]
        write(out, "purch-summary.csv", ["metric", "value"], [
            ["materials", count("SELECT COUNT(*) FROM MTR_NAME")],
            ["suppliers", count("SELECT COUNT(*) FROM SP")],
            ["supplier_codes", count("SELECT COUNT(*) FROM (SELECT DISTINCT UCASE(CODE) FROM SP WHERE CODE IS NOT NULL)")],
            ["waiting_rows", count("SELECT COUNT(*) FROM PO_RECEI4")],
            ["waiting_open", count("SELECT COUNT(*) FROM PO_RECEI4 WHERE RE_DATE IS NULL")],
            ["history_rows", count("SELECT COUNT(*) FROM PO_RECEI5")],
            ["history_loadable", count("SELECT COUNT(*) FROM PO_RECEI5 WHERE RE_DATE IS NOT NULL AND MTR_NAME IS NOT NULL")],
            ["evaluations", count("SELECT COUNT(*) FROM [ประเมินผู้ขาย]")],
            ["evaluations_all_suppliers", count("SELECT COUNT(*) FROM [ประเมินผู้ขาย] WHERE SUP = '*'")],
            ["stock_rows", count("SELECT COUNT(*) FROM Stock")],
        ])

        # RPT-04: the lines still waited for (RE_DATE ว่าง, as the report) — and the rows with a วันที่รับ typed in but never confirmed (BR-030), which the report leaves out (listed = 0)
        write(out, "purch-rpt04.csv", ["po_date", "po_no", "po_suf", "name", "unit", "qty", "deli_d", "sup_name", "sup_tel", "listed"],
              [[day(r[0]), text(r[1]), single(r[2]), text(r[3]), text(r[4]), single(r[5]), day(r[6]), text(r[7]), text(r[8]), 1 if r[9] is None else 0]
               for r in fetch("SELECT PO_DATE, PO_NO, PO_SUF, MTR_NAME, MTR_UNIT, PO_QTY, DELI_D, SUP_NAME, SUP_TEL, RE_DATE FROM PO_RECEI4 ORDER BY PO_NO, PO_SUF")])

        passed = "(PASS <> 'ไม่ผ่าน' OR PASS IS NULL)"
        rpt20, heads, rpt22, rpt11 = [], [], [], []
        for rid, a, b in ranges:
            lo, hi = date_literal(a), date_literal(b)
            where = f"RE_DATE >= {lo} AND RE_DATE <= {hi} AND {passed}"
            for r in fetch(f"SELECT MTR_NAME, MTR_UNIT, SUM(RE_QTY), SUM(NET_AMT) FROM PO_RECEI5 WHERE {where} GROUP BY MTR_NAME, MTR_UNIT"):
                rpt20.append([rid, text(r[0]), text(r[1]), double(r[2]), double(r[3])])
            requests = count(f"SELECT COUNT(*) FROM (SELECT PR_NO FROM PO_RECEI5 WHERE {where} GROUP BY PR_NO)")
            orders = count(f"SELECT COUNT(*) FROM (SELECT PO_NO FROM PO_RECEI5 WHERE {where} GROUP BY PO_NO)")
            lines = count(f"SELECT COUNT(*) FROM PO_RECEI5 WHERE {where}")
            heads.append([rid, lines, requests, orders])
            for r in fetch(f"SELECT EMP_ID, SUM(NET_AMT) FROM PO_RECEI5 WHERE {where} GROUP BY EMP_ID"):
                rpt22.append([rid, text(r[0]), double(r[1])])
            # PUR_ID of the delivered rows is a zero-length string, not Null: the query as written lists none of them (literal = 1); the rows it means to list (BR-063) are those too (literal = 0)
            for table in ("PO_RECEI4", "PO_RECEI5"):
                leftover = "IIF(RE_DATE IS NULL OR MTR_NAME IS NULL, 1, 0)" if table == "PO_RECEI5" else "0"  # a leftover row of the history is not moved (Q-12)
                for r in fetch(f"SELECT PO_DATE, PO_NO, PO_SUF, MTR_NAME, PO_QTY, MTR_UNIT, TAX, SUP_NAME, TOTAL, IIF(PUR_ID IS NULL, 1, 0), {leftover} FROM {table} "
                               f"WHERE PO_DATE >= {lo} AND PO_DATE <= {hi} AND (PUR_ID IS NULL OR PUR_ID = '') AND TOTAL <= 5000"):
                    rpt11.append([rid, table, day(r[0]), text(r[1]), single(r[2]), text(r[3]), single(r[4]), text(r[5]), single(r[6]), text(r[7]), single(r[8]), r[9], r[10]])
        write(out, "purch-rpt20.csv", ["range", "name", "unit", "qty", "amount"], rpt20)
        write(out, "purch-rpt20-heads.csv", ["range", "lines", "requests", "orders"], heads)
        write(out, "purch-rpt22.csv", ["range", "department", "amount"], rpt22)
        write(out, "purch-rpt11.csv", ["range", "table", "po_date", "po_no", "po_suf", "name", "qty", "unit", "tax", "sup_name", "total", "literal", "leftover"], rpt11)

        # RPT-26 and the carried-forward balances of every cut the migration loads (AC-20/21)
        dates = sorted({day(r[0]) for r in fetch(f"SELECT DISTINCT [DATE] FROM [ต้นงวดP_ST] WHERE [DATE] >= {date_literal(CUT_FROM)}")} | set(cuts))
        rpt26, balances = [], []
        for d in dates:
            lit = date_literal(d)
            for r in fetch(f"SELECT P.PD_C, P.QTY, M.NAME, M.U_NAME, M.COST FROM [ต้นงวดP_ST] AS P INNER JOIN MTR_NAME AS M ON P.PD_C = M.CODE WHERE P.[DATE] = {lit} AND P.QTY <> 0"):
                rpt26.append([d, text(r[0]), double(r[1]), text(r[2]), text(r[3]), double(r[4])])
            for r in fetch(f"SELECT PD_C, SUM(QTY), COUNT(*) FROM [ต้นงวดP_ST] WHERE [DATE] = {lit} GROUP BY PD_C"):
                balances.append([d, text(r[0]), double(r[1]), r[2]])
        write(out, "purch-rpt26.csv", ["cut", "code", "qty", "name", "unit", "cost"], rpt26)
        # the rows of Stock dated on or before a cut that are still open (keyed after Access cut that date): a cut made again now takes them in, Access's did not (AC-20)
        backdated = []
        for d in dates:
            for r in fetch(f"SELECT UCASE(MTR_CODE), SUM(RE_QTY), COUNT(*) FROM Stock WHERE RE_DATE <= {date_literal(d)} AND (INVOICE_NO IS NULL OR INVOICE_NO <> 'stock') GROUP BY UCASE(MTR_CODE)"):
                backdated.append([d, text(r[0]), double(r[1]), r[2]])
        write(out, "purch-stock-backdated.csv", ["cut", "code", "qty", "rows"], backdated)
        write(out, "purch-cut-balances.csv", ["cut", "code", "qty", "rows"], balances)

        # AC-07: every row of the orders of the latest year, in both tables
        year = last.year
        orders = []
        for table in ("PO_RECEI4", "PO_RECEI5"):
            for r in fetch(f"SELECT PO_NO, PO_SUF, PO_DATE, U_PRICE, PO_QTY, RE_QTY, NET_AMT, TAX, TOTAL, VAT, SUP_CODE, PASS, RE_DATE FROM {table} "
                           f"WHERE PO_DATE >= {date_literal(f'{year}-01-01')} AND PO_DATE <= {date_literal(f'{year}-12-31')}"):
                orders.append([table, text(r[0]), single(r[1]), day(r[2]), double(r[3]), double(r[4]), double(r[5]), double(r[6]), double(r[7]), double(r[8]), text(r[9]), text(r[10]), text(r[11]), day(r[12])])
        write(out, "purch-orders.csv", ["table", "po_no", "po_suf", "po_date", "u_price", "po_qty", "re_qty", "net_amt", "tax", "total", "vat", "sup_code", "pass", "re_date"], orders)

        # AC-18: the balance of every code = Σ RE_QTY of its rows in Stock (rows without a date are counted apart)
        write(out, "purch-stock.csv", ["code", "qty", "rows", "undated_qty"],
              [[text(r[0]), double(r[1]), r[2], double(r[3])] for r in fetch(
                  "SELECT UCASE(MTR_CODE), SUM(RE_QTY), COUNT(*), SUM(IIF(RE_DATE IS NULL, RE_QTY, 0)) FROM Stock GROUP BY UCASE(MTR_CODE)")])

        # AC-24: the evaluations kept for the quarter, and the counts บันทึกINCOMING gives on the data as it is now
        lo, hi = date_literal(EVALUATION[0]), date_literal(EVALUATION[1])
        write(out, "purch-eval.csv", ["sup", "from", "to", "term", "passed", "failed", "with_cert", "without_cert", "on_time", "late", "point1", "point2"],
              [[text(r[0]), day(r[1]), day(r[2]), text(r[3])] + [int(v or 0) for v in r[4:10]] + [text(r[10]), text(r[11])] for r in fetch(
                  f"SELECT SUP, FROM_DATE, TO_DATE, TERM, EXPR1, EXPR2, EXPR3, EXPR4, EXPR5, EXPR6, POINT1, POINT2 FROM [ประเมินผู้ขาย] WHERE FROM_DATE = {lo} AND TO_DATE = {hi}")])
        now = []
        for r in fetch(f"SELECT R.SUP_NAME, SUM(IIF(R.PASS = 'ผ่าน', 1, 0)), SUM(IIF(R.PASS = 'ไม่ผ่าน', 1, 0)), SUM(IIF(R.NOPASS = 'มี', 1, 0)), SUM(IIF(R.NOPASS = 'ไม่มี', 1, 0)), "
                       f"SUM(IIF(R.SEND = 'ทัน', 1, 0)), SUM(IIF(R.SEND = 'ไม่ทัน', 1, 0)), COUNT(*) "
                       f"FROM SP INNER JOIN PO_RECEI5 AS R ON SP.NAME = R.SUP_NAME WHERE R.RE_DATE >= {lo} AND R.RE_DATE <= {hi} AND R.LOT IS NOT NULL GROUP BY R.SUP_NAME"):
            now.append([text(r[0])] + [int(v or 0) for v in r[1:8]])
        write(out, "purch-eval-now.csv", ["sup", "passed", "failed", "with_cert", "without_cert", "on_time", "late", "rows"], now)
        connection.close()
    finally:
        shutil.rmtree(work, ignore_errors=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())

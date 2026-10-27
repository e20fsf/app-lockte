"""Make a COPY of ผลผลิตและORDER.accdb that holds no real customer, salesperson or employee data (#134, the source of the UAT database the customer can reach).

Usage: python -I make_fake.py <real.accdb> <fake.accdb> [--from-year 2025]

The real file is only copied (never opened for writing); everything below is done to the copy, whose file name must contain 'fake'. Needs the Access database engine (ACE) and pywin32. The script holds
no data; the files it reads and writes stay outside the repository (source/ is ignored).

What it does
- Rows older than the first day of --from-year are deleted from the history tables (ORDER, ODDส่งออก, P_REP*, P_ADDST, ต้นงวดP_ST, the mold cut table): the migration reads nothing older,
  and the copy is a fraction of the size. The master data and the outstanding ORDER (ODD) are kept whole.
- The result tables the reports fill (ODDรวม, สรุปส่งออก*, ยอดขายแต่ละชนิด, ผลผลิตประจำเดือน* …) and the old copies (สำเนาของ …, ODDADD …) are emptied: they hold customers and money
  and are made again by the reports.
- Customers (P_CUS and every CUS_N): name → "ลูกค้าทดสอบ nnnn" (one fake per customer code, the same everywhere); address, district, carrier, phone numbers, postal code → fake values;
  the province is kept (the province report needs it).
- Money: every row of ORDER, ODD and ODDส่งออก has U_PRICE, AMOUNT and DL_NET multiplied by the factor (0.85–1.15) of its customer, so AMOUNT = QTY × U_PRICE still holds;
  P_NAME.U_PRICE and FML, and the cost of a mold, are multiplied by a factor of their own.
- Salespeople (เซล), production employees (EMPO): names → fake names; the personal columns of เซล and every picture → empty.
- Contractors (รับจ้าง) are jobs ("เตารีด# …"), not people, and are kept. Products, molds, machines, lots and quantities are the company's catalog and are kept.
At the end a leak check looks for any value that was real (a name, an address, a phone number) in any text column of any table: it must find 0.
The QA specs are not in this file (they are read from the recovered reports): do not load them onto a database the customer can reach.
"""
import os
import random
import re
import shutil
import sys
import time

import win32com.client

sys.stdout.reconfigure(encoding="utf-8", line_buffering=True)
T0 = time.time()
random.seed(20261027)

FAIL = 128  # dbFailOnError
DB_TEXT, DB_OLE = 10, 11


def log(message):
    print(f"[{time.time() - T0:7.1f}s] {message}")


def q(text):
    return "'" + text.replace("'", "''") + "'"


def key(text):
    return re.sub(r"\s+", "", text or "").upper()


# the tables whose rows are kept only from the first day of --from-year (table, date column)
HISTORY = [("ORDER", "OD_DATE"), ("ODDส่งออก", "DL_DATE"), ("P_REP", "DATE"), ("P_REPไดคาสท์", "DATE"), ("P_REPตกแต่ง", "DATE"), ("P_REPอัดผ้าเบรก", "DATE"),
           ("P_REPฝนผ้าเบรก", "DATE"), ("P_REPผ้าคลัทช์และดิสเบรค", "DATE"), ("P_REPประกอบ", "DATE"), ("P_REPบรรจุ", "DATE"), ("P_ADDST", "DATE"), ("ต้นงวดP_ST", "DATE"),
           ("ตันงวดการใช้งานของพิมพ์0", "DAY")]
# the tables the reports fill, and the old copies: emptied
EMPTIED = ["ODDรวม", "ODDรวม1", "ODDADD", "ODDADD1", "P_ODDADD", "P_IN", "DEL_ODD", "สำเนาของ ODDส่งออก", "สำเนาของ P_CUS", "สำเนาของ P_NAME", "สำเนาของ P_NAME1", "สำเนาของ P_NAME11",
           "สำเนาของ ตันงวดการใช้งานของพิมพ์0", "ผลผลิตเดือน", "ผลผลิตประจำเดือน", "ผลผลิตประจำเดือน120", "ผลผลิตประจำเดือน121", "ยอดขายแต่ละชนิด", "สรุปการรับORDER", "สรุปส่งออก1001",
           "สรุปส่งออก23", "สรุปส่งออกเดือน10B", "สรุปส่งออกเดือน1B", "สรุปส่งออกเดือน400B", "สรุปส่งออกใน-นอก", "สรุปส่งออกตามเซล", "สรุปส่งออกตามชื่อสินค้า", "สรุปส่งออกตามรหัสสินค้า",
           "รายงานส่งออกSALE ORDER1", "รหัสลูกค้า", "รหัสลูกค้าย่อ", "เซล1", "เลือกเซล", "พิมพ์", "ORDERส่งครบ", "ลบORDER", "P_REP1", "P_REP2", "P_ST1", "P_ST2", "P_ST3", "P_ADDREP"]
ORDER_TABLES = ["ORDER", "ODD", "ODDส่งออก"]


def tables(db):
    out = {}
    for i in range(db.TableDefs.Count):
        td = db.TableDefs(i)
        if td.Name.startswith("MSys") or td.Name.startswith("~") or td.Connect:
            continue
        out[td.Name] = {td.Fields(j).Name: td.Fields(j).Type for j in range(td.Fields.Count)}
    return out


def rows(db, sql):
    recordset = db.OpenRecordset(sql, 4)
    if recordset.EOF:
        recordset.Close()
        return []
    columns = recordset.GetRows(10_000_000)
    recordset.Close()
    return list(zip(*columns))


def fake_digits_like(value):
    return re.sub(r"[0-9]", lambda _: str(random.randint(0, 9)), value)


def main(real, fake, from_year):
    if "fake" not in os.path.basename(fake).lower():
        sys.exit("refusing: the copy's file name must contain 'fake'")
    if os.path.abspath(real) == os.path.abspath(fake):
        sys.exit("refusing: the copy must be another file")
    if os.path.exists(fake):
        os.remove(fake)
    log("copying the file")
    shutil.copyfile(real, fake)
    os.chmod(fake, 0o666)
    engine = win32com.client.Dispatch("DAO.DBEngine.120")  # DAO directly: the file opens at once and can be written (through Access.Application it opened read-only)
    db = engine.OpenDatabase(os.path.abspath(fake), False, False)
    try:
        leaks = run(db, from_year)
    finally:
        db.Close()
    log("compacting")
    compacted = os.path.abspath(fake) + ".compact"
    if os.path.exists(compacted):
        os.remove(compacted)
    engine.CompactDatabase(os.path.abspath(fake), compacted)
    os.replace(compacted, fake)
    log(f"done: {os.path.getsize(fake) / 1e6:.0f} MB, leak check {leaks} leftover values")
    return 0 if leaks == 0 else 1


def run(db, from_year):
    all_tables = tables(db)
    first_day = f"#{from_year}-01-01#"

    log("step 1: the history before the year, and the tables the reports fill")
    for name, column in HISTORY:
        if name in all_tables:
            db.Execute(f"DELETE FROM [{name}] WHERE [{column}] < {first_day}", FAIL)
            log(f"  trimmed {name}")
    for name in dict.fromkeys(EMPTIED):
        if name in all_tables:
            db.Execute(f"DELETE FROM [{name}]", FAIL)

    log("step 2: who is real")
    real_values = {}  # key of every value that was real (names, addresses, phones) -> what it was
    customers = rows(db, "SELECT CODE, NAME, ADDRESS, ADDRESS1, P_CODE, ADD1, ADDDE, ADDDE1, TEL_1, TEL_2 FROM P_CUS")
    for r in customers:
        for kind, v in zip(("name", "address", "district", "postal code", "carrier", "carrier address", "carrier phone", "phone", "phone"), r[1:]):
            if isinstance(v, str) and len(key(v)) >= 4 and kind != "postal code":  # a postal code is a place, not a person: it is not a leak
                real_values.setdefault(key(v), kind)

    # a fake for every customer code of the master and of the order tables
    fake_name, factor = {}, {}
    for r in customers:
        code = (r[0] or "").strip()
        if code and code not in fake_name:
            fake_name[code] = f"ลูกค้าทดสอบ {len(fake_name) + 1:04d}"
            factor[code] = round(random.uniform(0.85, 1.15), 4)
    for table in ORDER_TABLES:
        if table not in all_tables:
            continue
        for code, name in rows(db, f"SELECT DISTINCT CUS_ID, CUS_N FROM [{table}] WHERE CUS_ID IS NOT NULL"):
            code = (code or "").strip()
            if isinstance(name, str) and len(key(name)) >= 4:
                real_values.setdefault(key(name), "customer name")
            if code and code not in fake_name:
                fake_name[code] = f"ลูกค้าทดสอบ {len(fake_name) + 1:04d}"
                factor[code] = round(random.uniform(0.85, 1.15), 4)
    log(f"  customers: {len(fake_name)}")

    # a customer "code" that is really a typed name (the legacy let the user type anything there) becomes a code
    renamed = {}
    for code in list(fake_name):
        if len(key(code)) >= 4 and key(code) in real_values:
            renamed[code] = f"รหัสทดสอบ{len(renamed) + 1:03d}"
            fake_name[renamed[code]], factor[renamed[code]] = fake_name.pop(code), factor.pop(code)
    for old, new in renamed.items():
        for table in ORDER_TABLES + ["P_NAME"]:
            col = "CUS_ID"
            if table in all_tables and col in all_tables[table]:
                db.Execute(f"UPDATE [{table}] SET [{col}] = {q(new)} WHERE [{col}] = {q(old)}", FAIL)
    name_fake = {}
    for r in customers:
        if isinstance(r[1], str) and (r[0] or "").strip() in fake_name:
            name_fake.setdefault(key(r[1]), fake_name[(r[0] or "").strip()])
    db.Execute("CREATE TABLE zz_cus (CODE TEXT(50) CONSTRAINT pk PRIMARY KEY, FNAME TEXT(60), F DOUBLE)", FAIL)
    for code, name in fake_name.items():
        db.Execute(f"INSERT INTO zz_cus VALUES ({q(code)}, {q(name)}, {factor[code]})", FAIL)

    log("step 3: customers")
    db.Execute("UPDATE P_CUS INNER JOIN zz_cus ON P_CUS.CODE = zz_cus.CODE SET P_CUS.NAME = zz_cus.FNAME", FAIL)
    db.Execute("UPDATE P_CUS SET REMARK = 'ที่อยู่ลูกค้าทดสอบ' WHERE REMARK IS NOT NULL", FAIL)
    for column, make in (("ADDRESS", lambda: f"{random.randint(1, 199)}/{random.randint(1, 99)}  หมู่  {random.randint(1, 12)}  ต.ทดสอบ"),
                         ("ADDRESS1", lambda: "เมืองทดสอบ"), ("ADD1", lambda: f"ขนส่งทดสอบ {random.randint(1, 99)}"), ("ADDDE", lambda: f"ถนนทดสอบ {random.randint(1, 99)}")):
        for (value,) in rows(db, f"SELECT DISTINCT [{column}] FROM P_CUS WHERE [{column}] IS NOT NULL"):
            if value.strip():
                db.Execute(f"UPDATE P_CUS SET [{column}] = {q(make())} WHERE [{column}] = {q(value)}", FAIL)
    for column in ("ADDDE1", "TEL_1", "TEL_2", "P_CODE"):
        for (value,) in rows(db, f"SELECT DISTINCT [{column}] FROM P_CUS WHERE [{column}] IS NOT NULL"):
            if value.strip():
                db.Execute(f"UPDATE P_CUS SET [{column}] = {q(fake_digits_like(value))} WHERE [{column}] = {q(value)}", FAIL)
    for table in ORDER_TABLES:
        if table in all_tables:
            db.Execute(f"UPDATE [{table}] INNER JOIN zz_cus ON [{table}].CUS_ID = zz_cus.CODE SET [{table}].CUS_N = zz_cus.FNAME", FAIL)
            db.Execute(f"UPDATE [{table}] SET REMARK = 'ที่อยู่ลูกค้าทดสอบ' WHERE REMARK IS NOT NULL", FAIL)
    db.Execute("UPDATE P_NAME INNER JOIN zz_cus ON P_NAME.CUS_ID = zz_cus.CODE SET P_NAME.CUS = zz_cus.FNAME", FAIL)
    # what a join by code did not reach: a name that is still real is replaced by the fake of that customer, or by a new one
    for table, column in [(t, "CUS_N") for t in ORDER_TABLES] + [("P_NAME", "CUS")]:
        for (value,) in rows(db, f"SELECT DISTINCT [{column}] FROM [{table}] WHERE [{column}] IS NOT NULL"):
            if isinstance(value, str) and key(value) in real_values:
                new = name_fake.setdefault(key(value), f"ลูกค้าทดสอบ {len(fake_name) + len(name_fake) + 1:04d}")
                db.Execute(f"UPDATE [{table}] SET [{column}] = {q(new)} WHERE [{column}] = {q(value)}", FAIL)

    log("step 4: money")
    for table in ORDER_TABLES:
        if table in all_tables:
            db.Execute(f"UPDATE [{table}] INNER JOIN zz_cus ON [{table}].CUS_ID = zz_cus.CODE SET [{table}].U_PRICE = Round([{table}].U_PRICE * zz_cus.F, 2), "
                       f"[{table}].AMOUNT = Round([{table}].AMOUNT * zz_cus.F, 2), [{table}].DL_NET = Round([{table}].DL_NET * zz_cus.F, 2)", FAIL)
            log(f"  scaled {table}")
    db.Execute("CREATE TABLE zz_prod (PD_C TEXT(50) CONSTRAINT pk PRIMARY KEY, F DOUBLE)", FAIL)
    for (code,) in rows(db, "SELECT DISTINCT PD_C FROM P_NAME WHERE PD_C IS NOT NULL"):
        db.Execute(f"INSERT INTO zz_prod VALUES ({q(code.strip())}, {round(random.uniform(0.85, 1.15), 4)})", FAIL)
    db.Execute("UPDATE P_NAME INNER JOIN zz_prod ON P_NAME.PD_C = zz_prod.PD_C SET P_NAME.U_PRICE = Round(P_NAME.U_PRICE * zz_prod.F, 2), P_NAME.FML = Round(P_NAME.FML * zz_prod.F, 4)", FAIL)
    db.Execute("UPDATE [รหัสพิมพ์] SET COST = CLng(COST * (0.85 + (Len([ID]) Mod 7) * 0.05)) WHERE COST IS NOT NULL", FAIL)

    log("step 5: people")
    # salespeople: the office and online ones are not people
    for code, name in rows(db, "SELECT ID, NAME FROM เซล"):
        if isinstance(name, str) and name.strip() and name.strip() not in ("ออฟฟิศ", "เซลออนไลน์"):
            real_values.setdefault(key(name), "salesperson")
            db.Execute(f"UPDATE เซล SET NAME = {q('เซลทดสอบ ' + code)} WHERE ID = {q(code)}", FAIL)
    for column in ("SEX", "ID_CODE", "PER_ADD", "ID_PLACE", "ADDRESS", "ID_S", "ACC", "HOSPITAL", "MEM", "BORN", "ID_DATE", "START", "RESIGN", "DATE_1", "DATE_2", "DATE_3"):
        if column in all_tables["เซล"]:
            db.Execute(f"UPDATE เซล SET [{column}] = Null", FAIL)
    # production employees
    seen = {}
    for code, name in rows(db, "SELECT ID, NAME FROM EMPO"):
        if isinstance(name, str) and name.strip():
            real_values.setdefault(key(name), "employee")
            new = seen.setdefault(key(name), f"พนักงานทดสอบ {len(seen) + 1:03d}")
            db.Execute(f"UPDATE EMPO SET NAME = {q(new)} WHERE NAME = {q(name)}", FAIL)

    log("step 6: pictures")
    for table, columns in all_tables.items():
        for column, kind in columns.items():
            if kind == DB_OLE:
                db.Execute(f"UPDATE [{table}] SET [{column}] = Null", FAIL)

    log("step 6b: a real value left in any other column (a maker of molds who is also a customer, a name copied into a remark)")
    scrub = {}
    for table, columns in all_tables.items():
        for column, kind in columns.items():
            if kind != DB_TEXT:
                continue
            try:
                values = rows(db, f"SELECT DISTINCT [{column}] FROM [{table}] WHERE [{column}] IS NOT NULL")
            except Exception:
                continue
            hits = [v for (v,) in values if isinstance(v, str) and key(v) in real_values]
            for value in hits:
                new = scrub.setdefault(key(value), f"ข้อมูลทดสอบ {len(scrub) + 1:04d}")
                db.Execute(f"UPDATE [{table}] SET [{column}] = {q(new)} WHERE [{column}] = {q(value)}", FAIL)
            if hits:
                log(f"  scrubbed {table}.{column}: {len(hits)} values")

    db.Execute("DROP TABLE zz_cus", FAIL)
    db.Execute("DROP TABLE zz_prod", FAIL)

    log("step 7: leak check")
    leaks = 0
    for table, columns in all_tables.items():
        for column, kind in columns.items():
            if kind != DB_TEXT:
                continue
            try:
                values = rows(db, f"SELECT DISTINCT [{column}] FROM [{table}] WHERE [{column}] IS NOT NULL")
            except Exception:
                continue
            for (value,) in values:
                if isinstance(value, str) and key(value) in real_values:
                    leaks += 1
                    log(f"  LEAK {table}.{column} (a real {real_values[key(value)]}, {len(value)} characters)")
    return leaks


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    year = int(sys.argv[sys.argv.index("--from-year") + 1]) if "--from-year" in sys.argv else 2025
    if len(args) < 2:
        sys.exit(__doc__)
    sys.exit(main(args[0], args[1], year))

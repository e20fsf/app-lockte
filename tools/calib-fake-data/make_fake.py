"""Make a COPY of สอบเทียบ.accdb that holds no real name of a calibrator or a calibration company (#210, for fixtures and demos that must not carry real data).

Usage: python -I make_fake.py <real.accdb> <fake.accdb>

The real file is only copied (never opened for writing); everything below is done to the copy, whose file name must contain 'fake'. Needs the Access database engine (ACE) and pywin32. The script
holds no data; the files it reads and writes stay outside the repository (source/ is ignored).

What it does
- Calibrators (the ผู้สอบเทียบ column of CALIPER, PRESSURE GAUGE, เครื่องชั่ง, ตัวควบคุมอุณหภูมิ, jig): every distinct name -> "ผู้สอบเทียบทดสอบ n", the same fake everywhere for the same person.
- Calibration companies (the lookup table บริษัทที่สอบเทียบ and the ผู้สอบเทียบ column of ภายนอก): every distinct name -> "บริษัทสอบเทียบทดสอบ n".
- Everything else is kept: the row counts (20 / 13 / 53 / 10 / 67 / 59 and the lookup), the codes, and the groups of data the migration has to handle — the dates with the wrong year (19xx, 25xx, 4722), the blank
  unit, the malformed standard instruments, the departments such as `DC (D02)` and `BL(32,33)`, the codes such as `JG-AS001-2/2560`.
At the end a leak check looks for any value that was a real name in any text column of any table (the notes too): it must find 0.
"""
import os
import re
import shutil
import sys
import time

import win32com.client

sys.stdout.reconfigure(encoding="utf-8", line_buffering=True)
T0 = time.time()

FAIL = 128  # dbFailOnError
DB_TEXT = 10

PEOPLE = [("CALIPER", "ผู้สอบเทียบ"), ("PRESSURE GAUGE", "ผู้สอบเทียบ"), ("เครื่องชั่ง", "ผู้สอบเทียบ"), ("ตัวควบคุมอุณหภูมิ", "ผู้สอบเทียบ"), ("jig", "ผู้สอบเทียบ")]
COMPANIES = [("ภายนอก", "ผู้สอบเทียบ"), ("บริษัทที่สอบเทียบ", "NAME")]


def log(message):
    print(f"[{time.time() - T0:7.1f}s] {message}")


def q(text):
    return "'" + text.replace("'", "''") + "'"


def key(text):
    return re.sub(r"\s+", "", text or "").upper()


def tables(db):
    out = {}
    for i in range(db.TableDefs.Count):
        td = db.TableDefs(i)
        if td.Name.startswith("MSys") or td.Name.startswith("~") or td.Connect:
            continue
        out[td.Name] = {td.Fields(j).Name: td.Fields(j).Type for j in range(td.Fields.Count)}
    return out


def distinct(db, table, column):
    recordset = db.OpenRecordset(f"SELECT DISTINCT [{column}] FROM [{table}] WHERE [{column}] IS NOT NULL", 4)
    if recordset.EOF:
        recordset.Close()
        return []
    values = [row[0] for row in zip(*recordset.GetRows(10_000_000))]
    recordset.Close()
    return [v for v in values if isinstance(v, str) and v.strip()]


def main(real, fake):
    if "fake" not in os.path.basename(fake).lower():
        sys.exit("refusing: the copy's file name must contain 'fake'")
    if os.path.abspath(real) == os.path.abspath(fake):
        sys.exit("refusing: the copy must be another file")
    if os.path.exists(fake):
        os.remove(fake)
    log("copying the file")
    shutil.copyfile(real, fake)
    os.chmod(fake, 0o666)
    engine = win32com.client.Dispatch("DAO.DBEngine.120")  # DAO directly: the file opens at once and can be written
    db = engine.OpenDatabase(os.path.abspath(fake), False, False)
    try:
        leaks = run(db)
    finally:
        db.Close()
    log("compacting")
    compacted = os.path.abspath(fake) + ".compact"
    if os.path.exists(compacted):
        os.remove(compacted)
    engine.CompactDatabase(os.path.abspath(fake), compacted)
    os.replace(compacted, fake)
    log(f"done: {os.path.getsize(fake) / 1e6:.1f} MB, leak check {leaks} leftover values")
    return 0 if leaks == 0 else 1


def run(db):
    real = {}  # key(real name) -> what it was ("calibrator" / "company")
    fakes = {}

    def fake_for(name, kind):
        found = fakes.get(key(name))
        if found is None:
            number = sum(1 for k in fakes.values() if k.startswith(kind)) + 1
            found = fakes[key(name)] = f"{kind} {number}"
        real[key(name)] = "calibrator" if kind.startswith("ผู้") else "company"
        return found

    log("step 1: calibrators and companies -> fake names")
    for kind, group in (("ผู้สอบเทียบทดสอบ", PEOPLE), ("บริษัทสอบเทียบทดสอบ", COMPANIES)):
        for table, column in group:
            for name in distinct(db, table, column):
                db.Execute(f"UPDATE [{table}] SET [{column}] = {q(fake_for(name, kind))} WHERE [{column}] = {q(name)}", FAIL)
            log(f"  {table}.{column}: done")

    log("step 2: a real name left in any other text column (a name copied into a remark)")
    all_tables = tables(db)
    for table, columns in all_tables.items():
        for column, kind in columns.items():
            if kind != DB_TEXT:
                continue
            for value in distinct(db, table, column):
                if key(value) in real:
                    db.Execute(f"UPDATE [{table}] SET [{column}] = {q(fake_for(value, 'ข้อมูลทดสอบ'))} WHERE [{column}] = {q(value)}", FAIL)

    log("step 3: leak check")
    leaks = 0
    for table, columns in all_tables.items():
        for column, kind in columns.items():
            if kind != DB_TEXT:
                continue
            for value in distinct(db, table, column):
                if key(value) in real:
                    leaks += 1
                    log(f"  LEAK {table}.{column} (a real {real[key(value)]}, {len(value)} characters)")
    return leaks


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    if len(args) < 2:
        sys.exit(__doc__)
    sys.exit(main(args[0], args[1]))

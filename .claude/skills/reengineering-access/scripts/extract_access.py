"""Extract everything needed to write a spec from an MS Access database.

Dumps tables, relationships, query SQL, forms/reports (SaveAsText + VBA +
summary), modules, macros, linked tables and sample rows into text files.

Requires Windows, MS Access, and pywin32. Opens the database with VBA/macros
disabled so AutoExec and startup code do not run.

Usage:
    py extract_access.py <file.accdb> [--out DIR] [--sample-rows N] [--no-samples]
"""

import argparse
import csv
import datetime as dt
import os
import re
import sys
import tempfile
import time
from pathlib import Path

import pythoncom
import win32com.client

# Access object type constants for SaveAsText
AC_QUERY, AC_FORM, AC_REPORT, AC_MACRO, AC_MODULE = 1, 2, 3, 4, 5

DAO_TYPES = {
    1: "Yes/No", 2: "Byte", 3: "Integer", 4: "Long", 5: "Currency",
    6: "Single", 7: "Double", 8: "Date/Time", 9: "Binary", 10: "Text",
    11: "OLE Object", 12: "Memo/Long Text", 15: "GUID", 16: "BigInt",
    17: "VarBinary", 18: "Char", 19: "Numeric", 20: "Decimal", 21: "Float",
    22: "Time", 23: "TimeStamp", 101: "Attachment", 102: "Complex Byte",
    103: "Complex Integer", 104: "Complex Long", 105: "Complex Single",
    106: "Complex Double", 107: "Complex GUID", 108: "Complex Decimal",
    109: "Complex Text",
}
QUERY_TYPES = {
    0: "SELECT", 16: "CROSSTAB", 32: "DELETE", 48: "UPDATE", 64: "APPEND",
    80: "MAKE-TABLE", 96: "DDL", 112: "PASS-THROUGH", 128: "UNION",
    144: "PASS-THROUGH (no rows)", 224: "COMPOUND", 240: "PROCEDURE", 256: "ACTION",
}
DB_AUTO_INCR = 16
REL_DONT_ENFORCE, REL_UPDATE_CASCADE, REL_DELETE_CASCADE = 2, 256, 4096
REL_LEFT, REL_RIGHT, REL_UNIQUE = 16777216, 33554432, 1

CONTROL_TYPES = {
    "TextBox", "ComboBox", "ListBox", "CheckBox", "OptionGroup", "OptionButton",
    "ToggleButton", "CommandButton", "Subform", "Subreport", "Label",
    "Image", "BoundObjectFrame", "ObjectFrame", "Attachment", "TabControl",
    "Page", "WebBrowser", "NavigationControl", "NavigationButton",
}
SECTION_TYPES = {"Section", "FormHeader", "FormFooter", "PageHeader",
                 "PageFooter", "BreakHeader", "BreakFooter"}
INTERESTING_PROPS = {
    "RecordSource", "ControlSource", "RowSource", "RowSourceType",
    "DefaultValue", "ValidationRule", "ValidationText", "InputMask", "Format",
    "SourceObject", "LinkMasterFields", "LinkChildFields", "Filter", "OrderBy",
    "Caption", "Locked", "Enabled", "Visible", "BoundColumn", "LimitToList",
    "ControlType", "ControlSourceType", "ColumnCount", "DataEntry",
    "AllowEdits", "AllowAdditions", "AllowDeletions", "ControlSource",
    "GroupOn", "SortOrder",
}

INVALID_FN = re.compile(r'[<>:"/\\|?*\x00-\x1f]')


def safe_name(name):
    return INVALID_FN.sub("_", name).strip().rstrip(".") or "_"


def md_cell(value):
    if value is None:
        return ""
    return str(value).replace("|", "\\|").replace("\r", " ").replace("\n", " ")


def read_saved_text(path):
    """SaveAsText writes UTF-16 (forms/reports, BOM) or ANSI (modules)."""
    raw = Path(path).read_bytes()
    if raw.startswith(b"\xff\xfe") or raw.startswith(b"\xfe\xff"):
        return raw.decode("utf-16")
    if raw.startswith(b"\xef\xbb\xbf"):
        return raw[3:].decode("utf-8")
    for enc in ("utf-8", "cp874", "mbcs"):
        try:
            return raw.decode(enc)
        except (UnicodeDecodeError, LookupError):
            continue
    return raw.decode("latin-1")


def prop(obj, name, default=None):
    try:
        return obj.Properties(name).Value
    except Exception:
        return default


def log(msg):
    print(msg, flush=True)


class Extractor:
    def __init__(self, db_path, out_dir, sample_rows):
        self.db_path = str(Path(db_path).resolve())
        self.out = Path(out_dir)
        self.sample_rows = sample_rows
        self.tmp = Path(tempfile.mkdtemp(prefix="accx_"))
        self.inventory = {}
        self.errors = []

    def open(self):
        pythoncom.CoInitialize()
        self.app = win32com.client.DispatchEx("Access.Application")
        self.app.Visible = False
        self.app.AutomationSecurity = 3  # msoAutomationSecurityForceDisable
        self.app.OpenCurrentDatabase(self.db_path, False)
        self.db = self.app.CurrentDb()

    def close(self):
        try:
            self.app.CloseCurrentDatabase()
        except Exception:
            pass
        try:
            self.app.Quit(2)  # acQuitSaveNone
        except Exception:
            pass

    def write(self, rel, text):
        p = self.out / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8", newline="\n")
        return p

    def save_as_text(self, obj_type, name):
        tmp = self.tmp / f"obj_{time.time_ns()}.txt"
        self.app.SaveAsText(obj_type, name, str(tmp))
        text = read_saved_text(tmp)
        tmp.unlink(missing_ok=True)
        return text

    # ---------- tables ----------
    def tables(self):
        lines = ["# Tables\n"]
        linked = []
        names = []
        for td in self.db.TableDefs:
            name = td.Name
            if name.startswith("MSys") or name.startswith("~") or name.startswith("USys"):
                continue
            names.append(name)
            connect = td.Connect or ""
            if connect:
                linked.append((name, td.SourceTableName, connect))
            try:
                count = td.RecordCount if not connect else None
                if count in (None, -1):
                    rs = self.db.OpenRecordset(f"SELECT COUNT(*) FROM [{name}]")
                    count = rs.Fields(0).Value
                    rs.Close()
            except Exception as e:
                count = f"? ({e.__class__.__name__})"
            desc = prop(td, "Description", "")
            lines.append(f"\n## {name}\n")
            if desc:
                lines.append(f"Description: {desc}\n")
            if connect:
                lines.append(f"**Linked** → `{td.SourceTableName}` via `{connect}`\n")
            lines.append(f"Rows: {count}\n")
            vr = prop(td, "ValidationRule", "")
            if vr:
                lines.append(f"Table validation: `{vr}` — {prop(td, 'ValidationText', '')}\n")
            lines.append("\n| # | Field | Type | Size | Required | AllowZeroLen | Default | Validation | Description |")
            lines.append("|---|---|---|---|---|---|---|---|---|")
            for i, f in enumerate(td.Fields):
                ftype = DAO_TYPES.get(f.Type, f"type {f.Type}")
                if f.Type == 4 and (f.Attributes & DB_AUTO_INCR):
                    ftype = "AutoNumber"
                validation = f.ValidationRule or ""
                if f.ValidationText:
                    validation += f" — {f.ValidationText}"
                row_src = prop(f, "RowSource", "")
                fdesc = prop(f, "Description", "") or ""
                if row_src:
                    fdesc = (fdesc + f" [Lookup: {row_src}]").strip()
                lines.append("| " + " | ".join(md_cell(x) for x in [
                    i + 1, f.Name, ftype, f.Size if f.Type in (10, 18) else "",
                    "yes" if f.Required else "", "yes" if f.Type in (10, 12) and f.AllowZeroLength else "",
                    f.DefaultValue, validation, fdesc]) + " |")
            try:
                idx_lines = []
                for ix in td.Indexes:
                    fields = ", ".join(fl.Name for fl in ix.Fields)
                    flags = [x for x, on in (("PK", ix.Primary), ("unique", ix.Unique),
                                             ("FK", ix.Foreign)) if on]
                    idx_lines.append(f"- `{ix.Name}`: ({fields}) {' '.join(flags)}")
                if idx_lines:
                    lines.append("\nIndexes:\n" + "\n".join(idx_lines))
            except Exception:
                pass
        self.write("tables.md", "\n".join(lines) + "\n")
        if linked:
            self.write("linked-tables.md", "# Linked tables\n\n| Local name | Source table | Connect |\n|---|---|---|\n"
                       + "\n".join(f"| {md_cell(a)} | {md_cell(b)} | {md_cell(c)} |" for a, b, c in linked) + "\n")
        self.inventory["Tables"] = names
        self.inventory["Linked tables"] = [n for n, _, _ in linked]
        return names, {n for n, _, _ in linked}

    def relationships(self):
        lines = ["# Relationships\n",
                 "| Name | Parent (one) | Child (many) | Fields | Enforced | Cascade update | Cascade delete | Join | 1:1 |",
                 "|---|---|---|---|---|---|---|---|---|"]
        count = 0
        for r in self.db.Relations:
            if r.Table.startswith("MSys"):
                continue
            a = r.Attributes
            fields = ", ".join(f"{f.Name} → {f.ForeignName}" for f in r.Fields)
            join = "left" if a & REL_LEFT else "right" if a & REL_RIGHT else "inner"
            lines.append("| " + " | ".join(md_cell(x) for x in [
                r.Name, r.Table, r.ForeignTable, fields,
                "no" if a & REL_DONT_ENFORCE else "yes",
                "yes" if a & REL_UPDATE_CASCADE else "", "yes" if a & REL_DELETE_CASCADE else "",
                join, "yes" if a & REL_UNIQUE else ""]) + " |")
            count += 1
        if not count:
            lines.append("\n_No relationships declared. Infer them from JOINs in queries and matching field names._")
        self.write("relationships.md", "\n".join(lines) + "\n")
        self.inventory["Relationships"] = [str(count)]

    def queries(self):
        names = []
        for qd in self.db.QueryDefs:
            name = qd.Name
            if name.startswith("~"):  # embedded SQL of forms/reports/combos
                continue
            names.append(name)
            qtype = QUERY_TYPES.get(qd.Type, f"type {qd.Type}")
            params = []
            try:
                params = [f"{p.Name} ({DAO_TYPES.get(p.Type, p.Type)})" for p in qd.Parameters]
            except Exception:
                pass
            desc = prop(qd, "Description", "")
            header = f"-- Query: {name}\n-- Type: {qtype}\n"
            if params:
                header += f"-- Parameters: {', '.join(params)}\n"
            if desc:
                header += f"-- Description: {desc}\n"
            self.write(f"queries/{safe_name(name)}.sql", header + "\n" + (qd.SQL or "").strip() + "\n")
        self.inventory["Queries"] = names

    # ---------- forms / reports ----------
    def form_like(self, kind, collection, obj_type):
        names = [o.Name for o in collection]
        summary = [f"# {kind.capitalize()} summary\n"]
        for name in names:
            try:
                text = self.save_as_text(obj_type, name)
            except Exception as e:
                self.errors.append(f"{kind}/{name}: {e}")
                continue
            fn = safe_name(name)
            self.write(f"{kind}/{fn}.txt", text)
            code_idx = text.find("CodeBehindForm")
            if code_idx >= 0:
                code = text[code_idx + len("CodeBehindForm"):].lstrip("\r\n")
                code = "\n".join(l for l in code.splitlines() if not l.startswith("Attribute VB_"))
                self.write(f"{kind}/{fn}.vba", code.strip() + "\n")
            summary.append(self.summarize_form(name, text[:code_idx] if code_idx >= 0 else text,
                                               has_code=code_idx >= 0, kind=kind))
        self.write(f"{kind}/_summary.md", "\n".join(summary) + "\n")
        self.inventory[kind.capitalize()] = names

    def summarize_form(self, name, text, has_code, kind):
        """Parse SaveAsText's Begin/End tree into a readable summary."""
        stack = []          # list of (type, props)
        objects = []        # (type, props) for root, sections and controls
        cont_key = None
        for raw in text.splitlines():
            line = raw.strip()
            if not line:
                continue
            m_begin = re.match(r"^Begin\s*(\w*)$", line)
            if m_begin:
                obj = (m_begin.group(1) or "_block", {})
                stack.append(obj)
                if obj[0] in CONTROL_TYPES or obj[0] in SECTION_TYPES or obj[0] in ("Form", "Report", "BreakLevel"):
                    objects.append(obj)
                cont_key = None
                continue
            if line == "End":
                if stack:
                    stack.pop()
                cont_key = None
                continue
            m_prop = re.match(r"^(\w+)\s*=\s*(.*)$", line)
            if m_prop and stack:
                key, val = m_prop.group(1), m_prop.group(2).strip()
                cur = stack[-1][1]
                if key not in cur:
                    cur[key] = val.strip('"') if val.startswith('"') else val
                cont_key = key
            elif line.startswith('"') and cont_key and stack:
                # continuation line of a long string property
                stack[-1][1][cont_key] = stack[-1][1].get(cont_key, "") + line.strip('"')

        out = [f"\n## {name}\n"]
        root = next((p for t, p in objects if t in ("Form", "Report")), {})
        for k in ("RecordSource", "Filter", "OrderBy", "Caption", "DefaultView", "DataEntry",
                  "AllowEdits", "AllowAdditions", "AllowDeletions"):
            if root.get(k):
                out.append(f"- {k}: `{root[k]}`")
        events = {k: v for k, v in root.items() if k.startswith("On") or k in ("BeforeUpdate", "AfterUpdate", "BeforeInsert", "AfterInsert")}
        if events:
            out.append("- Form events: " + ", ".join(f"{k}={v}" for k, v in events.items()))
        if has_code:
            out.append(f"- Has VBA: see `{safe_name(name)}.vba`")
        breaks = [p for t, p in objects if t == "BreakLevel"]
        if breaks:
            out.append("- Sorting/Grouping: " + "; ".join(
                f"{b.get('ControlSource', '?')}{' (group)' if b.get('GroupHeader') or b.get('GroupFooter') else ''}"
                f"{' desc' if b.get('SortOrder') in ('-1', 'NotDefault') else ''}" for b in breaks))
        rows = []
        for t, p in objects:
            if t not in CONTROL_TYPES or t == "Label" and not p.get("ControlSource"):
                continue
            ev = ", ".join(f"{k}={v}" for k, v in p.items()
                           if (k.startswith("On") or k in ("BeforeUpdate", "AfterUpdate")) and v)
            detail = "; ".join(f"{k}={p[k]}" for k in ("RowSource", "DefaultValue", "ValidationRule",
                                                      "ValidationText", "SourceObject", "LinkMasterFields",
                                                      "LinkChildFields", "Format", "InputMask")
                               if p.get(k))
            if t == "CommandButton" and not ev:
                continue
            rows.append("| " + " | ".join(md_cell(x) for x in [
                t, p.get("Name", ""), p.get("ControlSource", ""), p.get("Caption", ""), detail, ev]) + " |")
        if rows:
            out.append("\n| Type | Name | ControlSource | Caption | Details | Events |")
            out.append("|---|---|---|---|---|---|")
            out.extend(rows)
        return "\n".join(out)

    def modules_and_macros(self):
        mods = [o.Name for o in self.app.CurrentProject.AllModules]
        for name in mods:
            try:
                text = self.save_as_text(AC_MODULE, name)
                text = "\n".join(l for l in text.splitlines() if not l.startswith("Attribute VB_"))
                self.write(f"modules/{safe_name(name)}.bas", text.strip() + "\n")
            except Exception as e:
                self.errors.append(f"module/{name}: {e}")
        self.inventory["Modules"] = mods
        macros = [o.Name for o in self.app.CurrentProject.AllMacros]
        for name in macros:
            try:
                self.write(f"macros/{safe_name(name)}.txt", self.save_as_text(AC_MACRO, name))
            except Exception as e:
                self.errors.append(f"macro/{name}: {e}")
        self.inventory["Macros"] = macros

    # ---------- samples ----------
    def samples(self, tables, linked):
        for name in tables:
            if name in linked:
                continue
            try:
                rs = self.db.OpenRecordset(f"SELECT TOP {self.sample_rows} * FROM [{name}]", 4)  # dbOpenSnapshot
                cols = [rs.Fields(i).Name for i in range(rs.Fields.Count)]
                rows = []
                while not rs.EOF and len(rows) < self.sample_rows:
                    row = []
                    for i in range(len(cols)):
                        try:
                            v = rs.Fields(i).Value
                        except Exception:
                            v = "<unreadable>"
                        if isinstance(v, (bytes, memoryview)):
                            v = f"<binary {len(v)} bytes>"
                        elif hasattr(v, "Count") or (v is not None and not isinstance(v, (str, int, float, bool, dt.datetime))):
                            v = f"<{type(v).__name__}>"
                        row.append(v)
                    rows.append(row)
                    rs.MoveNext()
                rs.Close()
                p = self.out / "samples" / f"{safe_name(name)}.csv"
                p.parent.mkdir(parents=True, exist_ok=True)
                with p.open("w", encoding="utf-8-sig", newline="") as fh:
                    w = csv.writer(fh)
                    w.writerow(cols)
                    w.writerows(rows)
            except Exception as e:
                self.errors.append(f"sample/{name}: {e}")

    def startup_info(self):
        info = {}
        for key in ("StartupForm", "AppTitle", "StartupShowDBWindow"):
            v = prop(self.db, key)
            if v not in (None, ""):
                info[key] = v
        if "AutoExec" in self.inventory.get("Macros", []):
            info["AutoExec macro"] = "yes (see macros/AutoExec.txt)"
        return info

    def write_inventory(self, startup, elapsed):
        size_mb = os.path.getsize(self.db_path) / 1024 / 1024
        lines = [f"# Inventory — {Path(self.db_path).name}\n",
                 f"- Size: {size_mb:,.1f} MB",
                 f"- Extracted: {dt.datetime.now():%Y-%m-%d %H:%M} in {elapsed:.0f}s"]
        for k, v in startup.items():
            lines.append(f"- {k}: `{v}`")
        lines.append("\n| Object type | Count |\n|---|---|")
        for k, v in self.inventory.items():
            lines.append(f"| {k} | {v[0] if k == 'Relationships' else len(v)} |")
        for k, v in self.inventory.items():
            if k == "Relationships" or not v:
                continue
            lines.append(f"\n## {k}\n")
            lines.extend(f"- [ ] {n}" for n in v)
        if self.errors:
            lines.append("\n## Extraction errors\n")
            lines.extend(f"- {e}" for e in self.errors)
        self.write("INVENTORY.md", "\n".join(lines) + "\n")

    def run(self, do_samples):
        t0 = time.time()
        self.out.mkdir(parents=True, exist_ok=True)
        log(f"Opening {self.db_path} ...")
        self.open()
        try:
            log("Tables ..."); tables, linked = self.tables()
            log("Relationships ..."); self.relationships()
            log("Queries ..."); self.queries()
            log("Forms ..."); self.form_like("forms", self.app.CurrentProject.AllForms, AC_FORM)
            log("Reports ..."); self.form_like("reports", self.app.CurrentProject.AllReports, AC_REPORT)
            log("Modules & macros ..."); self.modules_and_macros()
            if do_samples:
                log("Samples ..."); self.samples(tables, linked)
            startup = self.startup_info()
        finally:
            self.close()
        self.write_inventory(startup, time.time() - t0)
        log(f"Done → {self.out}  ({len(self.errors)} errors)")


def main():
    # Thai object/file names crash print() on a non-UTF-8 console (e.g. cp1252 when piped)
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("db")
    ap.add_argument("--out")
    ap.add_argument("--sample-rows", type=int, default=20)
    ap.add_argument("--no-samples", action="store_true")
    args = ap.parse_args()
    db = Path(args.db)
    if not db.exists():
        sys.exit(f"Not found: {db}")
    out = Path(args.out) if args.out else db.parent / "_extract" / db.stem
    Extractor(db, out, args.sample_rows).run(not args.no_samples)


if __name__ == "__main__":
    main()

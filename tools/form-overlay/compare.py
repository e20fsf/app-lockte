"""ตรวจรับเอกสารระดับ A (#40): วาง PDF ของระบบใหม่ซ้อนตัวอย่างจากระบบเดิม แล้ววัดว่าข้อความคลาดกันกี่ มม.

    python -I compare.py <legacy.pdf> <new.pdf> <out-dir> [--pages N]

For every text piece of the legacy page (split where the legacy lined things up with 2+ spaces; rows of full stops
left out), finds the same text on the new page nearest to it and reports how far its start, end and baseline moved.
Writes <out-dir>/overlay-<page>.png: the legacy page in red under the new one in blue, for HR to look at.
Both PDFs carry personal data when printed from real data — keep <out-dir> outside git. Needs PyMuPDF.
"""
import re
import sys

import pymupdf

sys.stdout.reconfigure(encoding="utf-8")
MM = 25.4 / 72


def pieces(page):
    """(text without spaces, x0, x1, baseline) in mm of each piece of text on the page."""
    found = []
    for block in page.get_text("rawdict")["blocks"]:
        for line in block.get("lines", []):
            for span in line["spans"]:
                chars = span["chars"]
                text = "".join(c["c"] for c in chars)
                for m in re.finditer(r"[^ ]+(?: [^ ]+)*", text):
                    part = chars[m.start():m.end()]
                    for run in re.split(r"\.{3,}", m.group(0)):
                        key = run.replace(" ", "")
                        if len(key) < 1 or not run.strip():
                            continue
                        start = m.group(0).index(run)
                        cs = part[start:start + len(run)]
                        found.append((key, cs[0]["bbox"][0] * MM, cs[-1]["bbox"][2] * MM, span["origin"][1] * MM))
    return found


def norm(text):
    """Angsana's PDFs extract สระอำ as a separate nikhahit + sara aa (or drop it): compare without them."""
    return text.replace("ำ", "า").replace("ํ", "").replace(" ", "")


def lines(page):
    """Per text line of the page: its characters (spaces left out) with x0, x1, baseline in mm."""
    found = []
    for block in page.get_text("rawdict")["blocks"]:
        for line in block.get("lines", []):
            chars = [(c["c"], c["bbox"][0] * MM, c["bbox"][2] * MM, s["origin"][1] * MM)
                     for s in line["spans"] for c in s["chars"] if norm(c["c"])]
            chars = [(norm(c), *rest) for c, *rest in chars]
            found.append(chars)
    return found


def compare(legacy, new):
    """Each legacy piece found inside a line of the new page on about the same baseline (±5 mm), nearest first."""
    ours = lines(new)
    rows, missing = [], []
    for key, x0, x1, y in pieces(legacy):
        key = norm(key)
        best = None
        for chars in ours:
            text = "".join(c[0] for c in chars)
            for m in re.finditer(re.escape(key), text):
                # map the match back to characters (each norm()ed char may be 0 or 1 long)
                pos, start, end = 0, None, None
                for i, c in enumerate(chars):
                    if pos == m.start() and start is None:
                        start = i
                    pos += len(c[0])
                    if pos >= m.end():
                        end = i
                        break
                a, b = chars[start], chars[end]
                if abs(a[3] - y) > 5:
                    continue
                d = abs(a[1] - x0) + abs(a[3] - y)
                if best is None or d < best[0]:
                    best = (d, a[1] - x0, b[2] - x1, a[3] - y)
        if best is None:
            missing.append(key)
        else:
            rows.append((key, *best[1:]))
    return rows, missing


def overlay(legacy, new, path):
    """Legacy ink red, new ink blue, both purple."""
    a = legacy.get_pixmap(dpi=150, colorspace=pymupdf.csGRAY, alpha=False)
    b = new.get_pixmap(matrix=pymupdf.Matrix(a.width / new.rect.width, a.height / new.rect.height),
                       colorspace=pymupdf.csGRAY, alpha=False)
    colours = {(True, True): b"\x80\x00\xa0", (True, False): b"\xe6\x00\x00", (False, True): b"\x00\x46\xe6", (False, False): b"\xff\xff\xff"}
    sa, sb = a.samples, b.samples
    pixels = b"".join(colours[(sa[i] < 160, sb[i] < 160)] for i in range(min(len(sa), len(sb))))
    pymupdf.Pixmap(pymupdf.csRGB, a.width, a.height, pixels, False).save(path)


def main():
    legacy_path, new_path, out_dir = sys.argv[1:4]
    count = int(sys.argv[sys.argv.index("--pages") + 1]) if "--pages" in sys.argv else 1
    legacy, new = pymupdf.open(legacy_path), pymupdf.open(new_path)
    worst = 0.0
    for p in range(min(count, len(legacy), len(new))):
        rows, missing = compare(legacy[p], new[p])
        print(f"== page {p + 1}: {len(rows)} pieces matched, {len(missing)} not found on the new page")
        for key, dx0, dx1, dy in sorted(rows, key=lambda r: -max(abs(r[1]), abs(r[3]))):
            worst = max(worst, abs(dx0), abs(dy))
            flag = "  !" if max(abs(dx0), abs(dy)) > 2 else ""
            print(f"start {dx0:+5.1f}  end {dx1:+5.1f}  baseline {dy:+5.1f} mm  {key[:50]}{flag}")
        if missing:
            print("not found:", ", ".join(m[:30] for m in missing))
        overlay(legacy[p], new[p], f"{out_dir}/overlay-{p + 1}.png")
    print(f"worst start/baseline offset: {worst:.1f} mm")


if __name__ == "__main__":
    main()

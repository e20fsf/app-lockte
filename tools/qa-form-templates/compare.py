#!/usr/bin/env python3
"""
เทียบใบตรวจที่ระบบพิมพ์กับ PDF ตัวอย่าง (#130): ตำแหน่งตัวอักษรและเส้นคลาดกันกี่ มม.

    python compare.py <sample PDF folder (lot 1)> <folder of PDFs the system printed> [name prefix ...]

The system's PDFs come from the test QaSheetTests.Prints_the_sheets_of_the_recovered_reports_for_comparison (env LOCKTE_QA_COMPARE=<reports_decompiled>|<output>). For every sample
that has a print of the same name it matches the text pieces by their text and the rules by their place, and prints the largest differences in mm. Only numbers are printed.
Needs PyMuPDF. The samples and the prints are the company's own and stay outside the repository.
"""
import os
import re
import sys

import pymupdf

PT = 25.4 / 72


def norm(text):
    return re.sub(r'[\s​]', '', text.replace('ำ', 'า'))


def read(path):
    """The first page: the text as spans (what the PDF says is one run), as chars (so a run the print merges with its neighbour can still be told apart), and the rules."""
    document = pymupdf.open(path)
    if len(document) == 0:
        return [], [], []
    page = document[0]
    spans, chars = [], []
    for block in page.get_text('rawdict')['blocks']:
        for line in block.get('lines', []):
            for span in line['spans']:
                visible = [c for c in span['chars'] if c['c'].strip()]
                if visible:
                    spans.append((''.join(c['c'] for c in visible), visible[0]['origin'][0] * PT, span['origin'][1] * PT, span['bbox'][2] * PT, visible))
                    chars += [(c['c'], c['origin'][0] * PT, span['origin'][1] * PT, c['bbox'][2] * PT) for c in visible]
    rules = []
    for d in page.get_drawings():
        r = d['rect']
        w, h = r.width * PT, r.height * PT
        if d['type'] == 'f' and min(w, h) < 0.6 and max(w, h) > 0.6:
            rules.append((r.x0 * PT, r.y0 * PT, r.x1 * PT, r.y1 * PT))
        elif d['type'] == 's' and (w < 0.05 or h < 0.05) and max(w, h) > 0.6:  # a stroked line: the rectangle it covers
            t = (d.get('width') or 0.4 / PT) * PT
            if h < 0.05:
                rules.append((r.x0 * PT, r.y0 * PT - t / 2, r.x1 * PT, r.y0 * PT + t / 2))
            else:
                rules.append((r.x0 * PT - t / 2, r.y0 * PT, r.x0 * PT + t / 2, r.y1 * PT))
    return spans, chars, rules


def compare(sample, ours):
    s_spans, _, s_rules = read(sample)
    _, o_chars, o_rules = read(ours)
    if not s_spans and not s_rules:
        return None
    in_picture = lambda x, y: x < 81 and 52 < y < 168 and not (30 < x < 60 and 70 < y < 90)
    dx, dy, ends, missing, total = [], [], [], 0, 0
    for text, x, base, x1, visible in s_spans:
        if in_picture(x, base):
            continue
        total += 1
        wanted = [norm(c['c']) for c in visible][:3]
        line = [c for c in o_chars if abs(c[2] - base) < 1.0 and abs(c[1] - x) < 12]  # in the order of the PDF: a mark can stand left of its letter
        letters = [c for c in line if norm(c[0])]
        found = None
        for i in range(len(letters) - len(wanted) + 1):
            if [norm(c[0]) for c in letters[i:i + len(wanted)]] == wanted and (found is None or abs(letters[i][1] - x) < abs(found[1] - x)):
                found = letters[i]
        if found is None:
            missing += 1
            continue
        dx.append(abs(found[1] - x))
        dy.append(abs(found[2] - base))
        if norm(text).endswith('....'):
            dots = [c for c in o_chars if abs(c[2] - base) < 1.0 and c[0] == '.' and x - 1 <= c[1] <= x1 + 1]
            if dots:
                ends.append(abs(max(d[3] for d in dots) - x1))
    free = list(o_rules)
    off_rules = 0
    worst_rule = 0.0
    for x0, y0, x1, y1 in [r for r in s_rules if not (r[2] < 81 and 52 < r[1] < 168)]:
        match = min(free, key=lambda r: abs(r[0] - x0) + abs(r[1] - y0) + abs(r[2] - x1) + abs(r[3] - y1), default=None)
        if match is None:
            off_rules += 1
            continue
        free.remove(match)
        worst = max(abs(match[0] - x0), abs(match[1] - y0), abs(match[2] - x1), abs(match[3] - y1))
        worst_rule = max(worst_rule, worst)
        if worst > 0.5:
            off_rules += 1
    return {
        'dxs': dx, 'dys': dy, 'texts': total, 'text_missing': missing, 'max_dx': max(dx, default=0), 'max_dy': max(dy, default=0), 'max_end': max(ends, default=0),
        'rules': len(s_rules), 'rules_off': off_rules, 'rules_extra': len(free), 'max_rule': worst_rule,
    }


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    samples, ours = sys.argv[1], sys.argv[2]
    prefixes = sys.argv[3:] or ['']
    rows = []
    for name in sorted(os.listdir(ours)):
        if not name.endswith('.pdf') or not any(name.startswith(p) for p in prefixes):
            continue
        sample = os.path.join(samples, name)
        if not os.path.exists(sample):
            continue
        result = compare(sample, os.path.join(ours, name))
        if result is not None:
            rows.append((name, result))
    if not rows:
        print('no sample with a print of the same name')
        return 1
    all_dx = [v for _, r in rows for v in r['dxs']]
    all_dy = [v for _, r in rows for v in r['dys']]
    share = lambda values, limit: 100.0 * sum(1 for v in values if v <= limit) / max(1, len(values))
    print('text pieces found: %d · start within 1 mm: %.1f%% · within 2 mm: %.1f%% · baseline within 0.5 mm: %.1f%%' % (len(all_dx), share(all_dx, 1.0), share(all_dx, 2.0), share(all_dy, 0.5)))
    for key in ('max_dx', 'max_dy', 'max_end', 'max_rule'):
        print('%-9s worst %.2f mm, median %.2f mm over %d sheets' % (key, max(r[key] for _, r in rows), sorted(r[key] for _, r in rows)[len(rows) // 2], len(rows)))
    print('text pieces not found: %d of %d · rules off by more than 0.5 mm: %d of %d · rules the print has extra: %d' % (
        sum(r['text_missing'] for _, r in rows), sum(r['texts'] for _, r in rows), sum(r['rules_off'] for _, r in rows), sum(r['rules'] for _, r in rows), sum(r['rules_extra'] for _, r in rows)))
    for name, r in sorted(rows, key=lambda x: -max(x[1]['max_dx'], x[1]['max_dy']))[:3]:
        print('largest: %s dx %.2f dy %.2f missing %d' % ('#%d' % rows.index((name, r)), r['max_dx'], r['max_dy'], r['text_missing']))
    return 0


if __name__ == '__main__':
    sys.exit(main())

#!/usr/bin/env python3
"""
สร้างโครงพิมพ์ใบตรวจ QA ระดับ A (#130) จากนิยามรายงานที่กู้ไว้ + PDF ตัวอย่าง

    python generate.py <reports_decompiled folder> <sample PDF folder (lot 1)> <output .cs>

For each family of sheets it reads ONE reference report (the recovered definition, for the captions and the kind of each control) and the sample PDF printed from it (for where everything
is): the rules and tick boxes, the labels that are the same on every sheet of the family, the places the model's own text goes (title, document number, measure points, defects, models,
picture) and the data fields — all in millimetres from the paper's top-left corner, as FormPage takes them. Nothing a model changes (the spec values) is written: those come from the QA tables
(#128, #129). A label with runs of spaces or a trail of full stops is split into its pieces so each lands where the legacy put it (TH Sarabun New's spaces are not Angsana New's). To add a
family (#131-#133) add its reference to FAMILIES and run the script again.
Needs PyMuPDF (pip install pymupdf). The definitions and the PDFs are the company's own and stay outside the repository.
"""
import os
import re
import sys

import pymupdf

KINDS = ('Line', 'Rectangle', 'Label', 'TextBox', 'UnboundObjectFrame')
PROPS = ('Left', 'Top', 'Width', 'Height', 'Caption', 'FontSize', 'FontWeight', 'TextAlign', 'ControlSource')
FIXED = ("ค่าที่กำหนด", "(Specification)", "ผลการตรวจสอบ", "ผ่านการตรวจสอบ", "ไม่ผ่านการตรวจสอบ", "P-NCR", "ผู้ตรวจสอบ", "หัวหน้า", "ผู้จัดการ", "(INSPECTOR)", "Variable Data",
         "Attribute Data", "จำนวน", "LOT NO", "รหัสสินค้า", "ชื่อสินค้า", "วันที่", "LOCKTE", "(")

# key of the template in code, the reference report (and its PDF), the group of sheets it serves (the start of their names), a note for the comments.
# A group can have several templates: the one whose rows fit the spec best is used (QaFormTemplates.For), so give a variant its own key and a reference of that shape.
FAMILIES = [
    ('D', 'ไดคาสท์JOG', 'ไดคาสท์', 'die casting (WI-QA055)'),
    ('T', 'ตกแต่ง029', 'ตกแต่ง', 'trimming (WI-QA023)'),
    ('M', 'พิมพ์029', 'พิมพ์', 'new mold (WI-QA022)'),
    ('B', 'อัดผ้าเบรคWAVE', 'อัดผ้าเบรค', 'drum brake after pressing (WI-QA050)'),
    ('G', 'ฝนผ้าเบรคTS100', 'ฝนผ้าเบรค', 'drum brake after grinding (WI-QA024)'),
    ('A', 'ประกอบ052', 'ประกอบ', 'assembled drum brake, short layout (WI-QA025)'),
    ('H', 'อัดดิสเบรคWAVE', 'อัดดิสเบรค', 'disc brake after pressing, defects only (WI-QA061)'),
    ('I', 'อัดดิสเบรคSPARK135', 'อัดดิสเบรค', 'disc brake after pressing, with measure points (WI-QA061)'),
    ('J', 'ดิสเบรคหลังผ่าร่องS4 99 UP RR', 'ดิสเบรคหลังผ่าร่อง', 'disc brake after slotting, defects only (WI-QA027)'),
    ('K', 'ดิสเบรคหลังผ่าร่องNINJA 650', 'ดิสเบรคหลังผ่าร่อง', 'disc brake after slotting, with measure points (WI-QA027)'),
    ('L', 'คลัทช์หลังอัดขึ้นรูปWAVE125', 'คลัทช์หลังอัดขึ้นรูป', 'clutch lining after pressing (WI-QA029)'),
    ('N', 'คลัทช์สำเร็จรูปVR150', 'คลัทช์สำเร็จรูป', 'finished clutch lining (WI-QA030)'),
    ('P', 'แผ่นผ้าคลัทช์C70', 'แผ่นผ้าคลัทช์', 'clutch plate, two sets of ranges (WI-QA034)'),
    # packing (WI-QA026): a strip for each product (the header above, the signatures below); the group is a marker the code matches (QaFormTemplates.For). The blank form has the strips
    # on page 1 and the signatures on page 2 — the signatures' page is the legacy's, kept: four strips fill the page.
    ('S', 'คลัทช์บรรจุกล่อง', '*packing', 'packing, a strip for each product (WI-QA026)', {'strip': (21.0, 166.0, 37.55, 4, False)}),
    ('Z', 'คลัทช์บรรจุกล่องฟอร์มเปล่า', '*packing-blank', 'packing, blank form, four strips (WI-QA026)',
     {'strip': (21.0, 166.0, 37.55, 4, True), 'regions': [(0, -1, 60.1), (1, 172.0, 1e9)], 'max_top': 2154}),
]

TW = 25.4 / 1440
PT = 25.4 / 72


def x_of(left):
    """Twips from the left of the report → mm on the page (fitted on the sample PDFs)."""
    return 10 + left * TW


def y_of(top):
    """Twips from the top of the report → mm on the page (the print is 0.9884 of the report vertically)."""
    return 4.169 + 0.9884 * top * TW


def baseline_of(top, size):
    return y_of(top) + 0.3516 * size


def controls(path):
    text = open(path, 'rb').read().decode('utf-16', errors='replace')
    rows, current, depth, section = [], None, 0, 0
    for raw in text.split('\n'):
        line = raw.strip()
        if line.startswith('Begin ') and '=' not in line:
            if current:
                rows.append(current)
            kind = line[6:].strip()
            if kind in ('Section', 'PageHeader', 'PageFooter'):
                section += 1
            current = {'k': kind, 'sec': section} if kind in KINDS else None
            continue
        if line.endswith('= Begin') or line.endswith('=Begin'):
            depth += 1
            continue
        if line == 'End':
            if depth > 0:
                depth -= 1
            else:
                if current:
                    rows.append(current)
                current = None
            continue
        if current is None or depth > 0:
            continue
        m = re.match(r'^(\w+)\s*=\s*(.*)$', line)
        if m and m.group(1) in PROPS:
            value = m.group(2).strip()
            if m.group(1) == 'Caption' and len(value) >= 2 and value[0] == '"':
                value = value[1:-1].replace('""', '"')
            elif m.group(1) == 'ControlSource' and len(value) >= 2 and value[0] == '"':
                value = value[1:-1]
            current[m.group(1)] = value
    if current:
        rows.append(current)
    for r in rows:
        for k in ('Left', 'Top', 'Width', 'Height', 'FontSize', 'TextAlign', 'FontWeight'):
            try:
                r[k] = int(r.get(k, 0))
            except ValueError:
                r[k] = 0
    return rows


def is_fixed(caption):
    if caption.startswith('จำนวนชิ้นงาน'):  # "how many pieces are packed": a line of the packing check, not a count of the sample
        return False
    return any(caption.startswith(p) for p in FIXED) or caption.startswith('หมายเหตุ')


def cs(text):
    return '"' + text.replace('\\', '\\\\').replace('"', '\\"') + '"'


def num(value):
    return ('%.2f' % value).rstrip('0').rstrip('.') if '.' in ('%.2f' % value) else '%d' % value


def norm(text):
    """The text of a caption or of a PDF span without spaces; the PDF has a sara am as a space and a sara aa, so both are taken as sara aa and the two compare."""
    text = text.replace('ำ', 'า')
    return re.sub(r'[\s​]', '', text)


def no_trail(text):
    """Without the trail of full stops: the legacy cut a label at its box, so the PDF can show fewer (or more) of them than the caption has."""
    return re.sub(r'\.{4,}$', '', text)


class Pdf:
    def __init__(self, path, regions=None):
        """regions: (page, from y, to y) in mm — what of the print is the form (a family's print can spread over pages: the strips on one, the signatures on the next)."""
        self.spans, self.rules, self.used = [], [], set()
        self.empty = not os.path.exists(path)
        if self.empty:
            return  # no sample was printed for this family: everything is placed where the report has it
        document = pymupdf.open(path)
        for page_no, y_from, y_to in regions or [(0, -1, 1e9)]:
            page = document[page_no]
            for block in page.get_text('rawdict')['blocks']:
                for line in block.get('lines', []):
                    for span in line['spans']:
                        chars = [c for c in span['chars'] if c['c'].strip()]
                        text = ''.join(c['c'] for c in chars)
                        base = span['origin'][1] * PT
                        if not chars or text == '#Name?' or not (y_from <= base < y_to):  # '#Name?' is Access's error in a box, not a label (D-3)
                            continue
                        self.spans.append({
                            'norm': text, 'chars': chars, 'size': span['size'], 'bold': 'Bold' in span['font'],
                            'x0': span['origin'][0] * PT, 'base': base, 'x1': span['bbox'][2] * PT,
                        })
            for d in page.get_drawings():
                if d['type'] != 'f':
                    continue
                r = d['rect']
                w, h = r.width * PT, r.height * PT
                if min(w, h) < 0.6 and max(w, h) > 0.6 and y_from <= (r.y0 + r.y1) / 2 * PT < y_to:
                    self.rules.append((r.x0 * PT, r.y0 * PT, r.x1 * PT, r.y1 * PT))

    def shifts(self, labels):
        """How far down the print is from where the report's tops put it, for each section of the report (a section can start below a page header): the median over the labels of the section found on the print by their text."""
        result = {}
        if self.empty:
            return result
        for section in {l['sec'] for l in labels}:
            offsets = []
            for l in (x for x in labels if x['sec'] == section):
                wanted = no_trail(norm(l['Caption']))
                if len(wanted) < 4:
                    continue
                candidates = [s['base'] - baseline_of(l['Top'], l['FontSize'] or 14) for s in self.spans if no_trail(s['norm']) == wanted and abs(s['x0'] - x_of(l['Left'])) < 20]
                if candidates:
                    offsets.append(min(candidates, key=abs))
            if offsets:
                offsets.sort()
                median = offsets[len(offsets) // 2]
                result[section] = median if abs(median) >= 2.0 else 0.0  # smaller than that is the formula's own error, not a page header
        return result

    def find(self, caption, expect_base, expect_x):
        wanted = no_trail(norm(caption))
        best, best_distance = None, 1e9
        for i, s in enumerate(self.spans):
            if i in self.used or no_trail(s['norm']) != wanted:
                continue
            distance = abs(s['base'] - expect_base) + abs(s['x0'] - expect_x) * 0.1
            if distance < best_distance:
                best, best_distance = i, distance
        if best is None:
            return None
        if best_distance > 15 and (best_distance > 80 or sum(1 for s in self.spans if no_trail(s['norm']) == wanted) != 1):
            return None
        self.used.add(best)
        return self.spans[best]


def report_rules(c):
    """The rules and tick boxes of the report itself, in mm, 0.4 mm thick — for a family with no sample PDF."""
    rules = []
    for x in c:
        if x['k'] == 'Line' and (x['Left'] >= 4000 or x['Width'] >= 10000 or (x['Left'] == 0 and x['Height'] >= 5000)):
            if x['Width']:
                rules.append((x_of(x['Left']), y_of(x['Top']) - 0.2, x_of(x['Left'] + x['Width']), y_of(x['Top']) + 0.2))
            elif x['Height']:
                rules.append((x_of(x['Left']) - 0.2, y_of(x['Top']), x_of(x['Left']) + 0.2, y_of(x['Top'] + x['Height'])))
        elif x['k'] == 'Rectangle' and 0 < x['Width'] < 1000 and x['Top'] > 5000:
            x0, y0, x1, y1 = x_of(x['Left']), y_of(x['Top']), x_of(x['Left'] + x['Width']), y_of(x['Top'] + x['Height'])
            rules += [(x0, y0, x1, y0 + 0.4), (x0, y1 - 0.4, x1, y1), (x0, y0, x0 + 0.4, y1), (x1 - 0.4, y0, x1, y1)]
    return rules


def pieces_of(caption):
    """The caption in pieces: text, runs of spaces between them, and a trail of full stops as its own piece (the leader)."""
    result, rest = [], caption.rstrip()
    leader = re.search(r'\.{4,}$', rest)
    tail = ''
    if leader:
        tail = leader.group(0)
        rest = rest[:leader.start()]
    for part in re.split(r'(?<=\S)\s{2,}(?=\S)', rest.strip()):
        if part:
            result.append(part)
    return result, tail


def char_index(caption_norm_prefix):
    return len(caption_norm_prefix)


def static_items(label, pdf, shift=0.0):
    """The label as pieces with their places in mm: from the sample PDF when it is found there, from the report's own place otherwise."""
    size = label['FontSize'] or 14
    bold = label['FontWeight'] >= 700
    anchor = {2: 1, 3: 2}.get(label['TextAlign'], 0)
    caption = label['Caption']
    expect_base = baseline_of(label['Top'], size) + shift
    expect_x = x_of(label['Left'])
    span = pdf.find(caption, expect_base, expect_x)
    parts, tail = pieces_of(caption)
    items = []
    if span is None:
        # not on the sample (a hidden label): where the report puts it
        base = expect_base
        if anchor == 1:
            x = x_of(label['Left'] + label['Width'] / 2)
        elif anchor == 2:
            x = x_of(label['Left'] + label['Width']) - 0.3
        else:
            x = expect_x + 0.26
        items.append((x, base, size, bold, anchor, caption.strip(), None))
        return items
    chars = span['chars']
    anchored = lambda: span['x0'] if anchor == 0 else (span['x0'] + span['x1']) / 2 if anchor == 1 else span['x1']
    # the legacy cut a label at its box: the trail of full stops in the PDF can be shorter than the caption's, so the words are compared without it
    pdf_dots = 0
    while pdf_dots < len(chars) and chars[len(chars) - 1 - pdf_dots]['c'] == '.':
        pdf_dots += 1
    words = norm(re.sub(r'\.{4,}\s*$', '', caption.rstrip()))
    if len(chars) - pdf_dots != len(words) and not (len(chars) == len(norm(caption))):
        # the PDF and the caption do not line up char for char: the whole caption as one piece, placed as the PDF has it
        return [(anchored(), span['base'], size, bold, anchor, caption.strip(), None)]
    if anchor != 0 and len(parts) <= 1 and not tail:
        return [(anchored(), span['base'], size, bold, anchor, caption.strip(), None)]
    # char for char: each piece starts at the origin of its first char, a trail of full stops is a leader to the end of the span
    position = 0
    for part in parts:
        items.append((chars[position]['origin'][0] * PT, span['base'], size, bold, 0, part, None))
        position += len(norm(part))
    if tail and position < len(chars):
        items.append((chars[position]['origin'][0] * PT, span['base'], size, bold, 0, '', span['x1']))
    return items


def static_literal(item):
    x, base, size, bold, anchor, text, leader = item
    return 'new(%s, %s, %s, %s, %d, %s, %s)' % (num(x), num(base), num(size), 'true' if bold else 'false', anchor, cs(text), num(leader) if leader is not None else 'null')


def slot_literal(x, base, size, bold, anchor):
    return 'new(%s, %s, %s, %s, %d)' % (num(x), num(base), num(size), 'true' if bold else 'false', anchor)


def family(key, name, note, definitions, samples, options):
    c = controls(os.path.join(definitions, name + '.txt'))
    if 'max_top' in options:  # a blank form repeats the strip in its report: the first one is the template
        c = [x for x in c if x['Top'] < options['max_top']]
    pdf = Pdf(os.path.join(samples, name + '.pdf'), options.get('regions'))
    labels = sorted([x for x in c if x['k'] == 'Label' and x.get('Caption', '').strip()], key=lambda x: (x['sec'], x['Top'], x['Left']))
    number = next(l for l in labels if re.match(r'^WI-QA\d+', l['Caption'].strip()))
    title = next(l for l in labels if l is not number and abs(l['Top'] - number['Top']) <= 60 and not is_fixed(l['Caption'].strip()))
    spec_header = next(l for l in labels if l['Caption'].strip() == 'ค่าที่กำหนด')
    first_spec = next(l for l in labels if l['Caption'].strip() == '(Specification)')
    stop = min([l['Top'] for l in labels if abs(l['Left'] - spec_header['Left']) <= 250 and l['Caption'].startswith('หมายเหตุ')] or [10 ** 9])
    column = [l for l in labels if l['sec'] == first_spec['sec'] and l['Top'] > first_spec['Top'] and abs(l['Left'] - spec_header['Left']) <= 250
              and not is_fixed(l['Caption'].strip()) and not re.match(r'^\d+\s*-\s*\d+$', l['Caption'].strip()) and l['Top'] < stop]
    points, defects, in_defects = [], [], False
    for l in column:
        if not in_defects and not re.search(r'\d', l['Caption']):
            in_defects = True
        (defects if in_defects else points).append(l)
    lot = next(l for l in labels if l['Caption'].startswith('LOT NO'))
    signature = min([l['Top'] for l in labels if l['Caption'].startswith('ผลการตรวจสอบ') and l['Left'] < spec_header['Left'] - 200] or [10 ** 9])
    models = [l for l in labels if lot['Top'] < l['Top'] < signature and l['Left'] < spec_header['Left'] - 200 and not is_fixed(l['Caption'])
              and not re.match(r'^P\.?$', l['Caption']) and '....' not in l['Caption']]
    overlays = [l for l in labels if re.match(r'^[+\-±]\s*\d+([.,]\d+)?$', l['Caption'].strip())]
    markers = [l for l in labels if re.match(r'^P\.?$', l['Caption'].strip())]
    dynamic = {id(x) for x in [number, title] + points + defects + models + overlays + markers}
    static = [l for l in labels if id(l) not in dynamic]

    shifts = pdf.shifts(labels)
    statics = []
    for l in static:
        statics += static_items(l, pdf, shifts.get(l['sec'], 0.0))

    def slot(l, anchor_override=None):
        size = l['FontSize'] or 14
        base_expect = baseline_of(l['Top'], size) + shifts.get(l['sec'], 0.0)
        span = pdf.find(l['Caption'], base_expect, x_of(l['Left']))
        anchor = {2: 1, 3: 2}.get(l['TextAlign'], 0) if anchor_override is None else anchor_override
        if span is not None:
            x = span['x0'] if anchor == 0 else (span['x0'] + span['x1']) / 2 if anchor == 1 else span['x1']
            return slot_literal(x, span['base'], size, l['FontWeight'] >= 700, anchor)
        x = x_of(l['Left']) + 0.26 if anchor == 0 else x_of(l['Left'] + l['Width'] / 2) if anchor == 1 else x_of(l['Left'] + l['Width']) - 0.3
        return slot_literal(x, base_expect, size, l['FontWeight'] >= 700, anchor)

    if pdf.empty:
        pdf.rules = report_rules(c)
    rules = ['new(%s, %s, %s, %s)' % tuple(num(v) for v in r) for r in sorted(pdf.rules, key=lambda r: (round(r[1], 1), round(r[0], 1)))]
    picture = next((x for x in c if x['k'] == 'UnboundObjectFrame' and x['Width'] >= 1000 and x['Top'] > 0), None)

    fields = []
    for x in sorted([x for x in c if x['k'] == 'TextBox'], key=lambda x: (x['Top'], x['Left'])):
        source = x.get('ControlSource', '')
        near = ''
        if source == 'DATE':
            row = [l for l in labels if abs(l['Top'] - x['Top']) <= 80 and l['Left'] < x['Left'] + 50 and l['Caption'].startswith(('วันที่', 'LOT'))]
            near = max(row, key=lambda l: l['Left'])['Caption'][:12] if row else ''
        kind = {'PD_C': 'ProductCode', 'PD_N': 'ProductName', 'LOT': 'Lot', 'QTY': 'Quantity', 'DATE': 'Date', 'Expr1': 'SampleSize'}.get(source)
        threshold = re.search(r'\[EXPR1\]>=(\d+)', source, re.I)
        if threshold:
            kind, near = 'Mark', threshold.group(1)
        if kind is None:
            continue
        size = x['FontSize'] or 14
        anchor = {2: 1, 3: 2}.get(x['TextAlign'], 0)
        px = x_of(x['Left']) + 0.26 if anchor == 0 else x_of(x['Left'] + x['Width'] / 2)
        fields.append('new(QaFieldKind.%s, %s, %s, %s, %d, %s)' % (kind, num(px), num(baseline_of(x['Top'], size) + shifts.get(x['sec'], 0.0)), num(size), anchor, cs(near)))

    out = ['    // %s: %s — the reference report has %d controls' % (key, note, len(c)),
           '    private static QaFormTemplate %sTemplate() => new(' % key,
           '        "%s",' % key,
           '        Rules: [' + ', '.join(rules) + '],',
           '        Texts:',
           '        [']
    out += ['            ' + static_literal(item) + ',' for item in statics]
    out += ['        ],',
            '        Title: %s,' % slot(title),
            '        DocumentNumber: %s,' % slot(number),
            '        Points: [' + ', '.join(slot(l) for l in points) + '],',
            '        Defects: [' + ', '.join(slot(l) for l in defects) + '],',
            '        Models: ' + (slot(models[0], 1) if models else 'null') + ',',
            '        Strip: ' + ('new(%s, %s, %s, %d, %s)' % (num(options['strip'][0]), num(options['strip'][1]), num(options['strip'][2]), options['strip'][3], 'true' if options['strip'][4] else 'false') if 'strip' in options else 'null') + ',',
            '        Picture: ' + ('new(%s, %s, %s, %s)' % (num(x_of(picture['Left'])), num(y_of(picture['Top']) + shifts.get(picture['sec'], 0.0)), num(picture['Width'] * TW), num(picture['Height'] * TW * 0.9884)) if picture else 'null') + ',',
            '        Fields:',
            '        [']
    out += ['            ' + f + ',' for f in fields]
    out += ['        ]);']
    return '\n'.join(out), len(statics), len(points), len(defects), len(fields), len(rules)


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        return 2
    definitions, samples, target = sys.argv[1], sys.argv[2], sys.argv[3]
    parts = ['// <auto-generated>',
             '// Generated by tools/qa-form-templates/generate.py from the recovered report definitions and the sample PDFs. Do not edit by hand: run the script again.',
             '// </auto-generated>', 'namespace Lockte.Web.Reports.Order.Qa;', '', 'public static partial class QaFormTemplates', '{']
    for key, name, group, note, *rest in FAMILIES:
        body, statics, points, defects, fields, rules = family(key, name, note, definitions, samples, rest[0] if rest else {})
        print('%s: %d text pieces, %d point rows, %d defect rows, %d fields, %d rules' % (key, statics, points, defects, fields, rules))
        parts.append(body)
        parts.append('')
    parts.append('    private static IReadOnlyList<QaFormTemplate> Build() => [' + ', '.join('%sTemplate()' % k for k, *_ in FAMILIES) + '];')
    parts.append('')
    parts.append('    /// <summary>The group of sheets (the start of the name of their spec) each template serves.</summary>')
    parts.append('    private static readonly (string Group, string Key)[] GroupKeys = [' + ', '.join('(%s, "%s")' % (cs(g), k) for k, _, g, *_ in FAMILIES) + '];')
    parts.append('}')
    with open(target, 'w', encoding='utf-8-sig', newline='\r\n') as f:
        f.write('\n'.join(parts) + '\n')
    return 0


if __name__ == '__main__':
    sys.exit(main())

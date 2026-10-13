# ไลบรารีรายงาน PDF ภาษาไทยและไฟล์ Excel ส่งธนาคาร

ตั๋ว: [#12](https://github.com/e20fsf/app-lockte/issues/12) (แผนที่ [#8](https://github.com/e20fsf/app-lockte/issues/8)) · ค้นคว้า 2026-10-13 · **ไม่ได้เลือก stack** มีแต่ข้อเท็จจริงพร้อมแหล่งอ้างอิงสำหรับตั๋วเลือก stack

## คำถาม

1. ไลบรารี PDF/รายงานตัวไหนแสดงภาษาไทยถูก ทั้ง **การจัดรูปอักษร (shaping)** คือสระบน/ล่างและวรรณยุกต์ซ้อนกัน และ **การตัดบรรทัด** (ภาษาไทยไม่เว้นวรรคระหว่างคำ จึงต้องตัดคำด้วยพจนานุกรม)
2. ไลบรารีไหนวางข้อความที่ตำแหน่งตายตัว (ซม./มม.) ได้ เพื่อให้ตรงแบบฟอร์มราชการ: แบบ คร.2 (RPT-57, INT-05) และทะเบียนผู้ประกันตน (RPT-51, INT-04)
3. ไลบรารีไหนเขียนไฟล์ Excel สำหรับขั้นตอนส่งธนาคารได้: ttb `TMB SAP_EPORTAL V3.1_NOWHT2` (INT-01) และ K-Bank `CREATE TEXT VERSION 4.0_IOS FOR PAYROLL` (INT-02), ดู `specs/employee/integrations.md`

## พื้นฐานที่ต้องรู้ก่อนอ่านตาราง

ไลบรารีต้องผ่านสองเรื่องแยกกัน:

- **Shaping**: ต้องใช้ shaping engine ที่มีตัวจัดรูปเฉพาะภาษาไทย เช่น HarfBuzz ถ้าแค่ใช้ GPOS ทั่วไปจะพังในบางกรณี ตัวอย่างชัดที่สุดคือ **สระอำ + วรรณยุกต์** (เช่น `น้ำ`) HarfBuzz แตกสระอำแล้วเรียงเครื่องหมายใหม่ก่อนใส่ feature แต่ fontkit (ที่ใช้ใน PDFKit/pdfmake) ไม่มีขั้นตอนนี้ ([fontkit #134](https://github.com/foliojs/fontkit/issues/134) ซึ่งยังเปิดอยู่ ผู้ร่วมพัฒนาบอกว่า "There does not seem to be a Thai shaper in fontkit at all")
- **การตัดบรรทัด**: HarfBuzz ไม่ตัดบรรทัดให้ ตาม UAX #14 อักษรไทยเป็นคลาส `SA` ซึ่งต้องใช้พจนานุกรม ไลบรารีที่ใช้แค่กฎ UAX #14 จะถือ SA เป็นตัวอักษรธรรมดา (`AL`) ผลคือ**ข้อความไทยตัดบรรทัดได้เฉพาะตรงที่มีเว้นวรรค** ICU ตัดคำไทยด้วยพจนานุกรมได้ ("ICU provides dictionary support for word boundaries in Chinese, Japanese, Thai, Lao, Khmer and Burmese" จาก [ICU Boundary Analysis](https://unicode-org.github.io/icu/userguide/boundaryanalysis/)) libthai ก็ทำได้เช่นกัน

ทางแก้ที่ใช้ได้กับไลบรารีทุกตัวที่ยังไม่ตัดคำไทย: ตัดคำล่วงหน้าแล้วแทรก ZERO WIDTH SPACE (U+200B) ซึ่ง UAX #14 ถือว่าตัดบรรทัดได้ (กฎ LB8) วิธีนี้ต้องใช้ตัวตัดคำแยก เช่น ICU หรือ PyThaiNLP

**ความเสี่ยงจริงของงานนี้**: ช่องในแบบ คร.2 และทะเบียนผู้ประกันตนส่วนใหญ่เป็นข้อความสั้นบรรทัดเดียว (ชื่อ, เลขบัตร, วันที่, ค่าจ้าง) จึงขึ้นกับ **shaping** มากกว่าการตัดบรรทัด ช่องที่อาจต้องขึ้นบรรทัดใหม่คือ "ที่อยู่ตามทะเบียน" (PER_ADD) ใน คร.2

## ตาราง: PDF / รายงาน

สัญลักษณ์: ✅ มีหลักฐานจากแหล่งต้นทาง · ⚠ ใช้ได้แต่มีเงื่อนไขหรือต้องหาทางเลี่ยง · ❌ มีหลักฐานว่าพัง · ❓ ยังไม่มีหลักฐานจากต้นทาง ต้องทดสอบจริง

| Stack | ไลบรารี | Shaping ไทย | ตัดบรรทัดไทย | วางตำแหน่งตายตัว | License | หลักฐาน |
|---|---|---|---|---|---|---|
| ทุก stack | **Headless Chromium** (Playwright / Puppeteer, HTML→PDF) | ✅ HarfBuzz (Blink) | ✅ ICU พร้อมพจนานุกรม (filter ICU ของ Chromium รวม `brkitr_dictionaries` ทั้งหมด) | ✅ CSS `@page { size }` + `position:absolute` หน่วย mm/cm; `preferCSSPageSize` | Apache-2.0 (Playwright/Puppeteer), Chromium BSD | [Chromium ICU filter common.json](https://chromium.googlesource.com/chromium/deps/icu/+/refs/heads/main/filters/common.json) · [Playwright page.pdf](https://playwright.dev/docs/api/class-page#page-pdf) · [Puppeteer PDFOptions](https://pptr.dev/api/puppeteer.pdfoptions) |
| .NET | **QuestPDF** (2024.3+) | ✅ HarfBuzz ผ่าน Skia SkParagraph (`skia_use_harfbuzz=true`) | ⚠ ใช้ **libgrapheme** แทน ICU (`skia_use_icu=false`); libgrapheme แปลง SA→AL จึงตัดเฉพาะที่เว้นวรรคหรือ ZWSP | ✅ `Layers`, `Unconstrained`, `TranslateX/Y`, `Unit.Centimetre` | Community ฟรีเมื่อรายได้ < USD 1M/ปี, ถ้าเกินต้องซื้อ Professional/Enterprise | [QuestPDF.Native.Skia.Build workflow](https://github.com/QuestPDF/QuestPDF.Native.Skia.Build/blob/main/.github/workflows/main.yaml) · [unicode.cpp](https://github.com/QuestPDF/QuestPDF.Native.Skia.Build/blob/main/native/src/text/unicode.cpp) · [release 2024.3.0-beta1 "switching from ICU to libgrapheme"](https://github.com/QuestPDF/QuestPDF/releases/tag/2024.3.0-beta1) · [libgrapheme gen/line.c (SA→AL)](https://git.suckless.org/libgrapheme/file/gen/line.c.html) · [Skia SkUnicode_libgrapheme.cpp](https://github.com/google/skia/blob/main/modules/skunicode/src/SkUnicode_libgrapheme.cpp) · [Unconstrained](https://www.questpdf.com/api-reference/unconstrained.html) · [License guide](https://www.questpdf.com/license/guide.html) · [#154 การตัดบรรทัดภาษาที่ไม่เว้นวรรค](https://github.com/QuestPDF/QuestPDF/issues/154) |
| .NET / Java | **iText Core** (+ pdfCalligraph) | ⚠ ต้องใช้ add-on **pdfCalligraph** ซึ่งระบุว่ารองรับ Thai; iText เองบอกว่า PDF creator ทั่วไปมีปัญหากับ "Arabic, Hebrew, Hindi or Thai" | ❓ | ✅ (canvas พิกัด pt) | Core เป็น AGPLv3 หรือ commercial; pdfCalligraph ไม่มี repo สาธารณะ, NuGet ชี้ไปหน้า legal ของ iText และต้องใช้ `itext.licensing.base` (ถือว่าต้องซื้อ commercial) | [pdfCalligraph product](https://itextpdf.com/products/pdfcalligraph) · [NuGet itext.pdfcalligraph](https://www.nuget.org/packages/itext.pdfcalligraph) · [iText AGPL](https://itextpdf.com/AGPL) |
| .NET | **PDFsharp / MigraDoc** | ❌ ไม่มี shaping engine; ทีมยืนยันว่าไม่รองรับ RTL/การจัดรูปตามอักษรข้างเคียง | ❌ | ✅ XGraphics พิกัดตรง | MIT | [MigraDoc known issues (RTL/CJK ไม่รองรับ)](https://www.pdfsharp.net/wiki/MigraDoc_Issues.ashx?Code=1) · [forum: "the shape of a character depends on its neighbours. This is not yet implemented"](https://forum.pdfsharp.net/viewtopic.php?p=1456) · [forum 2024 RTL](https://forum.pdfsharp.net/viewtopic.php?p=15047) |
| .NET | **DevExpress XtraReports** | ❓ บน Windows ใช้ GDI+ (มีตัวจัดรูปไทยของ Windows) แต่ยังไม่พบเอกสารยืนยันเรื่อง shaping ใน PDF export | ⚠ มีตัวเลือก `PrintingSettings.UseGdiPlusLineBreakAlgorithm` (v19.2.4+) ซึ่ง "mostly affects the creation of PDF files with CJK (or Thai) fonts" เพื่อให้ preview กับ PDF ตัดบรรทัดตรงกัน | ✅ designer แบบ band/absolute | Commercial (ต่อ developer) | [DevExpress Reporting Tips Oct 2019](https://community.devexpress.com/blogs/reporting/archive/2019/11/11/reporting-tips-amp-tricks-october-2019.aspx) |
| .NET | **Stimulsoft Reports** | ⚠/❌ มีรายงานปี 2025 ว่าวรรณยุกต์ไทยวางผิดตำแหน่ง (.NET: ผิดใน preview; Java: วรรณยุกต์ชนตัวอักษรใน PDF export) ยังไม่มีการแก้ใน thread | ❓ | ✅ | Commercial | [Reports.NET "Problem with Thai Tone Mark Rendering" (2025-09-08)](https://forum.stimulsoft.com/viewtopic.php?p=174636) · [Reports.JAVA Thai PDF export (2025-09-29)](https://forum.stimulsoft.com/viewtopic.php?p=174771) |
| .NET | **FastReport** | ❓ ไม่พบหลักฐานเรื่อง Thai shaping ใน .NET; มีรายงานว่า FastReport **VCL** export PDF แล้ว crash ตอนเจออักษรไทยตัวแรก | ❓ | ✅ | Commercial / Open Source edition (MIT) | [forum: Exception creating PDF with Thai](https://forum.fast-report.com/en/discussion/comment/43438) |
| Node | **pdfmake** (ใช้ PDFKit → fontkit) | ❌ วาด `น้ำ` ผิด; maintainer ปิด issue โดยโยนไป fontkit (label `3rd-party`) | ❌ (เหมือน PDFKit) | ✅ `absolutePosition` | MIT | [pdfmake #2677 "Thai letter not rendering correctly" (น้ำ)](https://github.com/bpampuch/pdfmake/issues/2677) · [fontkit #134](https://github.com/foliojs/fontkit/issues/134) |
| Node | **PDFKit** (fontkit + linebreak) | ❌ fontkit มี shaper แค่ Arabic, Hangul, Indic, Universal, Default (ไม่มี Thai); ปัญหา mark-to-mark ยังไม่ปิด | ❌ ไลบรารี `linebreak` แปลง `SA` → `AL` จึงไม่ตัดกลางข้อความไทยที่ไม่เว้นวรรค | ✅ `doc.text(x, y)` | MIT | [fontkit shapers dir](https://github.com/foliojs/fontkit/tree/master/src/opentype/shapers) · [fontkit #134](https://github.com/foliojs/fontkit/issues/134) · [linebreak linebreaker.js](https://github.com/foliojs/linebreak/blob/master/src/linebreaker.js) · [pdfkit package.json](https://github.com/foliojs/pdfkit/blob/master/package.json) |
| Node | **pdf-lib** | ❌ ไม่มี shaping; `drawText()` แทรกช่องว่างกับฟอนต์ไทย (issue ยังเปิด) | ❌ | ✅ | MIT | [pdf-lib #1010](https://github.com/Hopding/pdf-lib/issues/1010) · [pdf-lib #675](https://github.com/Hopding/pdf-lib/issues/675) |
| Python | **WeasyPrint** (HTML/CSS→PDF ด้วย Pango) | ✅ Pango ใช้ HarfBuzz | ✅ เมื่อ Pango ลิงก์กับ **libthai** (option ใน meson ของ Pango); แพ็กเกจ MSYS2 `mingw-w64-pango` ที่เอกสาร WeasyPrint ให้ใช้บน Windows ระบุ `libthai` เป็น dependency | ✅ CSS `@page`, `position:absolute` | BSD-3-Clause | [Pango meson.build (libthai, th_brk_find_breaks)](https://github.com/GNOME/pango/blob/main/meson.build) · [MSYS2 pango PKGBUILD](https://github.com/msys2/MINGW-packages/blob/master/mingw-w64-pango/PKGBUILD) · [WeasyPrint first steps, Windows](https://doc.courtbouillon.org/weasyprint/stable/first_steps.html) |
| Python | **ReportLab** | ❓ บน PyPI มี extra `shaping` (ต้องใช้ `uharfbuzz`) แต่ยังไม่พบเอกสารทางการที่บอกขอบเขตการใช้ | ❓ คู่มือยังเขียนไว้ใน To Do ว่า "Improve the word wrapping code in paragraphs" | ✅ canvas พิกัดตรง (cm/mm) | BSD | [PyPI reportlab metadata (`uharfbuzz; extra == "shaping"`)](https://pypi.org/pypi/reportlab/json) · [User guide ch3 fonts](https://docs.reportlab.com/reportlab/userguide/ch3_fonts/) |
| Python | **fpdf2** | ✅ ตั้งแต่ 2.7.5 ผ่าน uharfbuzz (`set_text_shaping(True)`); ปิด issue วรรณยุกต์ไทยแล้ว | ⚠ ตัดที่ ZWSP ได้หลังแก้ #1190 แต่ยังไม่มี UAX #14 หรือพจนานุกรม ต้องแทรก ZWSP เอง | ✅ `set_xy`, หน่วย mm | LGPL-3.0 | [fpdf2 #679 Shaping Thai (ปิดใน 2.7.5)](https://github.com/py-pdf/fpdf2/issues/679) · [#459 วรรณยุกต์ซ้อน](https://github.com/py-pdf/fpdf2/issues/459) · [#1190 Thai ZWSP wrap](https://github.com/py-pdf/fpdf2/issues/1190) · [Text shaping docs](https://py-pdf.github.io/fpdf2/TextShaping.html) |

### หมายเหตุเรื่อง Windows Server

- Playwright (Node) รองรับ "Windows 11+, Windows Server 2019+" ([Playwright intro, System requirements](https://playwright.dev/docs/intro#system-requirements)) และมี binding .NET/Python/Java ด้วย จึงใช้ได้ไม่ว่าจะเลือก stack ไหน
- WeasyPrint บน Windows ต้องติดตั้ง Pango ผ่าน MSYS2 หรือใช้ไฟล์ exe สำเร็จรูป เอกสารเตือนว่า exe "is regularly marked as malware by different antivirus companies" (false positive) ([first steps](https://doc.courtbouillon.org/weasyprint/stable/first_steps.html))
- QuestPDF ตั้งแต่ 2024.3 ไม่พึ่ง SkiaSharp แล้ว เพราะมี native Skia ของตัวเองรวมอยู่ใน NuGet ([release 2024.3.0](https://github.com/QuestPDF/QuestPDF/releases/tag/2024.3.0)) แต่การตัดคำไทยต้องหาเพิ่มเอง ฝั่ง .NET มี ICU4N แต่ล่าสุดยังเป็น `60.1.0-alpha` ([NuGet ICU4N](https://www.nuget.org/packages/ICU4N))

## ไลบรารีที่ควรตัดออกเพราะแสดงภาษาไทยพัง

- **PDFsharp / MigraDoc** (.NET): ไม่มี shaping engine และทีมไม่มีแผนจะทำ
- **pdf-lib** (Node): ไม่มี shaping และแทรกช่องว่างผิดเมื่อใช้ฟอนต์ไทย
- **pdfmake / PDFKit** (Node): fontkit ไม่มีตัวจัดรูปไทย ทำให้สระอำ + วรรณยุกต์ (`น้ำ`) วาดผิด และ issue ยังเปิดตั้งแต่ปี 2018 ข้อนี้กระทบชื่อคนไทยโดยตรง (เช่น ชื่อที่มี "น้ำ", "ค้ำ", "ซ้ำ") จึงไม่ปลอดภัยสำหรับแบบฟอร์มราชการ

ตัวที่**ยังไม่ตัดออกแต่เสี่ยง** ต้องทดสอบก่อนใช้: Stimulsoft (มีรายงานวรรณยุกต์ผิดตำแหน่งปี 2025), FastReport และ ReportLab (ไม่มีหลักฐานทั้งด้านดีและด้านเสีย), iText (ใช้ไทยได้เฉพาะเมื่อซื้อ pdfCalligraph)

## ตาราง: Excel (ไฟล์ส่งธนาคาร)

ข้อมูลระบบเดิม (INT-01/02): ระบบเดิม**ไม่ได้เติมค่าลง template ธนาคาร** แต่ export รายงานเป็น Excel ธรรมดา แล้วผู้ใช้คัดลอกแถวไปวางที่ A4 ของ template ธนาคาร (ไฟล์มี VBA และปุ่ม "CREATE TEXT FILE" / "EXPORT TEXT FILE") พร้อมกรอกวันที่จ่ายที่ D2 (ttb) หรือ B2 (K-Bank) ตัว template อยู่นอกโปรแกรม (❓ Q-03) ทางเลือกของระบบใหม่จึงมีสามแบบ:

- **(ก)** สร้าง .xlsx ธรรมดาที่มีคอลัมน์เหมือนเดิม แล้วให้ผู้ใช้วางเอง ไลบรารีไหนก็ทำได้
- **(ข)** เติมข้อมูลลง template .xlsm ของธนาคารโดยตรง ไลบรารีต้องเก็บ VBA และปุ่มไว้ได้
- **(ค)** สร้าง text file ให้ธนาคารโดยตรง ต้องรู้ format จาก VBA ใน template ก่อน และขัดกับข้อตกลง "ทำงานเหมือนระบบเดิม"

ภาษาไทยในเซลล์ Excel เป็น Unicode string ธรรมดา ทุกไลบรารีเขียนได้ จุดที่ต้องระวังคือ**เลขบัญชีต้องเป็น text** (Text 10, อาจขึ้นต้นด้วย 0)

| Stack | ไลบรารี | เขียน .xlsx ใหม่ | เปิด template แล้วเติมค่า | เก็บ VBA (.xlsm) | License | หลักฐาน |
|---|---|---|---|---|---|---|
| .NET | **ClosedXML** | ✅ | ✅ | ⚠ macro ยังใช้งานได้หลัง save แต่ชื่อปุ่มถูกเปลี่ยนเป็นชื่อทั่วไป (issue เปิดอยู่) | MIT | [ClosedXML #2212](https://github.com/ClosedXML/ClosedXML/issues/2212) |
| .NET | **EPPlus** (5+) | ✅ | ✅ | ✅ "EPPlus supports Creating, Reading and Writing VBA" (save เป็น .xlsm) | Polyform Noncommercial; **ใช้เชิงพาณิชย์ต้องซื้อ license** ต่อ developer (subscription ประมาณ $569/ปี สำหรับ 1–4 คน) | [EPPlus wiki VBA](https://github.com/EPPlusSoftware/EPPlus/wiki/VBA) · [License overview](https://epplussoftware.com/en/LicenseOverview) |
| Node | **exceljs** | ✅ | ✅ | ❌ macro หายหลัง save (#884); feature request #172 เปิดค้าง | MIT; release ล่าสุด v4.4.0 (2023-10-19) | [exceljs #884](https://github.com/exceljs/exceljs/issues/884) · [#172](https://github.com/exceljs/exceljs/issues/172) · [releases](https://github.com/exceljs/exceljs/releases) |
| Python | **openpyxl** | ✅ | ⚠ "shapes will be lost from existing files" (ปุ่มใน template อาจหาย) | ⚠ `keep_vba=True` เก็บ VBA ไว้ "still not editable" | MIT | [openpyxl tutorial](https://openpyxl.readthedocs.io/en/stable/tutorial.html) |

ถ้าเลือกทาง (ก) แบบเดียวกับระบบเดิม ทุกตัวในตารางใช้ได้ ข้อจำกัดเรื่อง VBA มีผลเฉพาะทาง (ข)

## สิ่งที่ยังไม่รู้ (ต้อง prototype ไม่ใช่ค้นเอกสาร)

- ฟอนต์ที่ใช้ในแบบฟอร์มเดิม (เช่น TH SarabunPSK / Angsana New) มี GSUB/GPOS ภาษาไทยครบไหม ข้อนี้มีผลกับทุกไลบรารี
- DevExpress, FastReport, ReportLab: ต้องลองพิมพ์ชุดทดสอบ `น้ำ ผู้ใหญ่ ปั๊ม กิ่ง ญี่ปุ่น ฤๅษี` และที่อยู่ยาว ๆ แล้วตรวจ PDF ด้วยตา
- เลย์เอาต์และตำแหน่ง (ซม.) ของ RPT-51 / RPT-57 อยู่ใน `source/_extract/พนักงาน/legacy/reports/` (ไม่อยู่ใน git) ต้องใช้ตอนสร้าง template
- VBA ใน template ธนาคาร (Q-03) ต้องรู้ถ้าจะเลือกทาง (ข) หรือ (ค)

# Reverse Engineering MS Access → Spec

## ทำไมต้องดึงข้อมูลด้วยสคริปต์ก่อน

ใน Access business logic กระจายอยู่หลายที่: ตาราง (validation rule, default), query (SQL ที่คำนวณ/กรอง), ฟอร์ม (event + VBA หลังฟอร์ม), รายงาน, module และ macro การเปิดดูด้วยมือพลาดง่าย สคริปต์ดึงทุกอย่างออกมาเป็นไฟล์ข้อความ ทำให้ค้นหา (grep) อ้างอิง และตรวจว่าครอบคลุมครบได้

## ขั้นที่ 1 — Extract

```powershell
py .claude/skills/reengineering-access/scripts/extract_access.py "source/<file>.accdb"
```

ตัวเลือก: `--out <dir>` (ค่าเริ่มต้น `source/_extract/<ชื่อไฟล์>/`), `--sample-rows N` (ค่าเริ่มต้น 20), `--no-samples`

ต้องการ: Windows + MS Access ติดตั้งอยู่ + Python ที่มี `pywin32` (ถ้า `py` ไม่มี ลอง `python` หรือหาใน `%LOCALAPPDATA%\Programs\Python\`; ถ้าขาด pywin32 → `py -m pip install pywin32`) สคริปต์เปิดไฟล์แบบปิด VBA/macro (AutomationSecurity = ForceDisable) จึงไม่รัน AutoExec และไม่แก้ไขไฟล์ต้นฉบับ — ถ้าไฟล์ถูกเปิดค้างใน Access อยู่ ให้ปิดก่อน

ถ้าไฟล์เป็น .zip ให้แตกไว้ใน `source/` ก่อน ไฟล์ใหญ่ (หลายร้อย MB) ใช้เวลาหลายนาที ให้รันแบบ background

ผลลัพธ์:
```
source/_extract/<name>/
├── INVENTORY.md        รายการวัตถุทั้งหมด + จำนวน + StartupForm — ใช้เป็น checklist ความครอบคลุม
├── tables.md           ตาราง ฟิลด์ ชนิด ขนาด required default validation description index row count
├── relationships.md    ความสัมพันธ์ + referential integrity + cascade
├── queries/*.sql       SQL ของทุก query (หัวไฟล์บอกชนิด: SELECT/UPDATE/APPEND/...)
├── forms/*.txt         SaveAsText ของฟอร์ม (ดิบ) + forms/*.vba + forms/_summary.md
├── reports/            เหมือน forms
├── modules/*.bas       standard/class modules
├── macros/*.txt        macros
├── linked-tables.md    ตารางที่ link ไปไฟล์/ฐานข้อมูลอื่น (ถ้ามี)
└── samples/*.csv       ตัวอย่างข้อมูล N แถวแรกต่อตาราง
```

### เมื่อฟอร์ม/รายงาน/module ที่มี VBA ดึงไม่ออก

เป็นเรื่องปกติ: ขณะปิด VBA Access export วัตถุที่มีโค้ดไม่ได้ จะขึ้นในหัวข้อ *Extraction errors* ของ INVENTORY.md (ข้อความ "search key was not found" หรือ "Property not found") บางไฟล์แม้เปิด VBA ก็ยังขึ้น "VBA project is corrupt" เพราะ compile มาจาก Access คนละรุ่น — ตาราง, query, macro, รายงานที่ไม่มีโค้ด และตัวอย่างข้อมูลยังได้ครบ

ทำตามลำดับ:
1. **แจ้งผู้ใช้** ว่าวัตถุไหนดึงไม่ได้กี่ชิ้น และเริ่มถอดจากส่วนที่ได้ก่อน — โปรแกรม Access จำนวนมากเก็บ logic ไว้ใน macro + query เป็นหลัก (เช่น ปุ่มบนฟอร์มเรียก macro ที่รัน query ต่อกัน) จึงถอดได้เกือบครบโดยไม่ต้องมี VBA
2. **ถ้าส่วนที่ขาดสำคัญ** (เช่น ปุ่มคำนวณเงินที่ไม่มี macro รองรับ) ให้ผู้ใช้ export จาก Access ของเขาเอง ซึ่งเปิดไฟล์ได้ตามปกติ: เปิดไฟล์ → `Ctrl+G` (Immediate window) → วางทีละบรรทัดแล้วกด Enter (สร้างโฟลเดอร์ปลายทางก่อน):
   ```vba
   For Each o In CurrentProject.AllForms: SaveAsText acForm, o.Name, "C:\Temp\vba\F_" & o.Name & ".txt": Next
   For Each o In CurrentProject.AllReports: SaveAsText acReport, o.Name, "C:\Temp\vba\R_" & o.Name & ".txt": Next
   For Each o In CurrentProject.AllModules: SaveAsText acModule, o.Name, "C:\Temp\vba\M_" & o.Name & ".bas": Next
   ```
   แล้วย้ายไฟล์เข้า `source/_extract/<name>/user-export/` โค้ด VBA อยู่หลังคำว่า `CodeBehindForm` ในแต่ละไฟล์
3. **ถ้าผู้ใช้ทำไม่ได้** — บันทึกใน README ว่าส่วนไหนถอดจากพฤติกรรมภายนอก (macro, query, ชื่อปุ่ม, RecordSource) และติด `🔍` ที่กฎที่ได้จากการอนุมาน

อย่าเปิดใช้ VBA/macro ของไฟล์เอง (เช่น เปลี่ยน AutomationSecurity) — โค้ดเดิมอาจลบหรือแก้ข้อมูลตอนเปิด

## ขั้นที่ 2 — วิเคราะห์ตามลำดับนี้

ลำดับสำคัญ เพราะแต่ละขั้นให้บริบทกับขั้นถัดไป ผลวิเคราะห์ของแต่ละขั้นลงไฟล์สเปคของเรื่องนั้นเท่านั้น (ตาราง → data-model.md, กฎ → business-rules.md, ...) ตาม `templates/`

1. **INVENTORY.md** — รายงานผู้ใช้สั้น ๆ: ขนาดระบบ, จำนวนแต่ละชนิด, ตารางใหญ่สุด, มี linked table ไหม, ดึงอะไรไม่ได้บ้าง
2. **เมนูหลัก → แผนผังเมนูใน screens.md** — เริ่มจาก `StartupForm` หรือ macro `AutoExec` ถ้าเป็นฟอร์ม `Switchboard` มาตรฐาน รายการเมนูทั้งหมดอยู่ในตาราง `Switchboard Items` (ดู samples — คอลัมน์ SwitchboardID, ItemNumber, ItemText, Command, Argument; ItemNumber 0 = ชื่อเมนูนั้น; Command 1 = ไปเมนูย่อย (Argument = SwitchboardID), 2 = เปิดฟอร์มเพิ่มข้อมูล, 3 = เปิดฟอร์มแก้ไข, 4 = เปิดรายงาน, 6 = ออก, 7 = รัน macro, 8 = รันฟังก์ชัน VBA) — นี่คือรายการสิ่งที่ผู้ใช้ใช้งานจริง วัตถุที่ไม่มีทางเข้าจากเมนูน่าจะเลิกใช้ (ติด `🔍` แล้วถามรวบเป็นคำถามเดียว)
3. **ตาราง → data-model.md** — **จัดกลุ่มก่อนลงรายละเอียด** (ดู "รูปแบบตารางที่พบบ่อย") แล้วลงรายละเอียดเฉพาะกลุ่มที่ต้องย้ายไประบบใหม่ ใช้ samples และ row count ดูค่าจริง: enum มีค่าอะไรบ้าง, ฟิลด์ไหนว่างตลอด, รูปแบบรหัส, ปี พ.ศ./ค.ศ., วันที่ล่าสุดของข้อมูล (บอกว่ายังใช้อยู่ไหม) Access มักไม่ได้ประกาศความสัมพันธ์ — อนุมานจาก JOIN ใน query และชื่อฟิลด์ที่ตรงกัน (ติดป้าย `🔍`)
4. **Macro + Query → business-rules.md** — ไล่จากปุ่มในเมนู: ปุ่ม → macro → ลำดับ `OpenQuery`/`RunSQL`/`OpenReport` query ชนิด UPDATE/APPEND/DELETE/MAKE-TABLE คือ *การกระทำ* ที่เปลี่ยนข้อมูล ให้เขียนเป็นขั้นตอนตามลำดับที่ macro เรียก แล้วสรุปเป็นกฎที่อ่านได้โดยไม่ต้องรู้ SQL
5. **ฟอร์ม + VBA → screens.md + business-rules.md** — เน้น event: `BeforeUpdate` (validation), `AfterUpdate` (คำนวณ/เติมค่า), `OnClick` ของปุ่ม, `OnOpen/OnLoad` (filter เริ่มต้น), `DefaultValue`, `ValidationRule` ของ control, `RowSource` ของ combo
6. **รายงาน → reports.md** — RecordSource, Sorting/Grouping, ช่องคำนวณ (`=Sum(...)`), พารามิเตอร์ (ค้น `[Forms]![...]` หรือ `[...?]` ใน SQL) รายงานที่เป็นแค่รายการข้อมูลสรุปเป็นแถวเดียวในตาราง
7. **Modules** — ฟังก์ชันที่ถูกเรียกจากหลายที่มักเป็น rule กลาง (เช่น gen เลขที่เอกสาร, แปลงตัวเลขเป็นคำอ่านภาษาไทย)

### รูปแบบตารางที่พบบ่อยใน Access

โปรแกรม Access ที่ใช้มานานมักมีตารางหลายร้อย แต่เป็นข้อมูลจริงไม่กี่สิบ จัดกลุ่มตามนี้ใน data-model.md แล้วลงรายละเอียดเฉพาะกลุ่ม A–C:

| กลุ่ม | ลักษณะที่สังเกตได้ | ระบบใหม่ |
|---|---|---|
| A. ข้อมูลหลัก/ธุรกรรม | มีข้อมูลต่อเนื่องถึงปัจจุบัน ถูกอ้างจากหลายฟอร์ม/query | ย้ายไป |
| B. ประวัติ (archive) | โครงเดียวกับตาราง A ชื่อลงท้าย ADD/_OLD/ปี มี APPEND query ย้ายข้อมูลเข้า | ย้ายไป (มักรวมกับตาราง A ได้) |
| C. Lookup | แถวน้อย ค่าคงที่ ใช้เป็น RowSource ของ combo | เป็น enum/master — ใส่ค่าทั้งหมดในสเปค |
| D. พารามิเตอร์ 1 แถว | 1 แถว ฟอร์มเขียนค่า (วันที่/แผนก) แล้ว query อ่าน | ไม่ย้าย — กลายเป็น parameter ของฟังก์ชัน ยกเว้นค่าที่ต้องจำข้ามรอบ |
| E. ตารางพักผล | ถูก DELETE แล้ว APPEND/MAKE-TABLE ใหม่ทุกครั้งที่รัน | ไม่ย้าย — คำนวณใหม่ แต่ขั้นตอนที่เติมมันคือ business rule |
| F. สำเนา/ทดลอง/เลิกใช้ | ชื่อ `สำเนาของ *`, ลงท้ายเลข, ข้อมูลหยุดนาน, ไม่มี query ใดอ้าง | ไม่ย้าย — ระบุหลักฐานแล้วถามผู้ใช้รวบครั้งเดียว |

### คำค้นที่มีประโยชน์ (ใช้ Grep ในโฟลเดอร์ extract)

| ค้นหา | เพื่อหา |
|---|---|
| `DoCmd.OpenForm\|DoCmd.OpenReport\|OpenForm\|OpenReport` | การนำทาง (ทั้ง VBA และ macro) |
| `DoCmd.RunSQL\|\.Execute\|OpenQuery\|RunSQL` | การเปลี่ยนแปลงข้อมูล |
| `MsgBox` | ข้อความ error/ยืนยัน = validation rule |
| `DLookup\|DSum\|DCount\|DMax` | การคำนวณ/ตรวจซ้ำ (DMax+1 = gen เลขที่) |
| `Forms!\|\[Forms\]!` | query/report ที่รับพารามิเตอร์จากฟอร์ม |
| `CurrentUser\|Environ\|fOSUserName` | การระบุตัวผู้ใช้/สิทธิ์ |
| `TransferSpreadsheet\|TransferText\|OutputTo` | import/export → integrations.md |
| `Round\|Int\(\|Fix\(\|CCur\|Format\(` | วิธีปัดเศษ |

### กับดักที่พบบ่อยใน Access

- **การปัดเศษมีหลายแบบในโปรแกรมเดียว** — รวบไว้เป็นตาราง RND-x ที่หัว business-rules.md แล้วให้แต่ละกฎอ้าง ID: `Round()` = banker's rounding (2.5 → 2), `Val(Format(x,"#"))` = ครึ่งปัดออกจากศูนย์ (2.5 → 3), `Int()` = ปัดลง, `Fix()` = ตัดทศนิยมเข้าหาศูนย์ ถ้าไม่แน่ใจผลจริงให้ถามผู้ใช้ — ยอดเงินที่ต่างกัน 1 บาทคือสิ่งที่ผู้ใช้จะเจอเป็นอย่างแรก
- **Yes/No เก็บเป็น -1/0** ไม่ใช่ 1/0
- **Date/Time เก็บวันและเวลารวมกัน** — query `= Date()` จะพลาดแถวที่มีเวลา
- **ฟิลด์ Lookup ในตาราง** — ค่าที่เก็บจริงเป็น ID แต่แสดงเป็นข้อความ ดูใน samples ว่าเก็บอะไรจริง
- **Null vs ค่าว่าง ""** — Access แยกกัน `AllowZeroLength` มีผล
- **ไม่มี primary key** — ตารางเก่าหลายตารางไม่มี PK และมีแถวซ้ำ ต้องระบุใน "ปัญหาคุณภาพข้อมูล" เพราะกระทบการย้ายข้อมูล
- **ค่าที่ hard-code** ใน VBA/macro/query (อัตรา, รหัสแผนก, ชื่อเครื่องพิมพ์, ปี) — ดึงออกมาเป็นค่าตั้งค่าในสเปค
- **query ชุดเลขต่อกัน** (`คำนวณ1`, `คำนวณ2`, ...) มักเป็นขั้นตอนเดียวที่แยกเป็นหลายท่อ — ยืนยันลำดับจาก macro ที่เรียก ไม่ใช่จากเลขในชื่อ
- **ขั้นตอนลบข้อมูลปีเก่า / ย้ายเข้าประวัติ** — มีแทบทุกโปรแกรม เป็นเพราะ Access จำกัดขนาด 2 GB ระบบใหม่อาจไม่ต้องการ → ถามผู้ใช้
- **ข้อความภาษาไทยเพี้ยน** — สคริปต์แปลงเป็น UTF-8 แล้ว ถ้ายังเพี้ยนให้แจ้งผู้ใช้
- **ไฟล์แยก Front-end/Back-end** — ถ้ามี linked table ไปไฟล์อื่น ต้องได้ไฟล์ back-end ด้วยถึงจะเห็นข้อมูล

## ขั้นที่ 3 — คุยกับผู้ใช้

หลังร่างแต่ละไฟล์ ให้ถามเฉพาะสิ่งที่โค้ดตอบไม่ได้: ความหมายทางธุรกิจ, เหตุผลของกฎ, ฟังก์ชันไหนยังใช้อยู่, บั๊กไหนต้องแก้ ถามเป็นข้อ ๆ พร้อมหลักฐานที่พบ เรียงตามผลกระทบ (ยอดเงินก่อน) เช่น:

> ❓ Q-03: ใน `frmIssue` ปุ่ม "อนุมัติ" ตรวจ `UserLevel >= 3` (VBA บรรทัด 88) — ระดับ 3 คือหัวหน้าแผนกใช่ไหม

แยกให้ชัดระหว่าง **"ระบบเดิมทำอะไร"** (ข้อเท็จจริงจากโค้ด — งานหลักของการถอด) กับ **"ระบบใหม่ควรทำอะไร"** (การตัดสินใจของผู้ใช้ — มักยังไม่พร้อมตอบ) ใน business-rules.md บรรยายพฤติกรรมเดิมตามจริง จุดที่ดูผิดปกติติดป้าย `⚠ ข้อสังเกต` แล้วเก็บคำถามฝั่งระบบใหม่แยกหมวดใน open-questions.md อย่าตัดสินใจแทนผู้ใช้ว่าจะ "แก้" หรือ "คงไว้"

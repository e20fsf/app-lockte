# โครงพิมพ์ใบตรวจ QA (ระดับ A)

`generate.py` สร้าง `src/Lockte.Web/Reports/Order/Qa/QaFormTemplates.Generated.cs` จากนิยามรายงานที่กู้ไว้ (`reports_decompiled/*.txt`) และ PDF ตัวอย่างที่พิมพ์จากรายงานนั้น
(`lot 1`) — เส้น กรอบ ข้อความคงที่ ช่องข้อมูล และตำแหน่งที่ข้อความของรุ่นไปอยู่ ทั้งหมดเป็นมิลลิเมตรจากมุมซ้ายบนของกระดาษ

```
python tools/qa-form-templates/generate.py <reports_decompiled> <lot 1> src/Lockte.Web/Reports/Order/Qa/QaFormTemplates.Generated.cs
```

- ใช้รายงาน **หนึ่งใบ** ต่อกลุ่มเป็นต้นแบบ (`FAMILIES` ในสคริปต์) — เลือกรุ่นที่ตำแหน่งตรงกับรุ่นส่วนใหญ่ของกลุ่ม; ข้อความที่แต่ละรุ่นเปลี่ยน (ชื่อ เลขเอกสาร จุดวัด ตำหนิ รุ่นที่ใช้ร่วม) ไม่ถูกเขียนลงไฟล์ — มาจากสเปคในฐานข้อมูล (#128, #129)
- กลุ่มที่ไม่มี PDF ตัวอย่าง (แม่พิมพ์ใหม่ WI-QA022) วางตามพิกัดของรายงานเอง (x = 10 + left·25.4/1440, y = 4.169 + 0.9884·top·25.4/1440 — หาจาก PDF ตัวอย่างของกลุ่มอื่น คลาด < 0.05 มม.)
- เพิ่มกลุ่มใหม่ (#131–#133): เพิ่มแถวใน `FAMILIES` และกลุ่มใน `QaFormTemplates.KeyOfGroup` แล้วรันสคริปต์ใหม่
- ต้องมี PyMuPDF (`pip install pymupdf`) · นิยามรายงานและ PDF เป็นข้อมูลของบริษัท อยู่นอก repo

## ตรวจว่าพิมพ์ตรงตัวอย่าง

```
LOCKTE_QA_COMPARE="<reports_decompiled>|<โฟลเดอร์ผลลัพธ์>" LOCKTE_TEST_SQL=... dotnet test tests/Lockte.Web.Tests --filter Prints_the_sheets_of_the_recovered_reports_for_comparison
python tools/qa-form-templates/compare.py "<lot 1>" "<โฟลเดอร์ผลลัพธ์>" ไดคาสท์ ตกแต่ง
```

สคริปต์พิมพ์เฉพาะตัวเลข (ตำแหน่งตัวอักษร/เส้นคลาดกี่ มม.) ห้ามนำ PDF หรือภาพเทียบเข้า repo

# ชุดปิดปีใน Access (#135)

- `sql/` — คำสั่ง SQL ของ Access (วางทีละไฟล์ใน Query Design → SQL View ตามลำดับเลข) ลบ pd ซ้ำ ธ.ค. 2568 ใน `P_ST` ที่ซ้ำเป๊ะกับ `P_ADDST` พร้อมตารางสำรองและรายงานยอดก่อน/หลัง
- `rehearse.py` — ซ้อมบนสำเนาผ่าน DAO (ต้องมี Access Database Engine บน Windows) ปฏิเสธไฟล์ที่ชื่อไม่มี `rehearsal`
- รันบุ๊กภาษาไทยและผลซ้อม: [docs/migration/year-close-2569.md](../../docs/migration/year-close-2569.md)
- เทสต์: `tests/Lockte.Web.Tests/YearCloseKitTests.cs` (ไฟล์ Access จำลอง ไม่มีข้อมูลจริง)

ผลลัพธ์ที่มีรหัสสินค้า/แม่พิมพ์จริงห้าม commit — `rehearse.py` เขียนลงโฟลเดอร์ที่ผู้ใช้ระบุ (ใช้ใน `source/work/` ที่ gitignored)

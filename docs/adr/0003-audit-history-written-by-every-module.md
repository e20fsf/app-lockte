---
status: accepted
---

# ทุก module เขียนประวัติการแก้ไขลง `audit.History` ได้ (ข้อยกเว้นของ ADR 0002)

ADR 0002 ให้แต่ละ module เขียนได้เฉพาะตารางใน schema ของตัวเอง แต่ประวัติการแก้ไข (#39, #43) ต้องบันทึก ค่าเดิม → ค่าใหม่ รายช่อง ของทุกตาราง **ใน transaction เดียวกับการแก้** — ถ้าแก้สำเร็จแต่บันทึกประวัติไม่สำเร็จ (หรือกลับกัน) ประวัติจะเชื่อไม่ได้ เราจึงยอมให้ DbContext ของทุก module map ตาราง `audit.History` (ไม่สร้างใน migration ของตัวเอง) และให้ interceptor กลาง (`AuditInterceptor` ใน SharedKernel) **เพิ่มแถวลงตารางนี้ได้อย่างเดียว** ภายใน SaveChanges เดียวกัน ส่วนการอ่าน ค้นหา และโครงสร้างตาราง เป็นของ `AuditDbContext` (schema `audit`) ผู้เดียว

## Considered Options

- **ตารางประวัติใน schema ของแต่ละ module (`hr.History`, `org.History` …)**: ไม่ผิด ADR 0002 แต่หน้าค้นประวัติหน้าเดียวต้อง union หลายตาราง และทุก module ต้องมี migration ของตารางประวัติซ้ำกัน
- **ส่งประวัติผ่าน service ของ module audit หลังบันทึก**: ต้องใช้ transaction ข้าม DbContext หรือยอมให้ประวัติหายเมื่อขั้นที่สองล้มเหลว
- **SQL Server Temporal Tables / Change Data Capture**: เก็บทั้งแถว ไม่รู้ว่าใครแก้ (ทุกคนต่อฐานข้อมูลด้วย user เดียวกันของแอป) และค้นรายช่องยาก

## Consequences

- `audit.History` เป็นตารางเพิ่มอย่างเดียว ไม่มีโค้ดใดแก้หรือลบแถว (ประวัติเก็บตลอดไป) — ภายหลังบังคับได้ด้วยสิทธิ์ฐานข้อมูล (INSERT/SELECT เท่านั้น)
- model snapshot ของทุก module มีตาราง History (แบบ excluded) — เปลี่ยนโครงตาราง History ต้องเพิ่ม migration ว่างให้ module อื่นด้วย
- ต้อง migrate schema `audit` ก่อน module อื่น (`LockteDatabase.MigrateAsync`)

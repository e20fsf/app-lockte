# PROTOTYPE — Razor → Playwright/Chromium PDF (ทิ้งได้)

ตอบตั๋ว [#21](https://github.com/e20fsf/app-lockte/issues/21) ว่าวิธีทำ PDF ตาม ADR 0001 ใช้กับ **แบบ คร.2** และ **ทะเบียนผู้ประกันตน** ได้จริงไหม — ผลและข้อสรุปอยู่ในตั๋ว · **ข้อมูลปลอมทั้งหมด** (repo นี้ public: ชื่อบริษัท/ผู้จัดการ/เลขบัญชีนายจ้างเป็นค่าสมมติ, เลขบัตรขึ้นต้น 0 ไม่มี checksum)

```bash
dotnet run --urls http://localhost:5099
```
ครั้งแรกจะดาวน์โหลด Chromium headless shell (~270 MB) ลง `bin/…/ms-playwright` แล้วเปิด http://localhost:5099

| URL | ได้อะไร |
|---|---|
| `/kr2?i=0` | แบบ คร.2 — ปรับ `pt` (ขนาดฟอนต์), `ml`/`mt` (ระยะขอบ ซม.) ไว้เทียบกับใบพิมพ์จริง |
| `/register?rows=450` | ทะเบียนผู้ประกันตน A4 แนวนอน |
| `/thai` | ทดสอบสระ/วรรณยุกต์ซ้อน + ตัดคำในคอลัมน์แคบ |
| `/bench?n=5&parallel=5` | เวลา + RAM (แอป และ Chromium) |
| `/env` | user/app pool/IIS/path ของ browser |
| `?font=embed\|local\|route`, `?pool=0` | สวิตช์ทดลองประสิทธิภาพ |

ไฟล์: `Reports/*.razor` (หน้ารายงาน — ตำแหน่งจากรายงาน Access เดิม), `PdfService.cs` (browser เดียว + page pool + ฟอนต์ผ่าน request interception), `vm/` (แพ็กเกจ + สคริปต์ทดสอบบน IIS ของ VM)

ฟอนต์ TH Sarabun New (SIPA) แจกจ่ายได้โดยเสรี

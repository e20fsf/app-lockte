# ทดสอบ PDF บน IIS ของ VM โรงงาน (prototype — ทิ้งได้)

ตอบเกณฑ์ข้อ "Chromium รันได้ใต้ app pool ของ IIS บน VM Windows Server 2025" ของตั๋ว #21 ใช้**ข้อมูลปลอม**ทั้งหมด

**ต้องมีก่อน (ผู้ดูแลระบบติดตั้งตาม ADR 0001):** IIS (Web-Server role) และ ASP.NET Core Hosting Bundle .NET 10 ไม่ต้องต่ออินเทอร์เน็ต เพราะ Chromium อยู่ในแพ็กเกจแล้ว

1. แตก `report-pdf-proto.zip` ไว้ที่ **path สั้น ๆ** เพราะไฟล์ใน Chromium มีชื่อยาว ให้ใช้คำสั่งนี้แทน Extract All ของ Explorer
   ```powershell
   mkdir C:\rpp; tar -xf report-pdf-proto.zip -C C:\rpp; cd C:\rpp
   ```
2. เปิด PowerShell แบบ **Run as Administrator** ในโฟลเดอร์ `C:\rpp` แล้วรัน
   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   .\Install-Proto.ps1
   ```
   สคริปต์สร้างเว็บ `ReportPdfProto` ที่พอร์ต 8099 (app pool แยกของตัวเอง) แล้วเรียกทุกรายงานผ่าน IIS ใช้เวลาประมาณ 2–5 นาที
3. ส่งไฟล์ `C:\rpp\results.zip` กลับมา ในไฟล์มี PDF, เวลาที่ใช้, RAM, รุ่น OS และ log
4. ลบทุกอย่างที่สร้างไว้ด้วย `.\Remove-Proto.ps1` แล้วลบโฟลเดอร์ `C:\rpp`

ถ้าสคริปต์หยุดด้วย error ให้ส่ง `C:\rpp\results\install-log.txt` กลับมาแทน

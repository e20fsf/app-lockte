---
status: accepted
---

# Stack: ASP.NET Core 10 + Blazor Server + SQL Server 2025 บน IIS (Windows Server 2025)

ระบบใหม่ทดแทน Access 5 โปรแกรม ผู้ใช้ ~10 คน พร้อมกัน ~5 คน ต้องส่งงานแรกใน 1 เดือน และติดตั้งบน VM Windows Server 2025 ในโรงงาน เราจึงเลือก **ASP.NET Core 10 (LTS ถึง พ.ย. 2028)** ใช้ UI แบบ **Blazor Server (Interactive Server) + MudBlazor** เก็บข้อมูลใน **SQL Server 2025 Standard** ผ่าน EF Core และรันแอปด้วย **IIS + .NET Hosting Bundle** โดยไม่ใช้ container ส่วน PDF รายงานทำจากหน้า Razor แล้วแปลงด้วย **Playwright/headless Chromium** เหตุผลหลักคือ บน Windows Server ทั้ง runtime, web server, ฐานข้อมูล และ OS มาจากผู้ผลิตเดียว และมีเอกสาร host ทางการครบ ขณะที่ฟอร์ม Access ซึ่งเป็นรายการหลักคู่รายการย่อย ย้ายมาเป็น component ของ Blazor ได้ตรงที่สุด VM, environment และ license ผู้ดูแลเป็นผู้จัดเตรียม จึงไม่ได้ใช้เป็นข้อจำกัดในการเลือก

## Considered Options

- **Django 5.2 + PostgreSQL**: หน้า admin ทำหน้าจัดการข้อมูลหลักได้เร็วที่สุด แต่บน Windows ใช้ Gunicorn ไม่ได้ และเอกสาร Django ไม่ครอบคลุม Windows ผู้ใช้ตัดสินให้ใช้ Windows Server + SQL Server
- **PostgreSQL ใน container บน VM Ubuntu**: เคยพิจารณาเพราะอยากใช้ container แต่ตัดทิ้งเมื่อเลือก Windows Server + SQL Server เพราะถ้าจะรัน Linux container บน VM Windows ต้องใช้ nested virtualization ซึ่ง host Hyper-V 2012 R2 น่าจะไม่รองรับ (ยังไม่ได้ตรวจกับเอกสาร Microsoft) และ Windows container ใส่ Chromium ได้ยาก
- **Node/TypeScript**: ต้องประกอบชิ้นส่วนเองมากที่สุด
- **Razor Pages + htmx / SPA + Web API / ทำ API ก่อน (Blazor WebAssembly)**: SPA และแนวทางทำ API ก่อนเพิ่มงานเกินกำหนด 1 เดือน ส่วน htmx ทำหน้าจอแก้ไขหลายบรรทัดแบบ subform ได้ยากกว่า
- **QuestPDF**: จัดรูปอักษรไทยถูก แต่ตัดคำไทยไม่ได้ ต้องแทรก ZWSP เอง (ดู research `thai-reports-excel`)

## Consequences

- **logic ทางธุรกิจต้องอยู่ใน service layer แยกจาก Blazor component** (เช่น คำนวณค่าแรง, บันทึกเวลาทำงาน, ORDER) เพื่อให้เพิ่ม Web API (Minimal API) ในแอปเดิมภายหลังได้ สำหรับแอป mobile หรือการต่อเครื่องลงเวลา โดยไม่เขียน logic ซ้ำ ระยะแรกยังไม่ทำ API
- IIS ต้องเปิด feature WebSocket เพื่อให้ Blazor Server ทำงานได้ ถ้าเครือข่ายหลุด ผู้ใช้ผ่าน VPN (สำนักงานสีลม) จะเห็นข้อความ reconnect
- ต้องเผื่อ RAM ใน VM สำหรับ Chromium ระหว่างพิมพ์รายงาน และต้องพิสูจน์ว่า Chromium รันจาก app pool ของ IIS ได้ — เป็นเกณฑ์หนึ่งของ prototype รายงาน คร.2 ถ้าไม่ผ่าน ทางสำรองคือ QuestPDF ซึ่งยังอยู่บน stack เดิม
- การเข้าระบบใช้ ASP.NET Core Identity แบบ user/password ไม่ใช้ระบบ role ของ Identity มีเพียงธง `IsAdmin` ของบัญชี admin ซึ่งเห็นเฉพาะหน้าจัดการผู้ใช้ระบบ (#39)
- การอ่าน `.accdb` ทำด้วยเครื่องมือย้ายข้อมูลแยกต่างหาก (.NET console) รันบน PC ที่มี Microsoft 365 Access Runtime ไม่รันบนเซิร์ฟเวอร์ เพราะ Microsoft ไม่สนับสนุนการใช้ ACE ฝั่ง server
- ต้องอัปเกรด .NET major อย่างน้อยหนึ่งครั้งก่อน พ.ย. 2028

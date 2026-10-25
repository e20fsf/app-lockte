# ติดตั้งระบบ Lockte บนเครื่อง server

เอกสารสำหรับผู้ดูแลระบบ ใช้ทั้งการติดตั้งครั้งแรกและการอัปเดต อ้างอิง [ADR 0001](../docs/adr/0001-stack-aspnet-blazor-sqlserver-iis.md)

แพ็กเกจ `lockte-<วันที่>-<commit>.zip` มีแอปที่ publish แล้วและ Chromium สำหรับพิมพ์ PDF อยู่ข้างใน เครื่อง server จึงไม่ต้องต่ออินเทอร์เน็ต และไม่ต้องติดตั้ง .NET SDK หรือ Playwright

## เครื่องที่ต้องมี

| | |
|---|---|
| OS | Windows Server 2025 (VM บน Hyper-V ของโรงงาน) |
| RAM | 8 GB ขึ้นไป ใช้ร่วมกับ SQL Server ควรตั้ง **max server memory** ของ SQL Server ไว้ประมาณ 4–5 GB เพราะแอปกับ Chromium ใช้ได้ถึงราว 1 GB ตอนพิมพ์รายงานพร้อมกัน |
| ฐานข้อมูล | SQL Server 2025 Standard (เครื่องเดียวกันหรือคนละเครื่องก็ได้) |
| IIS | สคริปต์ติดตั้งให้เอง ได้แก่ Web-Server, WebSocket และ Application Initialization |
| .NET | **ASP.NET Core Hosting Bundle .NET 10** ถ้า server ไม่มีอินเทอร์เน็ต ให้ดาวน์โหลดจาก <https://dotnet.microsoft.com/download/dotnet/10.0> (หัวข้อ Hosting Bundle) แล้วใส่ไว้ในโฟลเดอร์ `prereqs\` ของแพ็กเกจ สคริปต์จะติดตั้งให้ |
| ฐานข้อมูล login | **SQL login** ที่สร้างฐานข้อมูลได้ (ครั้งแรกแอปสร้างตารางเอง) หรือถ้า SQL Server อยู่เครื่องเดียวกัน ใช้ Windows authentication ของ app pool (`IIS AppPool\Lockte`) ได้ |

## 1. สร้างแพ็กเกจ (เครื่องนักพัฒนา)

```powershell
.\deploy\Build-Package.ps1                                       # ได้ out\lockte-<version>.zip
.\deploy\Build-Package.ps1 -HostingBundle C:\Downloads\dotnet-hosting-10.0.x-win.exe   # ใส่ Hosting Bundle ไปด้วย
```

## 2. ติดตั้งครั้งแรก (บน server)

1. คัดลอก zip ไปที่ server แล้วแตกไฟล์ไว้ที่ **path สั้น ๆ** ด้วย `tar` เพราะชื่อไฟล์ใน Chromium ยาวเกินกว่าที่ Extract All ของ Explorer รับได้
   ```powershell
   mkdir C:\lockte-pkg; tar -xf lockte-<version>.zip -C C:\lockte-pkg; cd C:\lockte-pkg
   ```
2. เปิด PowerShell แบบ **Run as Administrator** ในโฟลเดอร์นั้น แล้วรัน
   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   .\Install-Lockte.ps1 -SqlServer <ชื่อหรือ IP ของ SQL Server> -Database Lockte -SqlUser <SQL login> -ResetAdmin
   ```
   - สคริปต์ถามรหัสผ่านของ SQL login (ไม่แสดงบนจอ และไม่เก็บไว้ใน log) แล้วถามรหัสชั่วคราวของบัญชี `admin` 2 ครั้ง
   - ค่าเริ่มต้น: site และ app pool ชื่อ `Lockte` พอร์ต **8080** ติดตั้งที่ `C:\Lockte` เปลี่ยนได้ด้วย `-SiteName`, `-Port`, `-InstallRoot`
   - ถ้าไม่ใส่ `-SqlUser` จะใช้ Windows authentication ของ app pool ซึ่งต้องสร้าง login `IIS AppPool\Lockte` ใน SQL Server ก่อน
3. รอจนขึ้น `OK` สคริปต์จะตรวจ 2 อย่าง:
   - หน้า login ตอบกลับ
   - **พิมพ์ PDF ได้จริงภายใต้ app pool ของ IIS** (ตั๋ว #21) โดยเรียก `http://localhost:8080/health/reports` ซึ่งเรียกได้จากเครื่อง server เท่านั้น
4. ส่งไฟล์ `C:\Lockte\results-<เวลา>.zip` กลับมาให้ทีมพัฒนา ในไฟล์มี log, ผลตรวจ, เวลาพิมพ์, RAM, รุ่น OS และ PDF ตัวอย่าง **ไม่มีรหัสผ่านอยู่ในนั้น**
5. เปิด `http://<server>:8080` แล้ว login ด้วย `admin` และรหัสชั่วคราว ระบบจะบังคับให้เปลี่ยนรหัสทันที จากนั้นสร้าง user ให้ HR ที่หน้าจัดการผู้ใช้

## 3. ตั้งค่าหลังติดตั้ง

ค่าที่ต้องตั้งก่อนพิมพ์เอกสารจริง อยู่ที่ `C:\Lockte\config\appsettings.Production.json` ไฟล์นี้อ่านได้เฉพาะ Administrators และ app pool เพราะมีรหัสผ่านฐานข้อมูลอยู่ข้างใน:

```json
{
  "ConnectionStrings": { "Lockte": "..." },
  "Reports": { "BrowsersPath": "C:\\Lockte\\app\\ms-playwright", "InstallBrowser": false },
  "Employer": {
    "ManagerTitle": "นาย", "ManagerFirstName": "...", "ManagerLastName": "...",
    "FactoryManagerName": "...",
    "Office":    { "HouseNo": "...", "Road": "...", "Subdistrict": "...", "District": "...", "Province": "...", "Phone": "..." },
    "Workplace": { "HouseNo": "...", "Road": "...", "Subdistrict": "...", "District": "...", "Province": "...", "Phone": "..." },
    "SocialSecurityAccountNo": "...", "SocialSecurityBranchNo": "..."
  }
}
```

- **Employer:\*** ใช้กับ คร.2, ทะเบียนผู้ประกันตน, หนังสือรับรอง และประกาศพักร้อน ตามรายชื่อช่องใน `appsettings.json` ของแอป ใส่เฉพาะช่องที่ต้องการเปลี่ยน
- **ผู้ลงนามสลิป** (แผนกบุคคล, ผู้จัดการโรงงาน) ตั้งในหน้าจอของระบบ ไม่ได้อยู่ในไฟล์นี้
- แก้ไฟล์แล้ว ให้รัน `.\Install-Lockte.ps1` อีกครั้ง สคริปต์จะคัดลอกค่าเข้าแอปและเริ่มแอปใหม่ หรือจะคัดลอกไฟล์ไปที่ `C:\Lockte\app\` เองแล้ว `Restart-WebAppPool Lockte` ก็ได้
- **ห้าม** commit ไฟล์นี้หรือส่งต่อทางแชต/อีเมล

## 4. ย้ายข้อมูลจาก Access

รันเครื่องมือย้ายข้อมูลบน **PC ที่มี Microsoft 365 / Access Runtime** ไม่ใช่บน server ใช้สำเนาไฟล์ `.accdb` ชี้ connection string ไปที่ฐานข้อมูลของ server รายละเอียดดูหัวข้อ "เครื่องมือย้ายข้อมูล" ใน [README หลัก](../README.md)

- แอปต้องเริ่มบนฐานข้อมูลนี้แล้วอย่างน้อยหนึ่งครั้ง เพื่อให้ตารางถูกสร้างก่อน
- การย้ายแต่ละรอบ**ลบข้อมูลหลักเดิมแล้วโหลดใหม่** ห้ามรันหลังเริ่มใช้งานจริงแล้ว
- รายงานผลการย้ายมีชื่อพนักงานจริง เก็บไว้ในเครื่อง ห้ามใส่ใน git

## 5. อัปเดตเวอร์ชัน

แตกแพ็กเกจใหม่แล้วรัน `.\Install-Lockte.ps1` โดยไม่ต้องใส่ parameter สคริปต์จะทำดังนี้:
- คงไฟล์ตั้งค่าไว้
- หยุด app pool
- คัดลอกแอปใหม่ทับ
- เริ่มแอปอีกครั้ง (แอปปรับตารางฐานข้อมูลเองตอนเริ่ม)
- ตรวจซ้ำแล้วสร้าง results zip ใหม่

**สำรองฐานข้อมูลก่อนอัปเดตทุกครั้ง**

## 6. สำรองข้อมูล

ตั้ง SQL Server Agent job (หรือ Maintenance Plan) ให้ backup ฐานข้อมูล `Lockte` แบบ full ทุกคืน และเก็บสำเนาไว้นอก VM ด้วย ไฟล์อื่นที่ควรสำรองมีแค่ `C:\Lockte\config\`

## 7. เมื่อมีปัญหา

| อาการ | ดูที่ |
|---|---|
| เปิดเว็บไม่ขึ้น / HTTP 500.30 | `C:\Lockte\logs\stdout_*.log` (log ของแอป) |
| พิมพ์รายงานไม่ออก | เปิด `http://localhost:8080/health/reports` **บน server** ถ้าได้ `"ok":false` จะมีข้อความ error บอก หรือหาคำว่า `Chromium` ใน stdout log |
| หน้าจอขึ้น reconnect บ่อย | ตรวจว่า IIS เปิด WebSocket แล้ว (สคริปต์ติดตั้งให้) และเครือข่าย/VPN ไม่หลุด |
| ลืมรหัส admin | `.\Install-Lockte.ps1 -ResetAdmin` แล้วตั้งรหัสชั่วคราวใหม่ |

## 8. ถอนการติดตั้ง

```powershell
.\Remove-Lockte.ps1                 # ลบ site, app pool และ firewall rule ส่วนไฟล์กับฐานข้อมูลยังอยู่
.\Remove-Lockte.ps1 -RemoveFiles    # ลบ C:\Lockte ด้วย (รวมไฟล์ตั้งค่าและ log) แต่ไม่ลบฐานข้อมูล
```

## ยังไม่ได้ทำ

- **HTTPS:** ตอนนี้ใช้ HTTP ภายในเครือข่ายโรงงานและ VPN ถ้าต้องการ HTTPS ให้ติดตั้งใบรับรองแล้วเพิ่ม binding 443 ใน IIS ไม่ต้องแก้แอป
- **SQL login เฉพาะของแอป:** ควรใช้ login ที่มีสิทธิ์เฉพาะฐานข้อมูล `Lockte` (db_owner) แทน `sa` เมื่อสร้างฐานข้อมูลครั้งแรกเสร็จแล้ว

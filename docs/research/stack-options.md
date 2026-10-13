# เทียบตัวเลือก stack เว็บแอปบน Windows Server

> ตั๋ว: [#11](https://github.com/e20fsf/app-lockte/issues/11) (ลูกของแผนที่ [#8](https://github.com/e20fsf/app-lockte/issues/8))
> ค้นเมื่อ: 2026-10-13 — ตัวเลขวันหมดอายุ/เวอร์ชันอ้างจากหน้าเว็บต้นทาง ณ วันนี้ ควรตรวจซ้ำก่อนตัดสิน
> **ไฟล์นี้ไม่ตัดสินผู้ชนะ** — การเลือกอยู่ในตั๋ว "เลือก stack" ([#13](https://github.com/e20fsf/app-lockte/issues/13))

## เงื่อนไขที่ใช้เทียบ (จากตั๋ว/แผนที่)

- ผู้ใช้ ~10 คน พร้อมกัน ~5 คน, UI ภาษาไทย, เบราว์เซอร์บน PC
- รันบน Windows Server ในโรงงาน (on-prem)
- ส่งงานแรกใน 1 เดือน — หน้าจอ CRUD + รายงานจำนวนมาก (บางรายงานต้องตรงรูปแบบเอกสารราชการ/ธนาคาร)
- ต้องอ่าน `.accdb` เดิมเพื่อย้ายข้อมูล
- ขยายอีก 3 โปรแกรมภายหลัง, ดูแลระยะยาว
- โปรแกรมเมอร์ชำนาญทุกภาษา, ไม่มีข้อจำกัดงบ/license

ด้วยโหลดระดับ ~5 คนพร้อมกัน **ประสิทธิภาพไม่ใช่ตัวแยก** ตัวเลือกใดก็รับได้ (ข้อสรุปเชิงวิศวกรรม ไม่ใช่อ้างจากแหล่ง) — ตัวแยกจริงคือ การ host บน Windows, ความเร็วทำ CRUD/รายงาน, การอ่าน .accdb และอายุ support

---

## 1. อายุ support (LTS) ของ runtime / framework / database

| ส่วนประกอบ | เวอร์ชัน LTS ที่ควรใช้ตอนนี้ | หมดอายุ | นโยบาย | แหล่ง |
|---|---|---|---|---|
| .NET | .NET 10 (LTS, ออก 11 พ.ย. 2025) | 14 พ.ย. 2028 | LTS = 3 ปี (เลขคู่), STS = 2 ปี; .NET 8 หมด 10 พ.ย. 2026 | [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) |
| Node.js | v24 "Krypton" (Maintenance LTS ตั้งแต่ 20 ต.ค. 2026) / v26 (เข้า Active LTS 28 ต.ค. 2026) | v24: 30 เม.ย. 2028 · v26: 30 เม.ย. 2029 | LTS รวม ~30 เดือน; ตั้งแต่ v27 เปลี่ยนเป็นออกปีละครั้ง | [Node.js Release schedule](https://github.com/nodejs/Release), [previous releases](https://nodejs.org/en/about/previous-releases) |
| Django | 5.2 LTS | เม.ย. 2028 | 6.2 LTS จะออก เม.ย. 2027 (support ถึง เม.ย. 2030) | [Django download/supported versions](https://www.djangoproject.com/download/) |
| PostgreSQL | 18 (หรือ 17) | 18: 14 พ.ย. 2030 · 17: 8 พ.ย. 2029 | ทุก major support 5 ปี | [PostgreSQL versioning](https://www.postgresql.org/support/versioning/) |
| SQL Server | 2025 (17.x) | ตาม Microsoft Lifecycle | — | [SQL Server 2025 editions](https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2025?view=sql-server-ver17) |

ข้อสังเกต: .NET 10 และ Django 5.2 หมด support ไล่เลี่ยกัน (ปลาย/ต้น 2028) — ทุก stack ต้องวางแผนอัปเกรด major อย่างน้อยหนึ่งครั้งภายใน ~2 ปี

## 2. การติดตั้ง/รันบน Windows Server

### ASP.NET Core (.NET)
- **IIS** — ติดตั้ง *.NET Hosting Bundle* (มี ASP.NET Core Module); รองรับ Windows Server 2012 R2 ขึ้นไป; template ใช้โมเดล **in-process** เป็นค่าเริ่มต้น ([Host ASP.NET Core on Windows with IIS](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0))
- **Windows Service** โดยตรง — เรียก `AddWindowsService()` ใน Program.cs แล้วลงทะเบียนด้วย `New-Service` ใน PowerShell ([Host ASP.NET Core in a Windows Service](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-10.0))
- เป็นเส้นทางเดียวในสามตัวที่ผู้ผลิต runtime เขียนเอกสาร host บน IIS/Windows Service เองโดยตรง

### Node.js / TypeScript
- Node ไม่มีกลไก Windows Service ในตัว และเอกสารทางการของ Node ไม่มีคู่มือ host บน IIS (ไม่พบในแหล่งที่ค้น) — ทางเลือกคือ
  - IIS + **HttpPlatformHandler**: โมดูล IIS 8+ ที่ "จัดการ process ของ http listener ใดก็ได้ เช่น Node.exe" และ proxy request ไปให้ ([HttpPlatformHandler reference](https://learn.microsoft.com/en-us/iis/extensions/httpplatformhandler/httpplatformhandler-configuration-reference)) — หมายเหตุ: หน้าเอกสารนี้ลงวันที่ 2015 ไม่ค่อยถูกอัปเดต
  - หรือใช้ตัวห่อ service ของ third-party (เช่น NSSM / node-windows — ไม่ได้ตรวจแหล่งต้นทางในรอบนี้)
- ORM: Prisma กำลังเปลี่ยนรุ่น — Prisma ORM 8 รองรับ PostgreSQL (release candidate) แต่ **SQL Server ยังไม่มีใน ORM 8** (ใช้ Prisma 7 ได้) ([Prisma supported databases](https://www.prisma.io/docs/orm/reference/supported-databases)) — ถ้าเลือก Node ควรจับคู่ PostgreSQL หรือเลือก ORM อื่น

### Python / Django
- เอกสาร Django มีแค่คู่มือ **ติดตั้งเพื่อพัฒนา** บน Windows ([How to install Django on Windows](https://docs.djangoproject.com/en/5.2/howto/windows/)); หน้า deployment แนะนำ Gunicorn, uWSGI, mod_wsgi, Daphne, Hypercorn, Uvicorn และ **ไม่พูดถึง Windows/IIS เลย** ([How to deploy Django](https://docs.djangoproject.com/en/5.2/howto/deployment/))
- **Gunicorn** ระบุตัวเองว่า "Python WSGI HTTP Server for UNIX" ([gunicorn README](https://github.com/benoitc/gunicorn)) — ใช้บน Windows ไม่ได้ในทางปฏิบัติ
- ทางที่ใช้ได้บน Windows: **Waitress** — "runs on CPython on Unix and Windows" และวางตัวเป็น production-quality ([Waitress docs](https://docs.pylonsproject.org/projects/waitress/en/stable/)) อยู่หลัง IIS (HttpPlatformHandler หรือ reverse proxy) + ตัวห่อ service
- Django ต้องการ I/O เป็น UTF-8 — บน Windows อาจต้องตั้ง `PYTHONUTF8` ([Django on Windows](https://docs.djangoproject.com/en/5.2/howto/windows/)) — เกี่ยวกับข้อความภาษาไทยโดยตรง

### ฐานข้อมูล
- **SQL Server 2025 Express** (ฟรี): จำกัด 1 socket/4 core, buffer pool 1,410 MB, **ขนาดฐานข้อมูลสูงสุด 50 GB**; **ไม่มี SQL Server Agent** (ตั้ง backup อัตโนมัติต้องใช้ Task Scheduler แทน); Standard ไม่มีข้อจำกัดขนาด DB (524 PB) ([SQL Server 2025 editions](https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2025?view=sql-server-ver17)) — ข้อมูลโรงงานขนาดนี้น่าจะไม่ถึง 50 GB (ประมาณการ ยังไม่ได้วัดขนาด .accdb จริง)
- ตั้งแต่ SQL Server 2025 **Reporting Services (SSRS) ถูกรวมเข้า Power BI Report Server** (แหล่งเดียวกัน) — ถ้าคิดจะใช้ SSRS ทำรายงานต้องดูเงื่อนไข license ของ PBIRS แยก
- **PostgreSQL**: ติดตั้งด้วย installer ของ EDB (GUI หรือ silent; มี pgAdmin) ทดสอบบน Windows Server 2025/2022 สำหรับ v18 และ 2022/2019 สำหรับ v17 ([PostgreSQL Windows installers](https://www.postgresql.org/download/windows/))

## 3. การอ่านไฟล์ .accdb เพื่อย้ายข้อมูล

**ข้อค้นพบสำคัญ:**
- **Access Database Engine 2016 Redistributable หมด support แล้วเมื่อ 14 ต.ค. 2025**; Microsoft แนะนำ **Microsoft 365 Access Runtime** แทน และหน้าเดียวกันเตือนว่า engine นี้ไม่ได้มีไว้ใช้ใน server-side / service / ASP.NET / scheduled task ที่ไม่มีผู้ใช้ล็อกอิน ([Access Database Engine 2016 Redistributable](https://www.microsoft.com/en-us/download/details.aspx?id=54920))
- Microsoft 365 Access Runtime ฟรี มีทั้ง 32/64-bit และ "ODBC and OLEDB drivers are installed for application developers" แต่ **ไม่เข้ากับ Office ที่ติดตั้งแบบ Windows Installer (MSI)** ([Download and install Microsoft 365 Access Runtime](https://support.microsoft.com/en-us/office/download-and-install-microsoft-365-access-runtime-185c5a32-8ba9-491e-ac76-91cbe3ea09c9))
- Microsoft ทำ Access ODBC driver **เฉพาะ Windows** และ bitness ของโปรแกรมต้องตรงกับ driver (32/64) ([pyodbc wiki: Connecting to Microsoft Access](https://github.com/mkleehammer/pyodbc/wiki/Connecting-to-Microsoft-Access))

ผลต่อการเทียบ stack: เพราะ Microsoft ไม่สนับสนุนการใช้ ACE ฝั่ง server การอ่าน .accdb จึงควรเป็น **เครื่องมือย้ายข้อมูลแยก รันครั้งเดียว/รันซ้ำบน PC** ไม่ใช่ส่วนหนึ่งของเว็บแอป (ข้อสรุปจากคำเตือนข้างต้น) → **การอ่าน .accdb แทบไม่ใช่ตัวแยก stack** เพราะสคริปต์ย้ายเขียนด้วยภาษาอื่นจากตัวเว็บได้

| วิธีอ่าน .accdb | ภาษา | ต้องมี ACE driver? | หมายเหตุ | แหล่ง |
|---|---|---|---|---|
| `System.Data.OleDb` / `System.Data.Odbc` + ACE | C#/.NET | ใช่ (Windows เท่านั้น) | OleDb อยู่ใน API ของ .NET 10/11 (moniker `windowsdesktop`) | [System.Data.OleDb](https://learn.microsoft.com/en-us/dotnet/api/system.data.oledb) |
| `pyodbc` + ACE | Python | ใช่ | driver `Microsoft Access Driver (*.mdb, *.accdb)`; bitness ต้องตรง | [pyodbc wiki](https://github.com/mkleehammer/pyodbc/wiki/Connecting-to-Microsoft-Access) |
| `odbc` (node-odbc) + ACE | Node.js | ใช่ | native addon รองรับเฉพาะ Node LTS; README มีขั้นตอนติดตั้งแค่ Linux/macOS/FreeBSD/IBM i — Windows ไม่มีคู่มือ | [node-odbc](https://github.com/IBM/node-odbc) |
| Jackcess | Java (JVM) | ไม่ | pure Java, Access 2000–2019, Apache 2.0, Java 11+ | [Jackcess](https://jackcess.sourceforge.io/) |
| mdbtools (`mdb-export`, `mdb-schema`) | CLI/C | ไม่ | export CSV/SQL, เน้น *nix; บน Windows ต้อง build เอง | [mdbtools](https://github.com/mdbtools/mdbtools) |

ทุก stack ทำได้ — .NET และ Python มีเส้นทางที่เอกสารชัดที่สุด; Node ใช้ได้แต่เอกสาร Windows บาง; หรือเลี่ยง ACE ทั้งหมดด้วย mdbtools/Jackcess แล้ว import CSV/SQL

## 4. ความเร็วทำ CRUD + รายงาน (เป้าหมาย 1 เดือน)

| ด้าน | ASP.NET Core | Node/TypeScript | Django |
|---|---|---|---|
| CRUD สำเร็จรูป | Razor Pages/MVC + EF Core scaffolding (ไม่ได้ตรวจแหล่งในรอบนี้) | ไม่มี admin/scaffold มาตรฐานในตัว — ประกอบเองจาก framework + ORM | **Django admin** สร้างหน้าจัดการข้อมูลจาก model อัตโนมัติ — เอกสารระบุว่า "recommended use is limited to an organization's internal management tool" และถ้าต้องการ flow ตามกระบวนงานให้เขียน view เอง ([Django admin](https://docs.djangoproject.com/en/5.2/ref/contrib/admin/)) |
| auth user/password | ASP.NET Core Identity ในตัว (ไม่ได้ตรวจแหล่ง) | ประกอบเอง/ไลบรารี | `django.contrib.auth` ในตัว (ไม่ได้ตรวจแหล่ง) |
| PDF รายงานภาษาไทยตรงรูปแบบ | เช่น QuestPDF — Community license ฟรีถ้ารายได้ < USD 1M/ปี ไม่งั้นต้องซื้อ Professional/Enterprise ([QuestPDF community license](https://www.questpdf.com/license/community.html)) — ไม่มีปัญหางบ | HTML→PDF ผ่าน headless browser (ไม่ได้ตรวจแหล่ง) | เช่น WeasyPrint — บน Windows ต้องติดตั้ง Pango ผ่าน MSYS2 หรือใช้ executable สำเร็จรูป (antivirus มักแจ้งเตือน) ([WeasyPrint first steps](https://doc.courtbouillon.org/weasyprint/stable/first_steps.html)) |

ภาษาไทยใน PDF: ทุกตัวต้อง embed ฟอนต์ไทย (เช่น TH Sarabun) และทดสอบการตัดคำ/สระลอย — ยังไม่ได้ทดสอบจริง แนะนำ prototype รายงาน 1 ใบ (เช่น คร.2) ในตั๋ว prototype

## 5. ตารางเทียบสรุป

| เกณฑ์ | ASP.NET Core 10 + SQL Server 2025 | Node/TS (v24/26) + PostgreSQL | Django 5.2 + PostgreSQL |
|---|---|---|---|
| Host บน Windows Server | **ดีที่สุด** — IIS (Hosting Bundle) หรือ Windows Service มีเอกสาร Microsoft ทางการ | ทำได้ผ่าน HttpPlatformHandler (เอกสารเก่า 2015) หรือ service wrapper third-party | ทำได้ด้วย Waitress + IIS/service wrapper; **Gunicorn ใช้ไม่ได้**, เอกสาร Django ไม่ครอบคลุม Windows |
| ฐานข้อมูลบน Windows | SQL Server Express ฟรี (≤50 GB, ไม่มี Agent) หรือ Standard (มี license) | PostgreSQL installer EDB, ฟรี ไม่จำกัดขนาด | เหมือน Node (หรือใช้ SQL Server ผ่าน third-party backend — ไม่ได้ตรวจ) |
| ความเร็ว CRUD 1 เดือน | ดี (scaffolding + Identity) | ปานกลาง — ต้องเลือกประกอบชิ้นส่วนเอง; Prisma กำลังเปลี่ยนรุ่น | **สูง** สำหรับหน้าจัดการข้อมูล (admin อัตโนมัติ) แต่จอ flow หลักยังต้องเขียน view |
| รายงาน PDF | ไลบรารี .NET native (QuestPDF ฯลฯ) | HTML→PDF | WeasyPrint (ติดตั้งบน Windows ยุ่งกว่า) |
| อ่าน .accdb | OleDb/Odbc + Access Runtime (เอกสารชัด) | node-odbc (เอกสาร Windows บาง) | pyodbc + Access Runtime (wiki ชัด) |
| LTS | .NET 10 ถึง พ.ย. 2028 | v24 ถึง เม.ย. 2028 / v26 ถึง เม.ย. 2029 | 5.2 ถึง เม.ย. 2028 (6.2 LTS ถึง 2030) |
| Ecosystem/ดูแลยาว | ผู้ผลิตเดียว (Microsoft) คุมทั้ง runtime/web/DB/OS | กว้างที่สุดแต่กระจัดกระจาย ต้องเลือก/ติดตามหลายไลบรารี | framework "batteries included" ดูแลง่าย; deploy บน Windows เป็นจุดอ่อน |

## 6. Trade-off หลักที่ตั๋ว "เลือก stack" ต้องชั่ง

1. **Host บน Windows** ชัดเจนที่สุดฝั่ง .NET; Python/Node ต้องพึ่งชิ้นส่วนที่ผู้ผลิต framework ไม่ได้รับรองบน Windows
2. **ความเร็วทำหน้าจัดการข้อมูล** Django admin ได้เปรียบชัด แต่ต้องยอมรับ caveat ของเอกสาร (เหมาะกับเครื่องมือภายใน — ตรงกับกรณีนี้ — และจอ flow ต้องเขียนเอง)
3. **การอ่าน .accdb ไม่ควรตัดสิน stack** — ACE ไม่ควรรันใน server; ทำเป็นเครื่องมือย้ายข้อมูลแยกบน PC ภาษาใดก็ได้
4. **SQL Server Express vs PostgreSQL** — Express ฟรีแต่จำกัด 50 GB/ไม่มี Agent; ไม่มีปัญหางบจึงซื้อ Standard ได้; PostgreSQL ไม่มีเพดานแต่ทีม IT โรงงานอาจคุ้นเครื่องมือ Microsoft มากกว่า (สมมติฐาน — ควรถามผู้ใช้)
5. **รายงานภาษาไทยตรงรูปแบบ** เป็นความเสี่ยงร่วมของทุก stack — ควร prototype ก่อนผูกมัด

## คำถามที่ยังเปิด (ส่งต่อตั๋ว grilling)

- Windows Server เวอร์ชันอะไร มี IIS อยู่แล้วหรือไม่ ใครดูแลเครื่อง/backup
- ในโรงงานมีใครดูแลต่อหลังโปรแกรมเมอร์คนปัจจุบันหรือไม่ (ผลต่อการเลือกภาษาที่หาคนได้)
- ขนาดไฟล์ .accdb จริงของทั้ง 5 โปรแกรม (เทียบเพดาน 50 GB ของ Express)
- Office บนเครื่องที่จะรันสคริปต์ย้ายข้อมูลติดตั้งแบบ Click-to-Run หรือ MSI (ผลต่อ Access Runtime)

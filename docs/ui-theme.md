# หน้าตาของแอป (theme)

หน้าตาทั้งแอปอยู่ใน 3 ที่ — หน้า `.razor` รายหน้าไม่ต้องใส่สีหรือฟอนต์เอง

| ที่ | ใช้ทำอะไร |
|---|---|
| `src/Lockte.Web/Components/Layout/LockteTheme.cs` | MudTheme: สี (palette) ฟอนต์และขนาดตัวอักษร (typography) มุมโค้ง |
| `src/Lockte.Web/wwwroot/app.css` | ค่ากลาง (`--lk-*` ที่หัวไฟล์) + ปรับ MudBlazor: ตาราง ปุ่ม ช่องกรอก alert เมนู หน้า login |
| `src/Lockte.Web/wwwroot/fonts/` | ฟอนต์ Sarabun น้ำหนัก 400/500/700 (SIL OFL, ไฟล์สัญญา `OFL.txt`) ฝังในแอป ไม่โหลดจากอินเทอร์เน็ต |

## แก้ภายหลัง

- เปลี่ยนสีหลัก: แก้ `Primary` ใน `LockteTheme.cs` และ `--lk-red` / `--lk-red-dark` / `--lk-red-tint` ใน `app.css` ให้ตรงกัน
- สีตัวอักษรบนพื้นสี ต้องได้ความต่างอย่างน้อย 4.5:1 (WCAG AA) ขอบช่องกรอกอย่างน้อย 3:1 — สีหลัก `#C62828` กับตัวอักษรขาวได้ 5.6:1 (สีแดง `#FF0000` ของโลโก้ได้ราว 4:1 จึงใช้เป็นพื้นของข้อความไม่ได้)
- เพิ่มน้ำหนักฟอนต์: ใส่ไฟล์ใน `wwwroot/fonts/` แล้วเพิ่ม `@font-face` ใน `app.css`
- กล่อง การ์ด เงา: ปรับ `--lk-radius`, `--lk-shadow`
- หน้าใหม่ใช้ `MudPaper Outlined`, `MudTable`, `MudButton` ตามปกติ ได้หน้าตาเดียวกันเอง
- ห้ามเอา theme นี้ไปใช้กับเอกสารพิมพ์/PDF (`Reports/`) — ใช้ฟอนต์ TH Sarabun New และวางตำแหน่งตามแบบเดิม

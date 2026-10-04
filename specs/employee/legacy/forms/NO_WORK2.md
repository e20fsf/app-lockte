# ฟอร์ม: NO_WORK2

- เรียกจาก: ฟอร์ม [NO_WORK](<../forms/NO_WORK.md>) (VBA); มาโคร [บันทึกไม่มาทำงาน1](<../macros.md#m-บันทึกไม่มาทำงาน1>) (OpenForm)
- สร้าง 2021-05-12 · แก้ล่าสุด 2021-05-12
- RecordSource: `NO_WORK` → [NO_WORK](<../tables.md#t-NO_WORK>)
- DefaultView: Single Form
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน Section `ส่วนรายละเอียด` (สูง 17.90 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label3 | แก้ไขพนักงานที่ไม่มาทำงาน |  |  | 6.29,0.00 |
| Label | Label0 | ระบุวันที่ที่ตัองการแก้ไข |  |  | 5.00,1.00 |
| TextBox | DATE1 |  | Format: `Short Date` |  | 8.80,1.00 |
| Subform | ลูก1 |  | SourceObject: `Form.NO_WORK1`; LinkChildFields: `date1`; LinkMasterFields: `date1` |  | 2.30,2.10 |
| CommandButton | คำสั่ง5 | กลับเมนูเดิม |  | OnClick→NO_WORK | 11.80,10.40 |
| Label | Label6 | ต้องการลบรายการใดให้ลบรหัสพนักงานออก |  |  | 3.10,10.60 |

## VBA (Code-behind)

```vb
Attribute VB_GlobalNameSpace = False

Attribute VB_Creatable = True

Attribute VB_PredeclaredId = True

Attribute VB_Exposed = False

Option Compare Database

Option Explicit
```

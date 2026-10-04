# ฟอร์ม: ตั้งวันที่ตัดWEEK1

- เรียกจาก: มาโคร [คำนวณเวลาทำงาน1](<../macros.md#m-คำนวณเวลาทำงาน1>) (OpenForm); เมนู 9.2 ค่าแรงงานรับจ้าง (Switchboard)
- สร้าง 2021-05-12 · แก้ล่าสุด 2021-05-12
- RecordSource: `ตั้งวันที่ตัดWEEK` → [ตั้งวันที่ตัดWEEK](<../tables.md#t-ตั้งวันที่ตัดWEEK>)
- Caption: ตั้งวันที่ตัดWEEK1
- DefaultView: Single Form
- AllowAdditions: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน Section `ส่วนรายละเอียด` (สูง 13.50 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label6 | พิมพ์สรุปค่าแรงงานรับจ้าง |  |  | 6.10,1.10 |
| Label | FROM_DA Label |               ตั้งแต่วันที่ |  |  | 7.50,3.15 |
| TextBox | FROM_DA | FROM_DA | ControlSource: `FROM_DA`; Format: `Short Date` |  | 10.35,3.30 |
| Label | TO_DA Label |                    ถึงวันที่ |  |  | 7.50,3.85 |
| TextBox | TO_DA | TO_DA | ControlSource: `TO_DA`; Format: `Short Date` |  | 10.35,4.00 |
| Label | PAY_D Label |       วันที่จ่ายค่าแรง |  |  | 7.50,4.55 |
| TextBox | PAY_D | PAY_D | ControlSource: `PAY_D`; Format: `Short Date` |  | 10.35,4.71 |
| Label | Label12 | ลูกค้า |  |  | 9.30,5.20 |
| ComboBox | CUS | CUS | ControlSource: `CUS`; RowSourceType: `Table/Query`; RowSource: `ชื่อลูกค้า`; ColumnCount: `2`; ColumnWidths: `284;1418` | AfterUpdate→[Event Procedure] | 10.35,5.40 |
| Label | Label15 | ชื่อลูกค้า |  |  | 8.50,6.00 |
| TextBox | NAMES | NAMES | ControlSource: `NAMES` |  | 10.35,6.20 |
| CommandButton | คำสั่ง13 | พิมพ์ |  | OnClick→คำนวณเวลาทำงาน1 | 6.70,7.20 |
| CommandButton | คำสั่ง7 | กลับเมนูเดิม |  | OnClick→[Event Procedure] | 11.50,7.20 |

## ส่วน FormFooter `FormFooter` (สูง 0.00 ซม.)
(ไม่มี control)

## VBA (Code-behind)

```vb
Attribute VB_GlobalNameSpace = False

Attribute VB_Creatable = True

Attribute VB_PredeclaredId = True

Attribute VB_Exposed = False

Option Compare Database

Option Explicit



Private Sub CUS_AfterUpdate()

Me![NAMES] = Me![CUS].Column(1)

End Sub





Private Sub คำสั่ง7_Click()

On Error GoTo Err_คำสั่ง7_Click





    DoCmd.Close



Exit_คำสั่ง7_Click:

    Exit Sub



Err_คำสั่ง7_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง7_Click

    

End Sub
```

# ฟอร์ม: HOLIDAY

- เรียกจาก: เมนู 7.4 เพิ่มและแก้ไขวันหยุด (Switchboard)
- สร้าง 2021-05-12 · แก้ล่าสุด 2025-12-13
- RecordSource: `HOLIDAY` → [HOLIDAY](<../tables.md#t-HOLIDAY>)
- OrderBy: [date]
- OrderByOn: NotDefault
- Caption: HOLIDAY
- RecordSelectors: NotDefault
- ScrollBars: 2

## ส่วน FormHeader `FormHeader` (สูง 1.82 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label5 | เพิ่มและแก้ไขวันหยุดประจำปี |  |  | 5.30,0.00 |
| Label | DATE Label | วันที่ |  |  | 5.50,1.40 |
| Label | ITEM Label | ชื่อวันหยุด |  |  | 7.29,1.40 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.60 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | DATE | DATE | ControlSource: `DATE`; Format: `Short Date` |  | 5.50,-0.01 |
| TextBox | ITEM | ITEM | ControlSource: `ITEM` |  | 7.29,-0.01 |

## ส่วน FormFooter `FormFooter` (สูง 1.90 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| CommandButton | คำสั่ง4 | กลับเมนูเดิม |  | OnClick→วันหยุดประจำปี | 9.80,0.20 |

## VBA (Code-behind)

```vb
Attribute VB_GlobalNameSpace = False

Attribute VB_Creatable = True

Attribute VB_PredeclaredId = True

Attribute VB_Exposed = False

Option Compare Database

Option Explicit



Private Sub คำสั่ง4_Click()

On Error GoTo Err_คำสั่ง4_Click





    DoCmd.Close



Exit_คำสั่ง4_Click:

    Exit Sub



Err_คำสั่ง4_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง4_Click

    

End Sub
```

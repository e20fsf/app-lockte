# ฟอร์ม: E_WORK subform1

- เรียกจาก: ฟอร์ม [E_WORK](<../forms/E_WORK.md>) (Subform)
- สร้าง 2021-05-12 · แก้ล่าสุด 2024-06-29
- RecordSource: `E_WORK` → [E_WORK](<../tables.md#t-E_WORK>)
- Caption: E_WORK subform1
- DataEntry: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 0.76 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | ID Label | รหัส |  |  | 0.10,0.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.70 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| ComboBox | ID | ID | ControlSource: `ID`; RowSourceType: `Table/Query`; RowSource: `SELECT EMPLO.ID, EMPLO.NAME FROM EMPLO WHERE (((EMPLO.RESIGN) Is Null)) ORDER BY EMPLO.ID; `; ColumnCount: `2`; ColumnWidths: `567;2835` | AfterUpdate→[Event Procedure] | 0.10,-0.01 |

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



Private Sub ID_AfterUpdate()

ID = UCase([ID])

End Sub
```

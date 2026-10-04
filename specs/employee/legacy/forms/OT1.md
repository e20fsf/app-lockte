# ฟอร์ม: OT1

- เรียกจาก: ฟอร์ม [OT](<../forms/OT.md>) (Subform)
- สร้าง 2021-05-12 · แก้ล่าสุด 2025-11-18
- RecordSource: `OT` → [OT](<../tables.md#t-OT>)
- Filter: ((OT.MEMO="ช่วยแผนกประกอบ"))
- OrderBy: OT.MEMO
- OrderByOn: NotDefault
- Caption: OT
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 2

## ส่วน FormHeader `FormHeader` (สูง 0.71 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | ID Label | รหัส |  |  | 0.00,0.10 |
| Label | NAME Label | ชื่อ |  |  | 1.50,0.10 |
| Label | MEMO Label | เหตุผล |  |  | 7.51,0.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.80 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| ComboBox | ID | ID | ControlSource: `ID`; RowSourceType: `Table/Query`; RowSource: `SELECT EMPLO.ID, EMPLO.NAME, EMPLO.WORK, EMPLO.SHIFT, EMPLO.RESIGN FROM EMPLO WHERE (((EMPLO.RESIGN) Is Null)) ORDER BY EMPLO.ID; `; ColumnCount: `4`; ColumnWidths: `855;3402;3402;851` | AfterUpdate→[Event Procedure] | 0.00,0.00 |
| TextBox | NAME | NAME | ControlSource: `NAME` |  | 1.50,0.00 |
| TextBox | MEMO | MEMO | ControlSource: `MEMO` |  | 7.40,0.00 |

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

   Me![NAME] = Me![ID].Column(1)

   Me![MEMO] = Me![ID].Column(2)

End Sub
```

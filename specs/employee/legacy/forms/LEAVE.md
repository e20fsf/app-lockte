# ฟอร์ม: LEAVE

- เรียกจาก: ฟอร์ม [บันทึกการลางาน](<../forms/บันทึกการลางาน.md>) (Subform)
- สร้าง 2021-05-12 · แก้ล่าสุด 2024-08-01
- RecordSource: `LEAVE` → [LEAVE](<../tables.md#t-LEAVE>)
- Caption: LEAVE subform
- DataEntry: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 0.76 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | FROM_T Label | ตั้งแต่เวลา |  |  | 6.59,0.10 |
| Label | REMARK Label | เหตุผล |  |  | 13.37,0.10 |
| Label | Label22 | ค่าแรง/วัน |  |  | 17.52,0.10 |
| Label | DATE Label | วันที่ |  |  | 0.10,0.10 |
| Label | ID Label | รหัส |  |  | 1.80,0.10 |
| Label | FROM_D Label | ตั้งแต่วันที่ |  |  | 3.10,0.10 |
| Label | TO_D Label | ถึงวันที่ |  |  | 5.00,0.10 |
| Label | TO_T Label | ถึงเวลา |  |  | 8.09,0.10 |
| Label | SUM_D Label | จำนวนวัน |  |  | 9.18,0.10 |
| Label | SR Label | ประเภท |  |  | 10.63,0.10 |
| Label | SAL Label | ได้ค่าแรง |  |  | 11.92,0.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.56 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | DATE | DATE | ControlSource: `DATE`; DefaultValue: `Date()`; Format: `Short Date` |  | 0.00,0.00 |
| ComboBox | ID | ID | ControlSource: `ID`; RowSourceType: `Table/Query`; RowSource: `SELECT EMPLO.ID, EMPLO.NAME, EMPLO.SA_DAY FROM EMPLO ORDER BY EMPLO.NAME; `; ColumnCount: `3`; ColumnWidths: `567;2835;851` | AfterUpdate→[Event Procedure] | 1.80,0.00 |
| TextBox | FROM_D | FROM_D | ControlSource: `FROM_D`; Format: `Short Date` |  | 3.10,0.00 |
| TextBox | TO_D | TO_D | ControlSource: `TO_D`; Format: `Short Date` |  | 4.89,0.00 |
| TextBox | FROM_T | FROM_T | ControlSource: `FROM_T`; Format: `Short Time` |  | 6.69,0.00 |
| TextBox | TO_T | TO_T | ControlSource: `TO_T`; Format: `Short Time` |  | 8.09,0.00 |
| TextBox | SUM_D | SUM_D | ControlSource: `SUM_D`; Format: `Standard` |  | 9.07,0.00 |
| ComboBox | SR | SR | ControlSource: `SR`; RowSourceType: `Table/Query`; RowSource: `SELECT ประเภทการลางาน.ID, ประเภทการลางาน.DISCRIPTION, * FROM ประเภทการลางาน; `; ColumnCount: `2`; ColumnWidths: `285;2835` |  | 10.67,0.00 |
| ComboBox | SAL | SAL | ControlSource: `SAL`; RowSourceType: `Table/Query`; RowSource: `SELECT [ได้ค่าแรง].IDD, [ได้ค่าแรง].NAMED FROM ได้ค่าแรง; `; ColumnCount: `2`; ColumnWidths: `288;1134` |  | 11.97,0.00 |
| TextBox | REMARK | REMARK | ControlSource: `REMARK` |  | 13.47,0.00 |
| TextBox | SALA_DAY | SALA_DAY | ControlSource: `SALA_DAY`; Format: `Standard` |  | 17.50,0.00 |

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

Me![SALA_DAY] = Me![ID].Column(2)









End Sub
```

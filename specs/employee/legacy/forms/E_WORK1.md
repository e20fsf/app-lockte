# ฟอร์ม: E_WORK1

- เรียกจาก: ฟอร์ม [EMPLO3](<../forms/EMPLO3.md>) (Subform)
- สร้าง 2021-05-12 · แก้ล่าสุด 2021-05-12
- RecordSource: `E_WORK` → [E_WORK](<../tables.md#t-E_WORK>)
- OrderBy: date, work_t, rest_t
- OrderByOn: NotDefault
- Caption: E_WORK
- AllowAdditions: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 2

## ส่วน FormHeader `FormHeader` (สูง 0.62 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | LATE Label | เวลาสาย |  |  | 9.50,0.10 |
| Label | DATE Label | วันที่ |  |  | 0.10,0.10 |
| Label | ID Label | รหัส |  |  | 1.90,0.10 |
| Label | W_IN Label | เวลาเข้า |  |  | 2.81,0.10 |
| Label | W_OUT Label | เวลาออก |  |  | 4.00,0.10 |
| Label | REST_T Label | เวลาพัก |  |  | 5.40,0.10 |
| Label | WORK_T Label | เวลาทำงาน |  |  | 6.83,0.10 |
| Label | TYPE Label | ประเภท |  |  | 8.49,0.10 |
| Label | MARK Label | ลูกค้า |  |  | 11.00,0.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.48 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | DATE | DATE | ControlSource: `DATE`; Format: `Short Date` |  | 0.10,0.00 |
| TextBox | ID | ID | ControlSource: `ID` |  | 1.90,0.00 |
| TextBox | W_IN | W_IN | ControlSource: `W_IN`; Format: `Short Time` |  | 2.80,0.00 |
| TextBox | W_OUT | W_OUT | ControlSource: `W_OUT`; Format: `Short Time` |  | 4.00,0.00 |
| TextBox | REST_T | REST_T | ControlSource: `REST_T` |  | 5.40,0.00 |
| TextBox | WORK_T | WORK_T | ControlSource: `WORK_T`; Format: `Standard`; DecimalPlaces: `2` |  | 6.80,0.00 |
| TextBox | TYPE | TYPE | ControlSource: `TYPE` |  | 8.49,0.00 |
| TextBox | LATE | LATE | ControlSource: `LATE`; Format: `Short Time` |  | 9.70,0.00 |
| ComboBox | MARK | MARK | ControlSource: `MARK`; RowSourceType: `Table/Query`; RowSource: `ชื่อลูกค้า`; ColumnCount: `2`; ColumnWidths: `284;2268` |  | 11.00,0.00 |

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
```

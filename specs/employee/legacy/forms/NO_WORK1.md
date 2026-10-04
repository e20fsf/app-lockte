# ฟอร์ม: NO_WORK1

- เรียกจาก: ฟอร์ม [NO_WORK2](<../forms/NO_WORK2.md>) (Subform)
- สร้าง 2021-05-12 · แก้ล่าสุด 2024-06-29
- RecordSource: `NO_WORK` → [NO_WORK](<../tables.md#t-NO_WORK>)
- Filter: คน
- Caption: NO_WORK1
- AllowAdditions: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 2

## ส่วน FormHeader `FormHeader` (สูง 0.62 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | ID1 Label | รหัส |  |  | 1.90,0.10 |
| Label | W_IN Label | เวลาเข้า |  |  | 3.52,0.10 |
| Label | W_OUT Label | เวลาออก |  |  | 4.82,0.10 |
| Label | LATE Label | สาย |  |  | 8.62,0.10 |
| Label | REMARK Label | เหตุผล |  |  | 9.32,0.10 |
| Label | DATE1 Label | วันที่ |  |  | 0.10,0.10 |
| Label | WORK_T Label | จำนวน |  |  | 6.22,0.10 |
| Label | TYPE Label | ประเภท |  |  | 7.35,0.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.60 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| ComboBox | ID1 | ID1 | ControlSource: `ID1`; RowSourceType: `Table/Query`; RowSource: `EMPLO`; ColumnCount: `2`; ColumnWidths: `567;3402` |  | 1.91,0.10 |
| TextBox | W_IN | W_IN | ControlSource: `W_IN`; Format: `Short Time` |  | 3.50,0.10 |
| TextBox | W_OUT | W_OUT | ControlSource: `W_OUT`; Format: `Short Time` |  | 4.83,0.10 |
| TextBox | LATE | LATE | ControlSource: `LATE` |  | 8.62,0.10 |
| TextBox | DATE1 | DATE1 | ControlSource: `DATE1`; Format: `Short Date` |  | 0.10,0.10 |
| TextBox | WORK_T | WORK_T | ControlSource: `WORK_T` |  | 6.22,0.10 |
| TextBox | TYPE | TYPE | ControlSource: `TYPE` |  | 7.35,0.10 |
| TextBox | REMARK | REMARK | ControlSource: `REMARK` |  | 9.37,0.10 |

## ส่วน FormFooter `FormFooter` (สูง 1.16 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label17 | รวมทั้งหมด |  |  | 2.20,0.10 |
| TextBox | Text16 | =Count([id1]) | ControlSource: `=Count([id1])` |  | 4.10,0.10 |
| Label | Label19 | คน |  |  | 5.40,0.10 |

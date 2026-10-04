# ฟอร์ม: E_WORK subform2

- เรียกจาก: ฟอร์ม [NO_WORK](<../forms/NO_WORK.md>) (Subform)
- สร้าง 2021-05-12 · แก้ล่าสุด 2024-06-29
- RecordSource: `NO_WORK` → [NO_WORK](<../tables.md#t-NO_WORK>)
- OrderBy: NO_WORK.DATE, NO_WORK.ID, NO_WORK.W_IN, NO_WORK.W_OUT, NO_WORK.WORK_T, NO_WORK.TYPE, NO_WORK.LATE
- OrderByOn: NotDefault
- Caption: E_WORK subform2
- DataEntry: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 0.76 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label3 | เหตุผล |  |  | 1.50,0.10 |
| Label | ID Label | รหัส |  |  | 0.10,0.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.63 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| ComboBox | ID | ID1 | ControlSource: `ID1`; RowSourceType: `Table/Query`; RowSource: `SELECT EMPLO.ID, EMPLO.NAME FROM EMPLO WHERE (((EMPLO.RESIGN) Is Null)) ORDER BY EMPLO.ID; `; ColumnCount: `2`; ColumnWidths: `1134;3402` |  | 0.10,-0.01 |
| ComboBox | REMARK | REMARK | ControlSource: `REMARK`; RowSourceType: `Table/Query`; RowSource: `SELECT ประเภทการลางาน.DISCRIPTION, * FROM ประเภทการลางาน; ` |  | 1.51,0.00 |

## ส่วน FormFooter `FormFooter` (สูง 0.00 ซม.)
(ไม่มี control)

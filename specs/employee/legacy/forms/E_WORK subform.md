# ฟอร์ม: E_WORK subform

- เรียกจาก: — (ไม่มีที่เรียกใช้)
- สร้าง 2021-05-12 · แก้ล่าสุด 2021-05-12
- RecordSource: `SELECT DISTINCTROW E_WORK.DATE, E_WORK.ID, E_WORK.W_IN, E_WORK.W_OUT, E_WORK.WORK_T, E_WORK.TYPE, E_WORK.LATE, E_WORK.REST_T FROM E_WORK; ` → [E_WORK](<../tables.md#t-E_WORK>)
- OrderBy: DATE
- OrderByOn: NotDefault
- Caption: E_WORK subform
- DefaultView: Datasheet
- AllowAdditions: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 0.76 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | DATE Label | DATE |  |  | 0.00,0.10 |
| Label | ID Label | ID |  |  | 1.85,0.10 |
| Label | W_IN Label | W_IN |  |  | 3.06,0.10 |
| Label | W_OUT Label | W_OUT |  |  | 4.43,0.10 |
| Label | WORK_T Label | WORK_T |  |  | 8.18,0.10 |
| Label | TYPE Label | TYPE |  |  | 10.82,0.10 |
| Label | LATE Label | LATE |  |  | 11.85,0.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.68 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | REST_T | REST_T | ControlSource: `REST_T` |  | 5.80,0.10 |
| TextBox | DATE | DATE | ControlSource: `DATE`; Format: `Short Date` |  | 0.00,0.10 |
| TextBox | ID | ID | ControlSource: `ID` |  | 1.85,0.10 |
| TextBox | W_IN | W_IN | ControlSource: `W_IN`; Format: `Short Time` |  | 3.06,0.10 |
| TextBox | W_OUT | W_OUT | ControlSource: `W_OUT`; Format: `Short Time` |  | 4.43,0.10 |
| TextBox | WORK_T | WORK_T | ControlSource: `WORK_T`; Format: `Standard` |  | 8.18,0.10 |
| TextBox | TYPE | TYPE | ControlSource: `TYPE` |  | 10.82,0.10 |
| TextBox | LATE | LATE | ControlSource: `LATE` |  | 11.85,0.10 |

## ส่วน FormFooter `FormFooter` (สูง 0.00 ซม.)
(ไม่มี control)

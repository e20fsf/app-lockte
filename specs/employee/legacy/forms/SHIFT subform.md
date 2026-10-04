# ฟอร์ม: SHIFT subform

- เรียกจาก: ฟอร์ม [วันทำงาน](<../forms/วันทำงาน.md>) (Subform)
- สร้าง 2021-05-12 · แก้ล่าสุด 2024-06-14
- RecordSource: `SELECT DISTINCTROW SHIFT.SHIFT, SHIFT.W_IN, SHIFT.W_OUT, SHIFT.STOP, SHIFT.START, SHIFT.REST_T, SHIFT.O_IN, SHIFT.O_OUT FROM SHIFT; ` → [SHIFT](<../tables.md#t-SHIFT>)
- Caption: SHIFT subform
- DefaultView: Datasheet
- AllowAdditions: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 0.76 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label15 | เวลาพัก |  |  | 6.80,0.10 |
| Label | O_IN Label | O_IN |  |  | 8.00,0.10 |
| Label | O_OUT Label | O_OUT |  |  | 9.37,0.10 |
| Label | SHIFT Label | SHIFT |  |  | 0.10,0.10 |
| Label | W_IN Label | W_IN |  |  | 1.18,0.10 |
| Label | W_OUT Label | W_OUT |  |  | 2.55,0.10 |
| Label | STOP Label | STOP |  |  | 3.92,0.10 |
| Label | START Label | START |  |  | 5.29,0.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.75 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | O_IN | O_IN | ControlSource: `O_IN`; Format: `Short Time` |  | 8.00,0.10 |
| TextBox | O_OUT | O_OUT | ControlSource: `O_OUT`; Format: `Short Time` |  | 9.37,0.10 |
| TextBox | SHIFT | SHIFT | ControlSource: `SHIFT` |  | 0.10,0.10 |
| TextBox | W_IN | W_IN | ControlSource: `W_IN`; Format: `Short Time` |  | 1.18,0.10 |
| TextBox | W_OUT | W_OUT | ControlSource: `W_OUT`; Format: `Short Time` |  | 2.55,0.10 |
| TextBox | STOP | STOP | ControlSource: `STOP`; Format: `Short Time` |  | 3.92,0.10 |
| TextBox | START | START | ControlSource: `START`; Format: `Short Time` |  | 5.29,0.10 |
| TextBox | REST_T | REST_T | ControlSource: `REST_T` |  | 6.70,0.10 |

## ส่วน FormFooter `FormFooter` (สูง 0.00 ซม.)
(ไม่มี control)

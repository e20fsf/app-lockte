# ฟอร์ม: LEAVE1

- เรียกจาก: ฟอร์ม [LEAVE_A](<../forms/LEAVE_A.md>) (Subform); ฟอร์ม [บันทึกการลางาน](<../forms/บันทึกการลางาน.md>) (Subform)
- สร้าง 2021-05-12 · แก้ล่าสุด 2023-08-25
- RecordSource: `LEAVE` → [LEAVE](<../tables.md#t-LEAVE>)
- OrderBy: LEAVE.FROM_D, id
- OrderByOn: NotDefault
- Caption: LEAVE1
- AllowAdditions: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 2

## ส่วน FormHeader `FormHeader` (สูง 0.76 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | REMARK Label | เหตุผล |  |  | 11.20,0.10 |
| Label | Label23 | ค่าแรง/วัน |  |  | 15.00,0.10 |
| Label | Label21 | จ่ายค่าแรง |  |  | 16.70,0.10 |
| Label | ID Label | รหัส |  |  | 0.00,0.10 |
| Label | FROM_D Label | ตั้งแต่วันที่ |  |  | 0.91,0.10 |
| Label | TO_D Label | ถึงวันที่ |  |  | 2.81,0.10 |
| Label | FROM_T Label | ตั้งแต่เวลา |  |  | 4.41,0.10 |
| Label | TO_T Label | ถึงเวลา |  |  | 5.82,0.10 |
| Label | SUM_D Label | จำนวนวัน |  |  | 6.91,0.10 |
| Label | SR Label | ประเภท |  |  | 8.36,0.10 |
| Label | SAL Label | ได้ค่าแรง |  |  | 9.65,0.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.53 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | ID | ID | ControlSource: `ID` |  | 0.01,0.00 |
| TextBox | FROM_D | FROM_D | ControlSource: `FROM_D`; Format: `Short Date` |  | 0.92,0.00 |
| TextBox | TO_D | TO_D | ControlSource: `TO_D`; Format: `Short Date` |  | 2.71,0.00 |
| TextBox | FROM_T | FROM_T | ControlSource: `FROM_T`; Format: `Short Time` |  | 4.49,0.00 |
| TextBox | TO_T | TO_T | ControlSource: `TO_T`; Format: `Short Time` |  | 5.83,0.00 |
| TextBox | SUM_D | SUM_D | ControlSource: `SUM_D`; Format: `Standard` |  | 6.81,0.00 |
| TextBox | SR | SR | ControlSource: `SR` |  | 8.41,0.00 |
| TextBox | SAL | SAL | ControlSource: `SAL` |  | 9.71,0.00 |
| TextBox | REMARK | REMARK | ControlSource: `REMARK` |  | 11.21,0.00 |
| TextBox | SALA_DAY | SALA_DAY | ControlSource: `SALA_DAY`; Format: `Standard` |  | 15.20,0.00 |
| TextBox | PAY | PAY | ControlSource: `PAY`; Format: `Standard`; DecimalPlaces: `2` |  | 16.70,0.00 |

## ส่วน FormFooter `FormFooter` (สูง 0.61 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label25 | รวม |  |  | 0.80,0.00 |
| Label | Label29 | รายการ |  |  | 2.90,0.00 |
| Label | Label30 | วัน |  |  | 8.30,0.00 |
| Label | Label31 | บาท |  |  | 18.10,0.00 |
| TextBox | Text28 | =Count([id]) | ControlSource: `=Count([id])`; Format: `Standard`; DecimalPlaces: `0` |  | 2.10,0.10 |
| TextBox | Text26 | =Sum([pay]) | ControlSource: `=Sum([pay])`; Format: `Standard`; DecimalPlaces: `2` |  | 16.01,0.10 |
| TextBox | Text24 | =Sum([sum_d]) | ControlSource: `=Sum([sum_d])`; Format: `Standard`; DecimalPlaces: `2` |  | 6.90,0.11 |

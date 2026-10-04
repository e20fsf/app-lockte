# ฟอร์ม: LEAVE subform

- เรียกจาก: — (ไม่มีที่เรียกใช้)
- สร้าง 2021-05-12 · แก้ล่าสุด 2021-05-12
- RecordSource: `LEAVE` → [LEAVE](<../tables.md#t-LEAVE>)
- OrderBy: [leave].[id]
- OrderByOn: NotDefault
- Caption: LEAVE subform
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 2

## ส่วน FormHeader `FormHeader` (สูง 0.76 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | REMARK Label | เหตุผล |  |  | 11.56,0.10 |
| Label | DATE Label | วันที่ |  |  | 0.10,0.10 |
| Label | ID Label | รหัส |  |  | 1.80,0.10 |
| Label | FROM_D Label | ตั้งแต่วันที่ |  |  | 2.60,0.10 |
| Label | FROM_T Label | ตั้งแต่เวลา |  |  | 4.40,0.10 |
| Label | TO_D Label | ถึงวันที่ |  |  | 5.40,0.10 |
| Label | TO_T Label | ถึงเวลา |  |  | 7.20,0.10 |
| Label | SUM_D Label | จำนวนวัน |  |  | 8.10,0.10 |
| Label | SR Label | ประเภท |  |  | 9.26,0.10 |
| Label | SAL Label | ได้ค่าแรง |  |  | 10.55,0.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.73 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | ID | ID | ControlSource: `ID` |  | 1.80,0.10 |
| TextBox | FROM_D | FROM_D | ControlSource: `FROM_D`; Format: `Short Date` |  | 2.60,0.10 |
| TextBox | FROM_T | FROM_T | ControlSource: `FROM_T`; Format: `Short Time` |  | 4.40,0.10 |
| TextBox | TO_D | TO_D | ControlSource: `TO_D`; Format: `Short Date` |  | 5.40,0.10 |
| TextBox | TO_T | TO_T | ControlSource: `TO_T`; Format: `Short Time` |  | 7.20,0.10 |
| TextBox | SUM_D | SUM_D | ControlSource: `SUM_D` |  | 8.10,0.10 |
| TextBox | SR | SR | ControlSource: `SR` |  | 9.26,0.10 |
| TextBox | SAL | SAL | ControlSource: `SAL` |  | 10.55,0.10 |
| TextBox | REMARK | REMARK | ControlSource: `REMARK` |  | 11.55,0.10 |
| TextBox | DATE | DATE | ControlSource: `DATE`; Format: `Short Date` |  | 0.10,0.10 |

## ส่วน FormFooter `FormFooter` (สูง 0.00 ซม.)
(ไม่มี control)

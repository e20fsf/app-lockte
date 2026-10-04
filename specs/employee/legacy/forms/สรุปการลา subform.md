# ฟอร์ม: สรุปการลา subform

- เรียกจาก: ฟอร์ม [LEAVE_A](<../forms/LEAVE_A.md>) (Subform); ฟอร์ม [บันทึกการลางาน](<../forms/บันทึกการลางาน.md>) (Subform); มาโคร [ลางานรายคน1](<../macros.md#m-ลางานรายคน1>) (OpenForm)
- สร้าง 2021-05-12 · แก้ล่าสุด 2026-08-21
- RecordSource: `สรุปการลา` → [สรุปการลา](<../queries/ลางานรายคน1.md#q-สรุปการลา>)
- Caption: สรุปการลา subform
- DefaultView: Single Form
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 3.00 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | 2 Label | ลาป่วยมีใบแพทย์ |  |  | 2.10,0.10 |
| Label | 3 Label | ลาป่วยไม่มีใบแพทย์ |  |  | 3.10,0.10 |
| Label | 4 Label | ลาคลอด |  |  | 4.10,0.10 |
| Label | 5 Label | ลาอุบัติเหตุในงาน |  |  | 5.10,0.10 |
| Label | 6 Label | ลาพักร้อน |  |  | 6.10,0.10 |
| Label | 7 Label | ลาอื่นๆ |  |  | 7.10,0.10 |
| Label | Label21 | ขาดงาน |  |  | 8.10,0.10 |
| Label | Label30 | ลากิจฉุกเฉิน |  |  | 9.10,0.10 |
| Label | Label31 | ลาเพื่อระดมพล |  |  | 10.10,0.10 |
| Label | Label32 | ลาเพื่อการศึกษาฝึกอบรมของบริษัท |  |  | 11.10,0.10 |
| Label | Label33 | ลาทำหมัน |  |  | 12.10,0.10 |
| Label | Label34 | ลากิจโดยไม่ได้แจ้งล่วงหน้า |  |  | 13.20,0.10 |
| Label | Label35 | ลากิจเกินกำหนด |  |  | 14.20,0.10 |
| Label | Label37 | ลาเพื่อการศึกษาฝึกอบรมส่วนตัว |  |  | 15.20,0.10 |
| Label | Label16 | รวม |  |  | 16.20,0.10 |
| Label | Label23 | รวมค่าแรง |  |  | 17.40,0.10 |
| Label | ID Label | รหัส |  |  | 0.10,0.10 |
| Label | 1 Label | ลากิจ |  |  | 1.10,0.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 4.20 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | 2 | 2 | ControlSource: `2`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 2.10,0.10 |
| TextBox | 3 | 3 | ControlSource: `3`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 3.10,0.10 |
| TextBox | 4 | 4 | ControlSource: `4`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 4.10,0.10 |
| TextBox | 5 | 5 | ControlSource: `5`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 5.10,0.10 |
| TextBox | 6 | 6 | ControlSource: `6`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 6.10,0.10 |
| TextBox | 7 | 7 | ControlSource: `7`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 7.10,0.10 |
| TextBox | 8 | 8 | ControlSource: `8`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 8.10,0.10 |
| TextBox | 9 | 9 | ControlSource: `9`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 9.10,0.10 |
| TextBox | 10 | 10 | ControlSource: `10`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 10.10,0.10 |
| TextBox | 11 | 11 | ControlSource: `11`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 11.10,0.10 |
| TextBox | 12 | 12 | ControlSource: `12`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 12.10,0.10 |
| TextBox | 13 | 13 | ControlSource: `13`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 13.10,0.10 |
| TextBox | 14 | 14 | ControlSource: `14`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 14.20,0.10 |
| TextBox | 15 | 15 | ControlSource: `15`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 15.20,0.10 |
| TextBox | SumOfPAY | SumOfPAY | ControlSource: `SumOfPAY` |  | 17.40,0.10 |
| TextBox | ID | ID | ControlSource: `ID` |  | 0.10,0.10 |
| TextBox | 1 | 1 | ControlSource: `1`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 1.10,0.10 |
| TextBox | sumofsum_d1 | SumOfSUM_D | ControlSource: `SumOfSUM_D`; Format: `Standard`; DecimalPlaces: `1`; ขยายได้; หดได้ |  | 16.20,0.11 |

## ส่วน FormFooter `FormFooter` (สูง 0.00 ซม.)
(ไม่มี control)

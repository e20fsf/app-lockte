# ฟอร์ม: สรุปการลา subform1

- เรียกจาก: มาโคร [ลางานรายคน1](<../macros.md#m-ลางานรายคน1>) (OpenForm)
- สร้าง 2021-05-12 · แก้ล่าสุด 2024-06-29
- RecordSource: `สรุปการลา6` → [สรุปการลา6](<../queries/ลางานรายคน1.md#q-สรุปการลา6>)
- Caption: สรุปการลา subform1
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
| Label | Label42 | รวมลากิจ |  |  | 19.10,0.10 |
| Label | Label41 | รวมลาป่วย |  |  | 20.00,0.10 |
| Label | ID Label | รหัส |  |  | 0.10,0.10 |
| Label | 1 Label | ลากิจ |  |  | 1.10,0.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 4.20 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | 2 | SumOf2 | ControlSource: `SumOf2`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 2.10,0.10 |
| TextBox | 3 | SumOf3 | ControlSource: `SumOf3`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 3.10,0.10 |
| TextBox | 4 | SumOf4 | ControlSource: `SumOf4`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 4.10,0.10 |
| TextBox | 5 | SumOf5 | ControlSource: `SumOf5`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 5.10,0.10 |
| TextBox | 6 | SumOf6 | ControlSource: `SumOf6`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 6.10,0.10 |
| TextBox | 7 | SumOf7 | ControlSource: `SumOf7`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 7.10,0.10 |
| TextBox | 8 | SumOf8 | ControlSource: `SumOf8`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 8.10,0.10 |
| TextBox | 9 | SumOf9 | ControlSource: `SumOf9`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 9.10,0.10 |
| TextBox | 10 | SumOf10 | ControlSource: `SumOf10`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 10.10,0.10 |
| TextBox | 11 | SumOf11 | ControlSource: `SumOf11`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 11.10,0.10 |
| TextBox | 12 | SumOf12 | ControlSource: `SumOf12`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 12.10,0.10 |
| TextBox | 13 | SumOf13 | ControlSource: `SumOf13`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 13.10,0.10 |
| TextBox | 14 | SumOf14 | ControlSource: `SumOf14`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 14.20,0.10 |
| TextBox | 15 | SumOf15 | ControlSource: `SumOf15`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 15.20,0.10 |
| TextBox | SumOfPAY | SumOfSumOfPAY | ControlSource: `SumOfSumOfPAY`; DefaultValue: `0`; Format: `Standard` |  | 17.40,0.10 |
| TextBox | Expr1 | Expr1 | ControlSource: `Expr1` |  | 19.00,0.10 |
| TextBox | Expr2 | Expr2 | ControlSource: `Expr2` |  | 19.90,0.10 |
| TextBox | ID | ID | ControlSource: `ID` |  | 0.10,0.10 |
| TextBox | 1 | SumOf1 | ControlSource: `SumOf1`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1` |  | 1.10,0.10 |
| TextBox | sumofsum_d1 | SumOfSumOfSUM_D | ControlSource: `SumOfSumOfSUM_D`; DefaultValue: `0`; Format: `Standard`; DecimalPlaces: `1`; ขยายได้; หดได้ |  | 16.20,0.11 |
| TextBox | NAMES | NAMES | ControlSource: `NAMES` |  | 0.10,1.80 |
| Label | Label44 | พักร้อน |  |  | 5.90,1.80 |
| TextBox | Expr3 | Expr3 | ControlSource: `Expr3` |  | 7.00,1.80 |
| Label | Label45 | เพศ |  |  | 8.60,1.80 |
| TextBox | SEX | SEX | ControlSource: `SEX` |  | 9.40,1.80 |
| Label | Label46 | วันที่เริ่มทำงาน |  |  | 10.90,1.80 |
| TextBox | START | START | ControlSource: `START`; Format: `Short Date` |  | 13.19,1.80 |

## ส่วน FormFooter `FormFooter` (สูง 0.00 ซม.)
(ไม่มี control)

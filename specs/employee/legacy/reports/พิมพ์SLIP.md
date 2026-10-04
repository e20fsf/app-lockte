# รายงาน: พิมพ์SLIP

- เรียกจาก: มาโคร [SLIP](<../macros.md#m-SLIP>) (OpenReport); มาโคร [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) (OpenReport); มาโคร [SLIPช่วงที่2](<../macros.md#m-SLIPช่วงที่2>) (OpenReport); มาโคร [SLIPรวมโบนัส](<../macros.md#m-SLIPรวมโบนัส>) (OpenReport)
- สร้าง 2021-05-12 · แก้ล่าสุด 2025-09-05
- RecordSource: `SLIP` → [SLIP](<../tables.md#t-SLIP>)
- DefaultView: Single Form

## การจัดกลุ่ม/เรียงลำดับ

| ลำดับ | ฟิลด์/นิพจน์ | เรียง | Group Header | Group Footer | GroupOn |
|---:|---|---|---|---|---|
| 1 | `ID` | น้อย→มาก |  |  |   |

## ส่วน PageHeader `PageHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน Section `ส่วนรายละเอียด` (สูง 9.50 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| UnboundObjectFrame | OLEUnbound25 |  | ล็อก (แก้ไม่ได้) |  | 0.70,0.00 |
| Label | Label0 | บริษัท ล็อคเต้ จำกัด |  |  | 7.20,0.10 |
| Label | Label78 | 85/36 ซ.สุขาภิบาล2 ถ.พุทธมณฑลสาย5 ต.อ้อมน้อย อ.กระทุ่มแบน จ.สมุทรสาคร โทรฯ024204554 |  |  | 3.10,0.90 |
| Label | Label1 | รายละเอียดการจ่ายค่าแรงงวดวันที่ |  |  | 5.50,1.40 |
| TextBox | DATE | DATE | ControlSource: `DATE`; Format: `General Date` |  | 10.20,1.41 |
| Label | Label8 | วัน |  |  | 8.51,2.10 |
| Label | Label9 | วันอาทิตย์ |  |  | 9.01,2.10 |
| TextBox | SUN_D | SUN_D | ControlSource: `SUN_D` |  | 10.21,2.10 |
| Label | Label10 | วัน |  |  | 10.59,2.10 |
| Label | Label11 | วันหยุด |  |  | 11.19,2.10 |
| TextBox | H_D | H_D | ControlSource: `H_D` |  | 12.19,2.10 |
| Label | Label12 | วัน |  |  | 12.59,2.10 |
| Label | Label13 | วันทำงาน |  |  | 13.19,2.10 |
| TextBox | W_D | W_D | ControlSource: `W_D` |  | 14.39,2.10 |
| Label | Label14 | วัน |  |  | 14.90,2.10 |
| Label | Label3 | ตั้งแต่วันที่ |  |  | 1.30,2.10 |
| TextBox | FROM_D | FROM_D | ControlSource: `FROM_D`; Format: `Short Date` |  | 2.70,2.10 |
| Label | Label5 | ถึงวันที่ |  |  | 4.20,2.10 |
| TextBox | Text4 | TO_D | ControlSource: `TO_D`; Format: `Short Date` |  | 5.20,2.10 |
| Label | Label7 | จำนวน |  |  | 6.70,2.10 |
| TextBox | TT_D | TT_D | ControlSource: `TT_D` |  | 7.70,2.10 |
| Label | Label23 | รหัสพนักงาน |  |  | 1.30,2.70 |
| TextBox | ID | ID | ControlSource: `ID` |  | 3.00,2.70 |
| Label | Label24 | ชื่อ-นามสกุล |  |  | 4.60,2.70 |
| TextBox | NAME | NAME | ControlSource: `NAME` |  | 6.20,2.70 |
| Label | Label68 | ค่าแรงเดือนละหรือวันละ |  |  | 9.80,2.70 |
| TextBox | SA_D | SA_D | ControlSource: `SA_D`; Format: `Standard`; DecimalPlaces: `2` |  | 12.81,2.70 |
| Label | Label69 | บาท |  |  | 14.30,2.70 |
| Label | Label52 | เวลาทำงาน |  |  | 4.10,3.50 |
| Label | Label53 | ค่าแรง |  |  | 12.62,3.50 |
| Label | Label26 | วันปกติ |  |  | 0.70,4.20 |
| Label | Label27 |  วันลา |  |  | 1.70,4.20 |
| Label | Label28 | วันอาทิตย์ |  |  | 2.53,4.20 |
| Label | Label29 | วันหยุด |  |  | 3.75,4.20 |
| Label | Label30 |  OTปกติ |  |  | 4.70,4.20 |
| Label | Label31 | OTวันอาทิตย์ |  |  | 5.80,4.20 |
| Label | Label32 |  OTวันหยุด |  |  | 7.40,4.20 |
| Label | Label35 | วันปกติ |  |  | 8.77,4.20 |
| Label | Label36 | วันอาทิตย์ |  |  | 10.30,4.20 |
| Label | Label37 | วันหยุด |  |  | 11.60,4.20 |
| Label | Label38 |  OTปกติ |  |  | 12.60,4.20 |
| Label | Label39 |  OTวันอาทิตย์ |  |  | 13.70,4.20 |
| Label | Label40 |  OTวันหยุด |  |  | 15.40,4.20 |
| TextBox | W | W | ControlSource: `W`; Format: `Fixed`; DecimalPlaces: `1` |  | 0.90,4.79 |
| TextBox | L | L | ControlSource: `L`; Format: `Fixed`; DecimalPlaces: `1` |  | 1.70,4.79 |
| TextBox | S | S | ControlSource: `S`; Format: `Fixed`; DecimalPlaces: `1` |  | 2.50,4.79 |
| TextBox | H | H | ControlSource: `H`; Format: `Fixed`; DecimalPlaces: `1` |  | 3.78,4.79 |
| TextBox | O1 | O1 | ControlSource: `O1`; Format: `Fixed`; DecimalPlaces: `1` |  | 4.70,4.79 |
| TextBox | O2 | O2 | ControlSource: `O2`; Format: `Fixed`; DecimalPlaces: `1` |  | 5.80,4.79 |
| TextBox | O3 | O3 | ControlSource: `O3`; Format: `Fixed`; DecimalPlaces: `1` |  | 7.40,4.79 |
| TextBox | SALA | SALA | ControlSource: `SALA`; Format: `Standard`; DecimalPlaces: `0` |  | 8.90,4.79 |
| TextBox | S_W | S_W | ControlSource: `S_W`; Format: `Standard`; DecimalPlaces: `0` |  | 10.20,4.79 |
| TextBox | H_W | H_W | ControlSource: `H_W`; Format: `Standard`; DecimalPlaces: `0` |  | 11.60,4.79 |
| TextBox | OT | OT | ControlSource: `OT`; Format: `Standard`; DecimalPlaces: `0` |  | 12.60,4.79 |
| TextBox | OT2 | OT2 | ControlSource: `OT2`; Format: `Standard`; DecimalPlaces: `0` |  | 13.70,4.79 |
| TextBox | OT3 | OT3 | ControlSource: `OT3`; Format: `Standard`; DecimalPlaces: `0` |  | 15.40,4.79 |
| Label | Label55 | ATM |  |  | 0.90,5.50 |
| Label | Label56 | ประกันสังคม |  |  | 1.90,5.50 |
| Label | Label57 | กองทุนเลี้ยงชีพ |  |  | 3.90,5.50 |
| Label | Label58 | ค่าแรงค้างวิคก่อน |  |  | 6.10,5.50 |
| Label | Label59 | ภาษี |  |  | 8.50,5.50 |
| Label | Label71 | โบนัส |  |  | 9.89,5.50 |
| Label | Label60 | รวมค่าแรง |  |  | 12.52,5.50 |
| Label | Label61 | เงินได้สุทธิ |  |  | 14.61,5.50 |
| Label | Label80 | ค่าพักร้อน |  |  | 11.10,5.50 |
| TextBox | ATM | ATM | ControlSource: `ATM`; Format: `Standard`; DecimalPlaces: `2` |  | 0.79,6.19 |
| TextBox | SO_S | SO_S | ControlSource: `SO_S`; Format: `Standard`; DecimalPlaces: `2` |  | 1.90,6.19 |
| TextBox | FA_L | FA_L | ControlSource: `FA_L`; Format: `Standard`; DecimalPlaces: `2` |  | 3.90,6.19 |
| TextBox | INS | INS | ControlSource: `INS`; Format: `Standard`; DecimalPlaces: `2` |  | 6.10,6.19 |
| TextBox | TAX | TAX | ControlSource: `TAX`; Format: `Standard`; DecimalPlaces: `2` |  | 8.50,6.19 |
| TextBox | N | N | ControlSource: `N`; Format: `Standard`; DecimalPlaces: `0` |  | 9.71,6.19 |
| TextBox | HOT | HOT | ControlSource: `HOT`; Format: `Standard`; DecimalPlaces: `0` |  | 11.00,6.19 |
| TextBox | TT | TT | ControlSource: `TT`; Format: `Standard`; DecimalPlaces: `0` |  | 12.52,6.19 |
| TextBox | NET | NET | ControlSource: `NET`; Format: `Standard`; DecimalPlaces: `0` |  | 14.61,6.19 |
| Label | Label83 | ณัฏฐกานต์ |  |  | 7.48,7.01 |
| Label | Label84 | นิวาส |  |  | 12.80,7.01 |
| Label | Label72 | ผู้รับเงิน |  |  | 1.99,7.09 |
| Label | Label74 | แผนกบุคคล |  |  | 5.89,7.09 |
| Label | Label76 |       ผู้จัดการโรงงาน |  |  | 10.17,7.09 |

## ส่วน PageFooter `PageFooter` (สูง 0.00 ซม.)
(ไม่มี control)

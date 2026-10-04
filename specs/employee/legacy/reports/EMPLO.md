# รายงาน: EMPLO

- เรียกจาก: มาโคร [พิมพ์รายชื่อทั้งหมด](<../macros.md#m-พิมพ์รายชื่อทั้งหมด>) (OpenReport)
- สร้าง 2021-05-12 · แก้ล่าสุด 2021-05-12
- RecordSource: `พิมพ์รายชื่อทั้งหมด` → [พิมพ์รายชื่อทั้งหมด](<../queries/พิมพ์รายชื่อทั้งหมด.md#q-พิมพ์รายชื่อทั้งหมด>)
- Caption: EMPLO
- DefaultView: Single Form

## การจัดกลุ่ม/เรียงลำดับ

| ลำดับ | ฟิลด์/นิพจน์ | เรียง | Group Header | Group Footer | GroupOn |
|---:|---|---|---|---|---|
| 1 | `ID` | น้อย→มาก |  |  |   |

## ส่วน FormHeader `ReportHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน PageHeader `PageHeader` (สูง 3.02 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label43 | บริษัท ล็อคเต้ จำกัด |  |  | 5.60,0.00 |
| Label | Label14 | รายชื่อพนักงานทั้งหมด |  |  | 5.30,1.20 |
| Label | ID Label | รหัส |  |  | 0.80,2.40 |
| Label | NAME Label | ชื่อ-นามสกุล |  |  | 1.80,2.40 |
| Label | DEPARTMENT Label | แผนก |  |  | 6.39,2.40 |
| Label | POSITION Label | ตำแหน่ง |  |  | 8.89,2.40 |
| Label | BORN Label | เกิด |  |  | 10.87,2.40 |
| Label | START Label | เริ่มทำงาน |  |  | 12.70,2.40 |
| Label | CLAS Label | ประเภท |  |  | 14.49,2.40 |
| Label | Label25 | อายุ |  |  | 15.90,2.40 |
| Label | Label45 | โรงพยาบาล |  |  | 16.80,2.41 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.61 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | Text19 | =1 | ControlSource: `=1`; RunningSum ทั้งรายงาน |  | 0.00,0.00 |
| TextBox | Text23 | =(Now()-[born])/365 | ControlSource: `=(Now()-[born])/365`; Format: `Fixed`; DecimalPlaces: `0` |  | 15.79,0.00 |
| TextBox | HOSPITAL | HOSPITAL | ControlSource: `HOSPITAL` |  | 16.70,0.00 |
| TextBox | ID | ID | ControlSource: `ID` |  | 0.76,0.00 |
| TextBox | NAME | NAME | ControlSource: `NAME` |  | 1.81,0.00 |
| TextBox | DEPARTMENT | DEPARTMENT | ControlSource: `DEPARTMENT` |  | 6.41,0.00 |
| TextBox | POSITION | POSITION | ControlSource: `POSITION` |  | 8.89,0.00 |
| TextBox | BORN | BORN | ControlSource: `BORN`; Format: `Short Date` |  | 10.89,0.00 |
| TextBox | START | START | ControlSource: `START`; Format: `Short Date` |  | 12.80,0.00 |
| TextBox | CLAS | CLAS | ControlSource: `CLAS` |  | 14.50,0.00 |

## ส่วน PageFooter `PageFooter` (สูง 0.84 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | Text15 | =Now() | ControlSource: `=Now()`; Format: `d" ดดดด bbbb"` |  | 0.10,0.00 |
| TextBox | Text16 | ="หน้า " & [Page] & " จาก " & [Pages] | ControlSource: `="หน้า " & [Page] & " จาก " & [Pages]` |  | 8.10,0.00 |

## ส่วน FormFooter `ReportFooter` (สูง 1.68 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | Text33 | =Count([id]) | ControlSource: `=Count([id])`; DecimalPlaces: `0` |  | 2.50,0.28 |
| TextBox | SumOfSumOfM | SumOfSumOfM | ControlSource: `SumOfSumOfM` |  | 5.50,0.28 |
| TextBox | Text28 | =Avg((Now()-[born])/365) | ControlSource: `=Avg((Now()-[born])/365)`; Format: `Fixed`; DecimalPlaces: `0` |  | 15.80,0.28 |
| Label | Label39 | ชาย |  |  | 4.30,0.30 |
| Label | Label40 | คน |  |  | 6.50,0.30 |
| Label | Label41 | หญิง |  |  | 7.90,0.30 |
| TextBox | SumOfSumOfF | SumOfSumOfF | ControlSource: `SumOfSumOfF` |  | 8.90,0.30 |
| Label | Label42 | คน |  |  | 9.80,0.30 |
| Label | Label34 | รวมทั้งหมด |  |  | 0.70,0.31 |
| Label | Label35 | คน |  |  | 3.40,0.31 |
| Label | Label29 | อายุเฉลื่ย |  |  | 13.90,0.31 |

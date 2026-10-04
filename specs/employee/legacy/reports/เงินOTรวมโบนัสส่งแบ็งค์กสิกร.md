# รายงาน: เงินOTรวมโบนัสส่งแบ็งค์กสิกร

- เรียกจาก: มาโคร [เงินOTส่งแบงค์รวมโบนัส](<../macros.md#m-เงินOTส่งแบงค์รวมโบนัส>) (OpenReport)
- สร้าง 2021-05-12 · แก้ล่าสุด 2021-05-12
- RecordSource: `เงินOTส่งแบ็งค์รวมโบนัส` → [เงินOTส่งแบ็งค์รวมโบนัส](<../tables.md#t-เงินOTส่งแบ็งค์รวมโบนัส>)
- Caption: เงินOTส่งแบงค์กสิกร
- DefaultView: Single Form

## การจัดกลุ่ม/เรียงลำดับ

| ลำดับ | ฟิลด์/นิพจน์ | เรียง | Group Header | Group Footer | GroupOn |
|---:|---|---|---|---|---|
| 1 | `TITLE` | น้อย→มาก |  |  |   |
| 2 | `TITLE` | น้อย→มาก |  |  |   |

## ส่วน FormHeader `ReportHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน PageHeader `PageHeader` (สูง 2.23 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label25 | เงินOTรวมโบนัสส่งแบ็งค์กสิกร |  |  | 0.00,0.00 |
| Label | Label27 | รหัส |  |  | 1.40,1.50 |
| Label | NAME Label | ชื่อ-นามสกุล |  |  | 3.10,1.50 |
| Label | ACC Label | เลขบัญชี |  |  | 9.80,1.50 |
| Label | Expr1 Label | จำนวนเงิน |  |  | 12.20,1.50 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.70 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | Text13 | =1 | ControlSource: `=1`; RunningSum ทั้งรายงาน |  | 0.40,0.00 |
| TextBox | Text26 | TITLE | ControlSource: `TITLE` |  | 1.40,0.00 |
| TextBox | NAME | NAM | ControlSource: `NAM` |  | 3.10,0.00 |
| TextBox | ACC | ACC | ControlSource: `ACC` |  | 9.80,0.00 |
| TextBox | Expr1 | AMT | ControlSource: `AMT`; Format: `Standard` |  | 12.20,0.00 |

## ส่วน PageFooter `PageFooter` (สูง 0.79 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | Text9 | =Now() | ControlSource: `=Now()`; Format: `Long Date` |  | 0.20,0.00 |
| TextBox | Text10 | ="Page " & [Page] & " of " & [Pages] | ControlSource: `="Page " & [Page] & " of " & [Pages]` |  | 8.20,0.00 |

## ส่วน FormFooter `ReportFooter` (สูง 0.93 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label16 | รวม |  |  | 0.70,0.00 |
| TextBox | Text15 | =Count([Nam]) | ControlSource: `=Count([Nam])` |  | 1.90,0.00 |
| Label | Label20 | รายการ |  |  | 2.70,0.00 |
| Label | Label22 |  จำนวนเงิน |  |  | 10.20,0.00 |
| TextBox | Text21 | =Sum([AMT]) | ControlSource: `=Sum([AMT])`; Format: `Standard`; DecimalPlaces: `0` |  | 12.08,0.00 |

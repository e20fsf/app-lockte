# รายงาน: เงินOTส่งแบ็งค์กสิกร

- เรียกจาก: มาโคร [ค่าแรงส่งแบงค์](<../macros.md#m-ค่าแรงส่งแบงค์>) (OpenReport); มาโคร [ค่าแรงส่งแบงค์ทั้ง2ช่วง](<../macros.md#m-ค่าแรงส่งแบงค์ทั้ง2ช่วง>) (OpenReport)
- สร้าง 2021-05-12 · แก้ล่าสุด 2021-05-12
- RecordSource: `เงินOTส่งแบ็งค์` → [เงินOTส่งแบ็งค์](<../tables.md#t-เงินOTส่งแบ็งค์>)
- Caption: เงินOTส่งแบงค์กสิกร
- DefaultView: Single Form

## การจัดกลุ่ม/เรียงลำดับ

| ลำดับ | ฟิลด์/นิพจน์ | เรียง | Group Header | Group Footer | GroupOn |
|---:|---|---|---|---|---|
| 1 | `ACC` | น้อย→มาก |  |  |   |
| 2 | `ACC` | น้อย→มาก |  |  |   |

## ส่วน FormHeader `ReportHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน PageHeader `PageHeader` (สูง 2.23 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label25 | เงินOTส่งแบ็งค์กสิกร |  |  | 4.90,0.00 |
| Label | ACC Label | เลขบัญชี |  |  | 1.40,1.50 |
| Label | NAME Label | ชื่อ-นามสกุล |  |  | 5.50,1.50 |
| Label | Expr1 Label | จำนวนเงิน |  |  | 12.20,1.50 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.70 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | Text13 | =1 | ControlSource: `=1`; RunningSum ทั้งรายงาน |  | 0.40,0.00 |
| TextBox | ACC | ACC | ControlSource: `ACC` |  | 1.40,0.00 |
| TextBox | Text26 | TITLE | ControlSource: `TITLE` |  | 3.80,0.00 |
| TextBox | NAME | NAM | ControlSource: `NAM` |  | 5.50,0.00 |
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

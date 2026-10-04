# รายงาน: NO_WORK1

- เรียกจาก: มาโคร [ไม่มาทำงาน](<../macros.md#m-ไม่มาทำงาน>) (OpenReport)
- สร้าง 2021-05-12 · แก้ล่าสุด 2024-06-25
- RecordSource: `NO_WORK1` → [NO_WORK1](<../tables.md#t-NO_WORK1>)
- Caption: NO_WORK1
- DefaultView: Single Form

## การจัดกลุ่ม/เรียงลำดับ

| ลำดับ | ฟิลด์/นิพจน์ | เรียง | Group Header | Group Footer | GroupOn |
|---:|---|---|---|---|---|
| 1 | `DATE1` | น้อย→มาก |  |  |   |
| 2 | `ID1` | น้อย→มาก |  |  |   |

## ส่วน FormHeader `ReportHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน PageHeader `PageHeader` (สูง 2.81 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label36 | บริษัท ล็อคเต้ จำกัด |  |  | 0.00,0.00 |
| Label | Label8 | รายงานพนักงานที่ไม่มาทำงาน |  |  | 0.01,1.00 |
| Label | Label15 | วันที่ |  |  | 15.20,1.40 |
| TextBox | Text13 | =Now() | ControlSource: `=Now()`; Format: `Short Date` |  | 16.10,1.40 |
| Label | DATE Label | วันที่ |  |  | 0.80,2.10 |
| Label | ID Label | รหัส |  |  | 2.17,2.10 |
| Label | NAME Label | ชื่อ-นามสกุล |  |  | 3.01,2.10 |
| Label | Label38 | เหตุผล |  |  | 12.79,2.10 |
| Label | DEPARTMENT Label | แผนก |  |  | 9.21,2.10 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.61 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | Text16 | =1 | ControlSource: `=1`; RunningSum ทั้งรายงาน |  | 0.00,0.00 |
| TextBox | REMARK | REMARK | ControlSource: `REMARK` |  | 12.80,0.00 |
| TextBox | DATE | DATE1 | ControlSource: `DATE1`; Format: `Short Date` |  | 0.49,0.00 |
| TextBox | ID | ID1 | ControlSource: `ID1` |  | 2.17,0.00 |
| TextBox | NAME | NAME | ControlSource: `NAME` |  | 3.01,0.00 |
| TextBox | DEPARTMENT | DEPARTMENT | ControlSource: `DEPARTMENT` |  | 9.20,0.00 |

## ส่วน PageFooter `PageFooter` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน FormFooter `ReportFooter` (สูง 1.03 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | Text20 | =Count([id1]) | ControlSource: `=Count([id1])`; Format: `Standard`; DecimalPlaces: `0` |  | 3.40,0.09 |
| TextBox | QTY | QTY | ControlSource: `QTY` |  | 7.80,0.09 |
| TextBox | Text34 | =Count([id1])/[qty]*100 | ControlSource: `=Count([id1])/[qty]*100`; Format: `Standard`; DecimalPlaces: `2` |  | 11.39,0.09 |
| TextBox | Text39 | =[qty]-Count([id1]) | ControlSource: `=[qty]-Count([id1])`; Format: `Standard`; DecimalPlaces: `0` |  | 14.58,0.09 |
| Label | Label21 | รวม |  |  | 2.50,0.10 |
| Label | Label22 | คน |  |  | 4.10,0.10 |
| Label | Label32 | พนักงานทั้งหมด |  |  | 5.20,0.10 |
| Label | Label33 | คน |  |  | 8.80,0.10 |
| Label | Label35 | คิดเป็น% |  |  | 9.80,0.10 |
| Label | Label40 | มาทำงาน |  |  | 12.70,0.10 |
| Label | Label41 | คน |  |  | 15.70,0.10 |

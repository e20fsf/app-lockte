# รายงาน: OT

- เรียกจาก: มาโคร [ขออนุมัติทำ OT](<../macros.md#m-ขออนุมัติทำ-OT>) (OpenReport)
- สร้าง 2021-05-12 · แก้ล่าสุด 2025-04-26
- RecordSource: `OT` → [OT](<../tables.md#t-OT>)
- Caption: OT
- DefaultView: Single Form

## การจัดกลุ่ม/เรียงลำดับ

| ลำดับ | ฟิลด์/นิพจน์ | เรียง | Group Header | Group Footer | GroupOn |
|---:|---|---|---|---|---|
| 1 | `ID` | น้อย→มาก |  |  |   |

## ส่วน FormHeader `ReportHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน PageHeader `PageHeader` (สูง 1.89 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label18 | บริษัท ล็อคเต้ จำกัด |  |  | 0.00,0.00 |
| Label | REST_T Label | เรื่อง ขออนุมัติทำงานล่วงเวลา |  |  | 0.40,0.60 |
| Label | DEPART Label | แผนก |  |  | 4.70,0.60 |
| TextBox | DEPART | DEPART | ControlSource: `DEPART` |  | 5.60,0.60 |
| TextBox | Text73 | =IIf(DatePart("w",[date])=2,"วันจันทร์ที่",IIf(DatePart("w",[date])=3,"วันอังคารที่",IIf(DatePart("w",[date])=4,"วันพุธที่",IIf(DatePart("w",[date])=5,"วันพฤหัสบดีที่",IIf(DatePart("w",[date])=6,"วันศุกร์ที่",IIf(DatePart("w",[date])=7,"วันเสาร์ที่","วันอาทิตย์ที่")))))) | ControlSource: `=IIf(DatePart("w",[date])=2,"วันจันทร์ที่",IIf(DatePart("w",[date])=3,"วันอังคารที่",IIf(DatePart("w",[date])=4,"วันพุธที่",IIf(DatePart("w",[date])=5,"วันพฤหัสบดีที่",IIf(DatePart("w",[date])=6,"วันศุกร์ที่",IIf(DatePart("w",[date])=7,"วันเสาร์ที่","วันอาทิตย์ที่"))))))` |  | 9.20,0.60 |
| TextBox | DATE | DATE | ControlSource: `DATE`; Format: `Long Date` |  | 11.01,0.60 |
| Label | Label28 | ที่ |  |  | 0.00,1.20 |
| Label | ID Label | รหัส |  |  | 0.69,1.20 |
| Label | NAME Label | ชื่อ-นามสกุล |  |  | 1.70,1.20 |
| Label | W_IN Label | เริ่มงาน |  |  | 6.30,1.20 |
| Label | W_OUT Label | เลิกงาน |  |  | 7.70,1.20 |
| Label | Label24 | ลายเซ็นต์ |  |  | 9.10,1.20 |
| Label | MEMO Label | หมายเหตุ |  |  | 10.72,1.20 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.54 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | Text29 | =1 | ControlSource: `=1`; RunningSum ทั้งรายงาน |  | 0.00,0.00 |
| TextBox | ID | ID | ControlSource: `ID` |  | 0.69,0.00 |
| TextBox | NAME | NAME | ControlSource: `NAME` |  | 1.70,0.00 |
| TextBox | W_IN | W_IN | ControlSource: `W_IN`; Format: `Short Time` |  | 6.30,0.00 |
| TextBox | W_OUT | W_OUT | ControlSource: `W_OUT`; Format: `Short Time` |  | 7.66,0.00 |
| TextBox | MEMO | MEMO | ControlSource: `MEMO` |  | 10.72,0.00 |

## ส่วน PageFooter `PageFooter` (สูง 0.69 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label75 | ห้ามใส่รองเท้าแตะมาทำงาน ผู้ใดใส่มาไม่ให้ทำงานล่วงเวลา |  |  | 0.00,0.00 |
| Label | Label52 | ผู้จัดการ |  |  | 10.50,0.00 |

## ส่วน FormFooter `ReportFooter` (สูง 0.00 ซม.)
(ไม่มี control)

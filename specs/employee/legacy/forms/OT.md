# ฟอร์ม: OT

- เรียกจาก: มาโคร [ขออนุมัติทำ OT1](<../macros.md#m-ขออนุมัติทำ-OT1>) (OpenForm); มาโคร [ขออนุมัติทำ OT2](<../macros.md#m-ขออนุมัติทำ-OT2>) (OpenForm); มาโคร [ไม่มา](<../macros.md#m-ไม่มา>) (OpenForm)
- สร้าง 2021-05-12 · แก้ล่าสุด 2025-11-18
- RecordSource: `OT` → [OT](<../tables.md#t-OT>)
- Caption: OT
- DefaultView: Single Form
- DataEntry: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน Section `ส่วนรายละเอียด` (สูง 13.00 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label14 | พิมพ์ใบขออนุมัติทำงานล่วงเวลา |  |  | 6.60,0.00 |
| Label | DATE Label |  วันที่ทำ |  |  | 3.90,1.00 |
| Label | Label20 | แผนก |  |  | 6.79,1.00 |
| Label | W_IN Label | เวลาเข้า |  |  | 11.42,1.00 |
| Label | W_OUT Label | เวลาออก |  |  | 12.82,1.00 |
| Label | Label26 | เวลาพัก |  |  | 14.30,1.00 |
| Label | TYPE Label | ประเภท |  |  | 15.72,1.00 |
| TextBox | DATE | DATE | ControlSource: `DATE`; DefaultValue: `=Date()`; Format: `Short Date` |  | 3.90,1.60 |
| ComboBox | DEPART | DEPART | ControlSource: `DEPART`; RowSourceType: `Table/Query`; RowSource: `แผนก1` |  | 6.80,1.60 |
| ComboBox | W_IN | W_IN | ControlSource: `W_IN`; RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCT SHIFT.O_IN FROM SHIFT WHERE (((SHIFT.O_IN) Is Not Null)) ORDER BY SHIFT.O_IN; `; LimitToList; DefaultValue: `#12/30/1899 17:20:0#`; Format: `Short Time` |  | 11.40,1.60 |
| ComboBox | W_OUT | W_OUT | ControlSource: `W_OUT`; RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCT SHIFT.O_OUT FROM SHIFT WHERE (((SHIFT.O_OUT) Is Not Null)) ORDER BY SHIFT.O_OUT; `; LimitToList; DefaultValue: `#12/30/1899 20:20:0#`; Format: `Short Time` |  | 12.69,1.60 |
| ComboBox | REST_T | REST_T | ControlSource: `REST_T`; RowSourceType: `Table/Query`; RowSource: `เวลาพัก`; ColumnCount: `2`; ColumnWidths: `284;284`; DefaultValue: `0` |  | 14.30,1.60 |
| ComboBox | TYPE | TYPE | ControlSource: `TYPE`; RowSourceType: `Table/Query`; RowSource: `SELECT [ประเภทการทำงาน].CODE, [ประเภทการทำงาน].NAME FROM ประเภทการทำงาน; `; ColumnCount: `2`; ColumnWidths: `284;1418`; DefaultValue: `"O1"` |  | 15.70,1.60 |
| Subform | OT1 |  | SourceObject: `Form.OT1`; LinkChildFields: `DATE;depart;w_in;w_out;rest_t;type`; LinkMasterFields: `DATE;depart;w_in;w_out;rest_t;type` |  | 3.20,2.42 |
| CommandButton | คำสั่ง24 | หลายกะ |  | OnClick→ขออนุมัติทำ OT2 | 5.60,11.00 |
| CommandButton | คำสั่ง23 | พิมพ์ |  | OnClick→ขออนุมัติทำ OT | 9.80,11.00 |
| CommandButton | คำสั่ง25 | กลับเมนูเดิม |  | OnClick→[Event Procedure] | 13.80,11.00 |

## ส่วน FormFooter `FormFooter` (สูง 0.00 ซม.)
(ไม่มี control)

## VBA (Code-behind)

```vb
Attribute VB_GlobalNameSpace = False

Attribute VB_Creatable = True

Attribute VB_PredeclaredId = True

Attribute VB_Exposed = False

Option Compare Database

Option Explicit



Private Sub คำสั่ง15_Click()

On Error GoTo Err_คำสั่ง15_Click





    DoCmd.Close



Exit_คำสั่ง15_Click:

    Exit Sub



Err_คำสั่ง15_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง15_Click

    

End Sub

Private Sub คำสั่ง16_Click()

On Error GoTo Err_คำสั่ง16_Click





    DoCmd.GoToRecord , , acNext



Exit_คำสั่ง16_Click:

    Exit Sub



Err_คำสั่ง16_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง16_Click

    

End Sub

Private Sub คำสั่ง25_Click()

On Error GoTo Err_คำสั่ง25_Click





    DoCmd.Close



Exit_คำสั่ง25_Click:

    Exit Sub



Err_คำสั่ง25_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง25_Click

    

End Sub
```

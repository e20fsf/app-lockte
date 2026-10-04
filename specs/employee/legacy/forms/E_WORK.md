# ฟอร์ม: E_WORK

- เรียกจาก: มาโคร [บันทึกเวลาทำงานพิเศษ](<../macros.md#m-บันทึกเวลาทำงานพิเศษ>) (OpenForm); เมนู 3.3 บันทึกเวลาทำงาน,OT,วันอาทิตย์และวันหยุด (Switchboard)
- สร้าง 2021-05-12 · แก้ล่าสุด 2024-06-15
- RecordSource: `E_WORK` → [E_WORK](<../tables.md#t-E_WORK>)
- Caption: E_WORK
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
| Label | Label14 | บันทึกเวลาทำงานของพนักงาน |  |  | 6.90,0.40 |
| Label | DATE Label |  วันที่ |  |  | 7.30,2.20 |
| TextBox | DATE | DATE | ControlSource: `DATE`; Format: `Short Date` |  | 8.28,2.20 |
| Subform | E_WORK subform1 |  | SourceObject: `Form.E_WORK subform1`; LinkChildFields: `DATE;W_IN;W_OUT;REST_T;TYPE;LATE`; LinkMasterFields: `DATE;W_IN;W_OUT;REST_T;TYPE;LATE` |  | 11.80,2.20 |
| Label | W_IN Label | เวลาเข้า |  |  | 6.90,3.20 |
| ComboBox | W_IN | W_IN | ControlSource: `W_IN`; RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCT SHIFT.O_IN FROM SHIFT WHERE (((SHIFT.O_IN) Is Not Null)) ORDER BY SHIFT.O_IN; `; Format: `Short Time` |  | 8.28,3.20 |
| Label | W_OUT Label | เวลาออก |  |  | 6.80,4.20 |
| ComboBox | W_OUT | W_OUT | ControlSource: `W_OUT`; RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCT SHIFT.O_OUT FROM SHIFT WHERE (((SHIFT.O_OUT) Is Not Null)) ORDER BY SHIFT.O_OUT; `; ColumnWidths: `1701`; Format: `Short Time` |  | 8.28,4.20 |
| Label | WORK_T Label | เวลาพัก(นาที) |  |  | 6.20,5.20 |
| TextBox | WORK_T | REST_T | ControlSource: `REST_T` |  | 8.28,5.20 |
| Label | TYPE Label | ประเภท |  |  | 6.90,6.20 |
| ComboBox | TYPE | TYPE | ControlSource: `TYPE`; RowSourceType: `Table/Query`; RowSource: `SELECT [ประเภทการทำงาน].CODE, [ประเภทการทำงาน].NAME FROM ประเภทการทำงาน; `; ColumnCount: `2`; ColumnWidths: `284;1418` |  | 8.28,6.20 |
| CommandButton | คำสั่ง16 | รายการต่อไป |  | OnClick→บันทึกเวลาทำงานพิเศษ | 5.90,8.90 |
| CommandButton | คำสั่ง15 | กลับเมนูเดิม |  | OnClick→แมโคร5 | 11.60,8.90 |

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
```

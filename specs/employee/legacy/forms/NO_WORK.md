# ฟอร์ม: NO_WORK

- เรียกจาก: มาโคร [บันทึกไม่มาทำงาน](<../macros.md#m-บันทึกไม่มาทำงาน>) (OpenForm); มาโคร [บันทึกไม่มาทำงาน1](<../macros.md#m-บันทึกไม่มาทำงาน1>) (OpenForm); เมนู 3.1 บันทึก,แก้ไขและพิมพ์รายงานพนักงานไม่มาทำงาน (Switchboard)
- สร้าง 2021-05-12 · แก้ล่าสุด 2024-06-15
- RecordSource: `NO_WORK` → [NO_WORK](<../tables.md#t-NO_WORK>)
- Caption: NO_WORK
- DefaultView: Single Form
- DataEntry: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 2.52 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label14 | บันทึกพนักงานที่ไม่มาทำงาน |  |  | 6.10,1.30 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 12.20 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | DATE Label |                          วันที่ |  |  | 2.60,0.10 |
| TextBox | DATE | DATE1 | ControlSource: `DATE1`; Format: `Short Date` |  | 5.46,0.10 |
| Subform | ลูก17 |  | SourceObject: `Form.E_WORK subform2`; LinkChildFields: `DATE1;W_IN;W_OUT;WORK_T;TYPE;LATE`; LinkMasterFields: `DATE1;W_IN;W_OUT;WORK_T;TYPE;LATE` |  | 8.60,0.20 |
| Label | W_IN Label | เวลาเข้า |  |  | 2.60,0.90 |
| ComboBox | W_IN | W_IN | ControlSource: `W_IN`; RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCT SHIFT.W_IN FROM SHIFT WHERE (((SHIFT.W_IN) Is Not Null)) ORDER BY SHIFT.W_IN; `; Format: `Short Time` |  | 5.46,0.90 |
| Label | W_OUT Label | เวลาออก |  |  | 2.60,1.60 |
| ComboBox | W_OUT | W_OUT | ControlSource: `W_OUT`; RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCT SHIFT.W_OUT FROM SHIFT WHERE (((SHIFT.W_OUT) Is Not Null)) ORDER BY SHIFT.W_OUT; `; Format: `Short Time` |  | 5.46,1.60 |
| Label | WORK_T Label | จำนวนวัน |  |  | 2.60,2.31 |
| TextBox | WORK_T | WORK_T | ControlSource: `WORK_T`; DefaultValue: `1` |  | 5.46,2.31 |
| Label | TYPE Label | TYPE |  |  | 2.60,3.01 |
| TextBox | TYPE | TYPE | ControlSource: `TYPE`; DefaultValue: `"A"` |  | 5.46,3.01 |
| CommandButton | คำสั่ง21 | พิมพ์รายงาน |  | OnClick→[Event Procedure] | 3.90,6.00 |
| CommandButton | คำสั่ง22 | แก้ไข |  | OnClick→บันทึกไม่มาทำงาน1 | 9.70,6.00 |
| CommandButton | คำสั่ง15 | กลับเมนูเดิม |  | OnClick→บันทึกไม่มาทำงาน | 13.90,6.00 |

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





    DoCmd.GoToRecord , , acNext



Exit_คำสั่ง15_Click:

    Exit Sub



Err_คำสั่ง15_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง15_Click

    

End Sub

Private Sub คำสั่ง16_Click()

On Error GoTo Err_คำสั่ง16_Click





    DoCmd.Close



Exit_คำสั่ง16_Click:

    Exit Sub



Err_คำสั่ง16_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง16_Click

    

End Sub

Private Sub คำสั่ง21_Click()

On Error GoTo Err_คำสั่ง21_Click



    Dim stDocName As String

    Dim stLinkCriteria As String



    stDocName = "วันทำงาน1"

    DoCmd.OpenForm stDocName, , , stLinkCriteria



Exit_คำสั่ง21_Click:

    Exit Sub



Err_คำสั่ง21_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง21_Click

    

End Sub

Private Sub คำสั่ง22_Click()

On Error GoTo Err_คำสั่ง22_Click



    Dim stDocName As String

    Dim stLinkCriteria As String



    stDocName = "NO_WORK2"

    DoCmd.OpenForm stDocName, , , stLinkCriteria



Exit_คำสั่ง22_Click:

    Exit Sub



Err_คำสั่ง22_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง22_Click

    

End Sub
```

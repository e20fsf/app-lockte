# ฟอร์ม: EMPLO3

- เรียกจาก: เมนู 3.4 แก้ไขและลบเวลาทำงาน (Switchboard)
- สร้าง 2021-05-12 · แก้ล่าสุด 2025-11-20
- RecordSource: `EMPLO` → [EMPLO](<../tables.md#t-EMPLO>)
- Caption: EMPLO3
- DefaultView: Single Form
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน Section `ส่วนรายละเอียด` (สูง 13.00 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | ID_Label | เลือกรหัสพนักงานที่ต้องการแก้ไข |  |  | 3.50,0.60 |
| ComboBox | ผสม4 |  | RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCTROW EMPLO.ID, EMPLO.NAME, EMPLO.SHIFT FROM EMPLO ORDER BY EMPLO.ID; `; ColumnCount: `3`; ColumnWidths: `853;3969;567` | AfterUpdate→[Event Procedure] | 10.39,0.60 |
| Subform | E_WORK subform |  | SourceObject: `Form.E_WORK1`; LinkChildFields: `ID`; LinkMasterFields: `ID` |  | 3.30,1.80 |
| CommandButton | คำสั่ง9 | ลบข้อมูลเฉพาะคนเปิดฟอร์ม |  | OnClick→[Event Procedure] | 3.40,9.30 |
| CommandButton | คำสั่ง10 | ลบข้อมูลทั้งวันเปิดฟอร์ม |  | OnClick→[Event Procedure] | 8.39,9.30 |
| CommandButton | คำสั่ง8 | กลับเมนูเดิม |  | OnClick→[Event Procedure] | 12.50,9.30 |

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



Private Sub ผสม4_AfterUpdate()

Dim rst As Object

Set rst = Me.RecordsetClone

rst.FindFirst " [id] = '" & Me!ผสม4 & "'"

Me.Bookmark = rst.Bookmark



End Sub





Private Sub คำสั่ง8_Click()

On Error GoTo Err_คำสั่ง8_Click





    DoCmd.Close



Exit_คำสั่ง8_Click:

    Exit Sub



Err_คำสั่ง8_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง8_Click

    

End Sub

Private Sub คำสั่ง9_Click()

On Error GoTo Err_คำสั่ง9_Click



    Dim stDocName As String

    Dim stLinkCriteria As String



    stDocName = "วันทำงาน2"

    DoCmd.OpenForm stDocName, , , stLinkCriteria



Exit_คำสั่ง9_Click:

    Exit Sub



Err_คำสั่ง9_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง9_Click

    

End Sub

Private Sub คำสั่ง10_Click()

On Error GoTo Err_คำสั่ง10_Click



    Dim stDocName As String

    Dim stLinkCriteria As String



    stDocName = "วันทำงาน3"

    DoCmd.OpenForm stDocName, , , stLinkCriteria



Exit_คำสั่ง10_Click:

    Exit Sub



Err_คำสั่ง10_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง10_Click

    

End Sub
```

# ฟอร์ม: EMPLO1

- เรียกจาก: มาโคร [แมโคร1](<../macros.md#m-แมโคร1>) (OpenForm)
- สร้าง 2021-05-12 · แก้ล่าสุด 2025-10-08
- RecordSource: `EMPLO` → [EMPLO](<../tables.md#t-EMPLO>)
- OrderBy: EMPLO.ID
- OrderByOn: NotDefault
- Caption: EMPLO1
- DefaultView: Single Form
- AllowAdditions: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 1

## ส่วน FormHeader `FormHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน Section `ส่วนรายละเอียด` (สูง 13.70 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label44 | เลือกรหัสพนักงานที่ต้องการแก้ไข |  |  | 1.30,0.00 |
| Subform | ลูก55 |  | SourceObject: `Form.โรงพยาบาลที่เลือก` |  | 15.20,0.00 |
| ComboBox | ผสม43 |  | RowSourceType: `Table/Query`; RowSource: `แบบสอบถาม16`; ColumnCount: `3`; ColumnWidths: `876;3177;1134` | AfterUpdate→[Event Procedure] | 9.78,0.20 |
| Label | Label61 | อายุ |  |  | 13.50,0.30 |
| TextBox | Text60 |  | DecimalPlaces: `0` |  | 14.20,0.30 |
| Subform | EMPLO2 |  | SourceObject: `Form.EMPLO2`; LinkChildFields: `ID`; LinkMasterFields: `ID` |  | 0.00,1.30 |
| Subform | แบบสอบถาม2 subform1 |  | หดได้; SourceObject: `Form.เพศพนักงานทั้งหมด1` |  | 15.10,3.30 |
| Subform | ลูก53 |  | SourceObject: `Form.วุฒิการศึกษา1` |  | 3.10,10.60 |
| CommandButton | คำสั่ง52 | ลบพนักงานที่ลาออก |  | OnClick→ลบพนักงานที่ลาออกแล้ว | 3.60,12.00 |
| CommandButton | คำสั่ง47 | กลับเมนูเดิม |  | OnClick→ปรับปรุงข้อมูลพนักงาน1 | 11.28,12.00 |

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



Private Sub คำสั่ง41_Click()

On Error GoTo Err_คำสั่ง41_Click





    DoCmd.Close



Exit_คำสั่ง41_Click:

    Exit Sub



Err_คำสั่ง41_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง41_Click

    

End Sub

Private Sub คำสั่ง42_Click()

On Error GoTo Err_คำสั่ง42_Click





    DoCmd.GoToRecord , , acNext



Exit_คำสั่ง42_Click:

    Exit Sub



Err_คำสั่ง42_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง42_Click

    

End Sub



Private Sub ผสม43_AfterUpdate()

Dim rst As Object

Set rst = Me.RecordsetClone

rst.FindFirst " [id] = '" & Me!ผสม43 & "'"

Me.Bookmark = rst.Bookmark

[Text60] = ผสม43.Column(2)

End Sub





Private Sub คำสั่ง47_Click()

On Error GoTo Err_คำสั่ง47_Click





    DoCmd.Close



Exit_คำสั่ง47_Click:

    Exit Sub



Err_คำสั่ง47_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง47_Click

    

End Sub

Private Sub คำสั่ง57_Click()

On Error GoTo Err_คำสั่ง57_Click



    Dim stDocName As String

    Dim stLinkCriteria As String



    stDocName = "แบบฟอร์มการรับพนักงาน"

    DoCmd.OpenForm stDocName, , , stLinkCriteria



Exit_คำสั่ง57_Click:

    Exit Sub



Err_คำสั่ง57_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง57_Click

    

End Sub
```

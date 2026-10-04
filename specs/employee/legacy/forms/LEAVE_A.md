# ฟอร์ม: LEAVE_A

- เรียกจาก: มาโคร [ลางานรายคน1](<../macros.md#m-ลางานรายคน1>) (OpenForm)
- สร้าง 2021-05-12 · แก้ล่าสุด 2026-08-21
- RecordSource: `LEAVE` → [LEAVE](<../tables.md#t-LEAVE>)
- Caption: LEAVE_A
- DefaultView: Single Form
- DataEntry: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0
- เหตุการณ์ระดับฟอร์ม: OnCurrent→[Event Procedure]; OnLoad→[Event Procedure]

## ส่วน FormHeader `FormHeader` (สูง 2.46 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label40 | บันทึกการลางานของ |  |  | 3.10,0.00 |
| TextBox | Text59 |  |  |  | 8.50,0.00 |
| Label | FROM_T Label | ตั้งแต่เวลา |  |  | 7.80,1.80 |
| Label | REMARK Label | เหตุผล |  |  | 13.11,1.80 |
| Label | Label22 | ค่าแรง/วัน |  |  | 16.90,1.80 |
| Label | DATE Label | วันที่ |  |  | 0.10,1.80 |
| Label | ID Label | รหัส |  |  | 1.80,1.80 |
| Label | FROM_D Label | ตั้งแต่วันที่ |  |  | 3.10,1.80 |
| Label | TO_D Label | ถึงวันที่ |  |  | 5.60,1.80 |
| Label | TO_T Label | ถึงเวลา |  |  | 9.30,1.80 |
| Label | SUM_D Label | จำนวนวัน |  |  | 10.40,1.80 |
| Label | SR Label | ประเภท |  |  | 11.84,1.80 |
| Label | SAL Label | ได้ค่าแรง |  |  | 18.40,1.80 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 2.80 ซม.) — OnClick→[Event Procedure]

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | DATE | DATE | ControlSource: `DATE`; DefaultValue: `Now()`; Format: `Short Date` |  | 0.00,0.00 |
| TextBox | ID | ID | ControlSource: `ID` | AfterUpdate→[Event Procedure] | 1.80,0.00 |
| TextBox | FROM_D | FROM_D | ControlSource: `FROM_D`; Format: `Short Date` |  | 3.10,0.00 |
| TextBox | TO_D | TO_D | ControlSource: `TO_D`; Format: `Short Date` |  | 5.50,0.00 |
| TextBox | FROM_T | FROM_T | ControlSource: `FROM_T`; Format: `Short Time` |  | 7.90,0.00 |
| TextBox | TO_T | TO_T | ControlSource: `TO_T`; Format: `Short Time` |  | 9.30,0.00 |
| TextBox | SUM_D | SUM_D | ControlSource: `SUM_D`; Format: `Standard` |  | 10.29,0.00 |
| ComboBox | SR | SR | ControlSource: `SR`; RowSourceType: `Table/Query`; RowSource: `SELECT ประเภทการลางาน.ID, ประเภทการลางาน.DISCRIPTION, * FROM ประเภทการลางาน; `; ColumnCount: `2`; ColumnWidths: `285;2835` |  | 11.89,0.00 |
| TextBox | REMARK | REMARK | ControlSource: `REMARK` |  | 13.11,0.00 |
| TextBox | SALA_DAY | SALA_DAY | ControlSource: `SALA_DAY`; Format: `Standard` |  | 17.10,0.00 |
| ComboBox | SAL | SAL | ControlSource: `SAL`; RowSourceType: `Table/Query`; RowSource: `SELECT [ได้ค่าแรง].IDD, [ได้ค่าแรง].NAMED FROM ได้ค่าแรง; `; ColumnCount: `2`; ColumnWidths: `288;1134` | AfterUpdate→[Event Procedure] | 18.44,0.00 |
| CommandButton | Command41 | กลับเมนูเดิม |  | OnClick→[Event Procedure] | 14.70,0.90 |

## ส่วน FormFooter `FormFooter` (สูง 7.81 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label39 | ประวัติการลางานที่ได้ค่าแรง |  |  | 2.80,0.00 |
| Label | Label62 | พักร้อน |  |  | 8.90,0.10 |
| TextBox | Text61 |  |  |  | 10.10,0.10 |
| Label | Label71 | เพศ |  |  | 11.20,0.10 |
| TextBox | Text70 |  |  |  | 12.60,0.10 |
| Label | Label75 | วันที่เริ่มทำงาน |  |  | 13.60,0.10 |
| TextBox | Text74 |  |  |  | 15.80,0.10 |
| Label | Label26 | ลากิจ |  |  | 0.10,0.80 |
| Label | Label36 | ลากิจฉุกเฉิน |  |  | 1.80,0.80 |
| Label | Label38 | รวมลากิจ |  |  | 3.80,0.80 |
| Label | Label30 | ลาป่วยมีใบแพทย์ |  |  | 5.40,0.80 |
| Label | Label32 | ลาป่วยไม่มีใบแพทย์ |  |  | 7.90,0.80 |
| Label | Label34 | รวมลาป่วย |  |  | 11.00,0.80 |
| Label | Label28 | ลาพักร้อน |  |  | 12.90,0.80 |
| Label | Label69 | ลาคลอด |  |  | 14.90,0.80 |
| Label | Label73 | ลาเพื่อระดมพล |  |  | 16.50,0.80 |
| Label | Label77 | รวมทั้งหมด |  |  | 18.88,0.80 |
| TextBox | Text25 |  | DefaultValue: `0`; Format: `General Number` |  | 0.50,1.40 |
| TextBox | Text35 |  | DefaultValue: `0`; Format: `General Number` |  | 2.60,1.40 |
| TextBox | Text37 |  | DefaultValue: `0`; Format: `General Number` |  | 4.20,1.40 |
| TextBox | Text29 |  | DefaultValue: `0`; Format: `General Number` |  | 6.90,1.40 |
| TextBox | Text31 |  | DefaultValue: `0`; Format: `General Number` |  | 9.90,1.40 |
| TextBox | Text33 |  | DefaultValue: `0`; Format: `General Number` |  | 11.70,1.40 |
| TextBox | Text27 |  | DefaultValue: `0`; Format: `General Number` |  | 13.50,1.40 |
| TextBox | Text68 |  | Format: `General Number` |  | 15.50,1.40 |
| TextBox | Text72 |  | Format: `General Number` |  | 17.80,1.40 |
| TextBox | Text76 |  | Format: `General Number` |  | 19.50,1.40 |
| Tab | TabCtl49 |  |  |  | 0.00,2.09 |
| Page | Page50 | รายละเอียดการลา |  |  | 0.19,3.05 |
| Page | Page51 | สรุปการลา |  |  | 0.19,3.05 |
| Subform | LEAVE1 |  | SourceObject: `Form.LEAVE1`; LinkChildFields: `ID`; LinkMasterFields: `ID` |  | 0.30,3.59 |
| Subform | สรุปการลา subform |  | SourceObject: `Form.สรุปการลา subform`; LinkChildFields: `ID`; LinkMasterFields: `ID` |  | 0.30,3.59 |

## VBA (Code-behind)

```vb
Attribute VB_GlobalNameSpace = False

Attribute VB_Creatable = True

Attribute VB_PredeclaredId = True

Attribute VB_Exposed = False

Option Compare Database

Option Explicit



Private Sub Form_Current()



End Sub



Private Sub Form_Load()

   

   

   Forms![leave_a].[Text25] = Forms![สรุปการลา subform1].[1]

   Forms![leave_a].[Text27] = Forms![สรุปการลา subform1].[6]

   Forms![leave_a].[Text29] = Forms![สรุปการลา subform1].[2]

   Forms![leave_a].[Text31] = Forms![สรุปการลา subform1].[3]

   Forms![leave_a].[Text35] = Forms![สรุปการลา subform1].[9]

   Forms![leave_a].[Text68] = Forms![สรุปการลา subform1].[4]

   Forms![leave_a].[Text72] = Forms![สรุปการลา subform1].[10]

   Forms![leave_a].[Text70] = Forms![สรุปการลา subform1].[SEX]

   Forms![leave_a].[Text37] = Forms![สรุปการลา subform1].[expr1]

   Forms![leave_a].[Text33] = Forms![สรุปการลา subform1].[expr2]

   Forms![leave_a].[Text59] = Forms![สรุปการลา subform1].[NAMES]

   Forms![leave_a].[Text61] = Forms![สรุปการลา subform1].[expr3]

   Forms![leave_a].[Text74] = Forms![สรุปการลา subform1].[START]

   Forms![leave_a].[ID] = Forms![รหัส1].[ID_E]

   Forms![leave_a].[SALA_DAY] = Forms![รหัส1].[SALA]

   [Text76] = [Text33] + [Text37] + [Text27] + [Text68] + [Text72]

End Sub



Private Sub ID_AfterUpdate()

Me![SALA_DAY] = Me![ID].Column(2)

ID = UCase([ID])

End Sub





Private Sub SAL_AfterUpdate()

 

  If SAL = "Y" Then

       Select Case SR

            Case "1"

            If (SUM_D + Text37) <= 6 Then

                 MsgBox "คลิกปุ่ม OK เพื่อทำการบันทึก", vbOKOnly, "บันทึก"

                  DoCmd.RunMacro "macro1"

            Else

                 MsgBox "ไม่สามารถบันทึกได้ เนื่องจากลากิจได้ค่าแรงเกิน 6 วัน", vbOKOnly, "ไม่บันทึก"

                 SUM_D = Null

                 DoCmd.RunMacro "macro1"

            End If

            Case "9"

            If (SUM_D + Text37) <= 6 Then

                 MsgBox "คลิกปุ่ม OK เพื่อทำการบันทึก", vbOKOnly, "บันทึก"

                  DoCmd.RunMacro "macro1"

            Else

                 MsgBox "ไม่สามารถบันทึกได้ เนื่องจากลากิจได้ค่าแรงเกิน 6 วัน", vbOKOnly, "ไม่บันทึก"

                 SUM_D = Null

                 DoCmd.RunMacro "macro1"

            End If

            Case "2"

            If (SUM_D + Text33) <= 30 Then

                 MsgBox "คลิกปุ่ม OK เพื่อทำการบันทึก", vbOKOnly, "บันทึก"

                  DoCmd.RunMacro "macro1"

            Else

                 MsgBox "ไม่สามารถบันทึกได้ เนื่องจากลาป่วยได้ค่าแรงเกิน 30 วัน", vbOKOnly, "ไม่บันทึก"

                 SUM_D = Null

                 DoCmd.RunMacro "macro1"

            End If

            Case "3"

            If (SUM_D + Text33) <= 30 Then

                 MsgBox "คลิกปุ่ม OK เพื่อทำการบันทึก", vbOKOnly, "บันทึก"

                  DoCmd.RunMacro "macro1"

            Else

                 MsgBox "ไม่สามารถบันทึกได้ เนื่องจากลาป่วยได้ค่าแรงเกิน 30 วัน", vbOKOnly, "ไม่บันทึก"

                 SUM_D = Null

                 DoCmd.RunMacro "macro1"

            End If

            Case "6"

            If Text61 = "YES" Then

                 If (SUM_D + Text27) <= 12 Then

                 MsgBox "คลิกปุ่ม OK เพื่อทำการบันทึก", vbOKOnly, "บันทึก"

                  DoCmd.RunMacro "macro1"

                 Else

                 MsgBox "ไม่สามารถบันทึกได้ เนื่องจากลาพักร้อนเกิน 12 วัน", vbOKOnly, "ไม่บันทึก"

                 SUM_D = Null

                 DoCmd.RunMacro "macro1"

                 End If

            Else

                 MsgBox "ไม่สามารถบันทึกได้ เนื่องจากยังไม่มีพักร้อน", vbOKOnly, "ไม่บันทึก"

                 SUM_D = Null

                 DoCmd.RunMacro "macro1"

            End If

            Case "4"

            If Text70 = "F" Then

                 If (SUM_D + Text68) <= 60 Then

                      MsgBox "คลิกปุ่ม OK เพื่อทำการบันทึก", vbOKOnly, "บันทึก"

                       DoCmd.RunMacro "macro1"

                 Else

                      MsgBox "ไม่สามารถบันทึกได้ เนื่องจากลาคลอดได้ค่าแรงเกิน 60 วัน", vbOKOnly, "ไม่บันทึก"

                      SUM_D = Null

                      DoCmd.RunMacro "macro1"

                 End If

             Else

                      MsgBox "ไม่สามารถบันทึกได้ เนื่องจากเพศชายลาคลอดไม่ได้", vbOKOnly, "ไม่บันทึก"

                      SUM_D = Null

                      DoCmd.RunMacro "macro1"

             End If

             Case "5"

                      MsgBox "คลิกปุ่ม OK เพื่อทำการบันทึก", vbOKOnly, "บันทึก"

                      DoCmd.RunMacro "macro1"

             Case "7"

                      MsgBox "ไม่สามารถบันทึกได้ เนื่องจากลาอื่นๆไม่ได้ค่าแรง", vbOKOnly, "ไม่บันทึก"

                      SUM_D = Null

                      DoCmd.RunMacro "macro1"

              Case "8"

                      MsgBox "ไม่สามารถบันทึกได้ เนื่องจากขาดงานไม่ได้ค่าแรง", vbOKOnly, "ไม่บันทึก"

                      SUM_D = Null

                      DoCmd.RunMacro "macro1"

               Case "10"

                      If (SUM_D + Text72) <= 60 Then

                      MsgBox "คลิกปุ่ม OK เพื่อทำการบันทึก", vbOKOnly, "บันทึก"

                       DoCmd.RunMacro "macro1"

                 Else

                      MsgBox "ไม่สามารถบันทึกได้ เนื่องจากลาได้ค่าแรงเกิน 60 วัน", vbOKOnly, "ไม่บันทึก"

                      SUM_D = Null

                      DoCmd.RunMacro "macro1"

                 End If

                Case "11"

                      MsgBox "คลิกปุ่ม OK เพื่อทำการบันทึก", vbOKOnly, "บันทึก"

                      DoCmd.RunMacro "macro1"

                 Case "12"

                      MsgBox "คลิกปุ่ม OK เพื่อทำการบันทึก", vbOKOnly, "บันทึก"

                      DoCmd.RunMacro "macro1"

                  Case "13"

                      MsgBox "ไม่สามารถบันทึกได้ เนื่องจากเป็นการลาที่ไม่ได้ค่าแรง", vbOKOnly, "ไม่บันทึก"

                      SUM_D = Null

                      DoCmd.RunMacro "macro1"

                   Case "14"

                      MsgBox "ไม่สามารถบันทึกได้ เนื่องจากเป็นการลาที่ไม่ได้ค่าแรง", vbOKOnly, "ไม่บันทึก"

                      SUM_D = Null

                      DoCmd.RunMacro "macro1"

                   Case "15"

                      MsgBox "ไม่สามารถบันทึกได้ เนื่องจากเป็นการลาที่ไม่ได้ค่าแรง", vbOKOnly, "ไม่บันทึก"

                      SUM_D = Null

                      DoCmd.RunMacro "macro1"

                End Select

  Else

       MsgBox "คลิกปุ่ม OK เพื่อทำการบันทึก", vbOKOnly, "บันทึก"

       DoCmd.RunMacro "macro1"

       

  End If

        

End Sub





Private Sub Command41_Click()

On Error GoTo Err_Command41_Click



    Dim stDocName As String



    stDocName = "Macro1"

    DoCmd.RunMacro stDocName



Exit_Command41_Click:

    Exit Sub



Err_Command41_Click:

    MsgBox Err.Description

    Resume Exit_Command41_Click

    

End Sub

Private Sub Command63_Click()

On Error GoTo Err_Command63_Click



    Dim stDocName As String

    Dim stLinkCriteria As String



    stDocName = "วันที่ลบการลางาน"

    DoCmd.OpenForm stDocName, , , stLinkCriteria



Exit_Command63_Click:

    Exit Sub



Err_Command63_Click:

    MsgBox Err.Description

    Resume Exit_Command63_Click

    

End Sub

Private Sub Command64_Click()

On Error GoTo Err_Command64_Click



    Dim stDocName As String



    stDocName = "รายงานลางานเฉพาะแผนก"

    DoCmd.RunMacro stDocName



Exit_Command64_Click:

    Exit Sub



Err_Command64_Click:

    MsgBox Err.Description

    Resume Exit_Command64_Click

    

End Sub

Private Sub Command65_Click()

On Error GoTo Err_Command65_Click



    Dim stDocName As String

    Dim stLinkCriteria As String



    stDocName = "ใบเตือน"

    DoCmd.OpenForm stDocName, , , stLinkCriteria



Exit_Command65_Click:

    Exit Sub



Err_Command65_Click:

    MsgBox Err.Description

    Resume Exit_Command65_Click

    

End Sub

Private Sub Command66_Click()

On Error GoTo Err_Command66_Click



    Dim stDocName As String



    stDocName = "แมโคร53"

    DoCmd.RunMacro stDocName



Exit_Command66_Click:

    Exit Sub



Err_Command66_Click:

    MsgBox Err.Description

    Resume Exit_Command66_Click

    

End Sub



Private Sub ส่วนรายละเอียด_Click()



End Sub
```

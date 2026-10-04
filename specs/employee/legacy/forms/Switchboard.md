# ฟอร์ม: Switchboard

- เรียกจาก: — (ไม่มีที่เรียกใช้)
- สร้าง 2021-05-12 · แก้ล่าสุด 2026-08-21
- RecordSource: `Switchboard Items` → [Switchboard Items](<../tables.md#t-Switchboard-Items>)
- Filter: [ItemNumber] = 0 AND [SwitchboardID]=4
- FilterOn: NotDefault
- Caption: Switchboard
- DefaultView: Single Form
- AllowAdditions: NotDefault
- AllowEdits: NotDefault
- AllowDeletions: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0
- เหตุการณ์ระดับฟอร์ม: OnCurrent→[Event Procedure]; OnOpen→[Event Procedure]; OnLoad→MAX

## ส่วน Section `Detail` (สูง 23.59 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| UnboundObjectFrame | OLEUnbound25 |  |  |  | 0.00,0.00 |
| Label | Label1 | บริษัท ล็อคเต้ จำกัด |  |  | 5.20,0.00 |
| Label | Label29 | ประกันสังคม |  |  | 15.30,0.00 |
| Label | Label28 | เลขที่บัญชี 10-0014348-1 |  |  | 15.30,0.80 |
| Label | Label2 | db1 |  |  | 5.29,0.82 |
| Label | Label30 | ลำดับที่สาขา 740001 |  |  | 15.30,1.50 |
| Image | Picture |  |  |  | 0.00,1.60 |
| Label | OptionLabel1 |  |  | OnClick→=HandleButtonClick(1) | 6.10,2.30 |
| CommandButton | Option1 |  |  | OnClick→=HandleButtonClick(1) | 5.30,2.49 |
| Label | OptionLabel2 |  | ซ่อน | OnClick→=HandleButtonClick(2) | 6.10,3.30 |
| Label | Label27 | พนักงาน |  |  | 0.20,3.40 |
| CommandButton | Option2 |  | ซ่อน | OnClick→=HandleButtonClick(2) | 5.30,3.44 |
| Label | optionLabel3 |  | ซ่อน | OnClick→=HandleButtonClick(2) | 6.10,4.30 |
| CommandButton | Option3 |  | ซ่อน | OnClick→=HandleButtonClick(3) | 5.30,4.40 |
| Label | Label31 | เลขที่บัญชี ธนาคารทหารไทย   118-1-05201-8 |  |  | 0.10,5.10 |
| Label | OptionLabel4 |  | ซ่อน | OnClick→=HandleButtonClick(4) | 6.10,5.20 |
| CommandButton | Option4 |  | ซ่อน | OnClick→=HandleButtonClick(4) | 5.30,5.34 |
| Label | OptionLabel5 |  | ซ่อน | OnClick→=HandleButtonClick(5) | 6.10,6.30 |
| CommandButton | Option5 |  | ซ่อน | OnClick→=HandleButtonClick(5) | 5.30,6.40 |
| Label | Label32 | ทุกสิ้นเดือนต้องตรวจสอบพนักงานที่ลาออกใส่วันที่ลาออกให้เรีบยร้อย |  |  | 0.10,6.60 |
| Label | OptionLabel6 |  | ซ่อน | OnClick→=HandleButtonClick(6) | 6.10,7.40 |
| CommandButton | Option6 |  | ซ่อน | OnClick→=HandleButtonClick(6) | 5.30,7.50 |
| Label | OptionLabel7 |  | ซ่อน | OnClick→=HandleButtonClick(7) | 6.10,8.50 |
| CommandButton | Option7 |  | ซ่อน | OnClick→=HandleButtonClick(7) | 5.30,8.58 |
| Label | Label33 | ไม่รับพนักงานเข้าทำงานตั้งแต่วันที่ 27 จนถึงสิ้นเดือนให้รับเข้าทำงานในเดือนถัดไป เพราะจะมีปัญหากับประกันสังคม รายเดือนรับวันที่ 12 |  |  | 0.00,8.68 |
| Label | OptionLabel8 |  | ซ่อน | OnClick→=HandleButtonClick(8) | 6.10,9.50 |
| CommandButton | Option8 |  | ซ่อน | OnClick→=HandleButtonClick(8) | 5.30,9.60 |

## VBA (Code-behind)

```vb
Attribute VB_GlobalNameSpace = False

Attribute VB_Creatable = True

Attribute VB_PredeclaredId = True

Attribute VB_Exposed = False

Option Compare Database



Private Sub Form_Open(Cancel As Integer)

' Minimize the database window and initialize the form.



    ' Move to the switchboard page that is marked as the default.

    Me.Filter = "[ItemNumber] = 0 AND [Argument] = 'Default' "

    Me.FilterOn = True

    

End Sub



Private Sub Form_Current()

' Update the caption and fill in the list of options.



    Me.Caption = Nz(Me![ItemText], "")

    FillOptions

    

End Sub



Private Sub FillOptions()

' Fill in the options for this switchboard page.



    ' The number of buttons on the form.

    Const conNumButtons = 8

    

    Dim con As Object

    Dim rs As Object

    Dim stSql As String

    Dim intOption As Integer

    

    ' Set the focus to the first button on the form,

    ' and then hide all of the buttons on the form

    ' but the first.  You can't hide the field with the focus.

    Me![Option1].SetFocus

    For intOption = 2 To conNumButtons

        Me("Option" & intOption).Visible = False

        Me("OptionLabel" & intOption).Visible = False

    Next intOption

    

    ' Open the table of Switchboard Items, and find

    ' the first item for this Switchboard Page.

    Set con = Application.CurrentProject.Connection

    stSql = "SELECT * FROM [Switchboard Items]"

    stSql = stSql & " WHERE [ItemNumber] > 0 AND [SwitchboardID]=" & Me![SwitchboardID]

    stSql = stSql & " ORDER BY [ItemNumber];"

    Set rs = CreateObject("ADODB.Recordset")

    rs.Open stSql, con, 1   ' 1 = adOpenKeyset

    

    ' If there are no options for this Switchboard Page,

    ' display a message.  Otherwise, fill the page with the items.

    If (rs.EOF) Then

        Me![OptionLabel1].Caption = "ไม่มีรายการใดๆ สำหรับหน้าสวิตช์บอร์ดนี้"

    Else

        While (Not (rs.EOF))

            Me("Option" & rs![ItemNumber]).Visible = True

            Me("OptionLabel" & rs![ItemNumber]).Visible = True

            Me("OptionLabel" & rs![ItemNumber]).Caption = rs![ItemText]

            rs.MoveNext

        Wend

    End If



    ' Close the recordset and the database.

    rs.Close

    Set rs = Nothing

    Set con = Nothing



End Sub



Private Function HandleButtonClick(intBtn As Integer)

' This function is called when a button is clicked.

' intBtn indicates which button was clicked.



    ' Constants for the commands that can be executed.

    Const conCmdGotoSwitchboard = 1

    Const conCmdOpenFormAdd = 2

    Const conCmdOpenFormBrowse = 3

    Const conCmdOpenReport = 4

    Const conCmdCustomizeSwitchboard = 5

    Const conCmdExitApplication = 6

    Const conCmdRunMacro = 7

    Const conCmdRunCode = 8

    Const conCmdOpenPage = 9



    ' An error that is special cased.

    Const conErrDoCmdCancelled = 2501

    

    Dim con As Object

    Dim rs As Object

    Dim stSql As String



On Error GoTo HandleButtonClick_Err



    ' Find the item in the Switchboard Items table

    ' that corresponds to the button that was clicked.

    Set con = Application.CurrentProject.Connection

    Set rs = CreateObject("ADODB.Recordset")

    stSql = "SELECT * FROM [Switchboard Items] "

    stSql = stSql & "WHERE [SwitchboardID]=" & Me![SwitchboardID] & " AND [ItemNumber]=" & intBtn

    rs.Open stSql, con, 1    ' 1 = adOpenKeyset

    

    ' If no item matches, report the error and exit the function.

    If (rs.EOF) Then

        MsgBox "มีข้อผิดพลาดเกิดขึ้นขณะที่อ่านตารางรายการสวิตช์บอร์ด"

        rs.Close

        Set rs = Nothing

        Set con = Nothing

        Exit Function

    End If

    

    Select Case rs![Command]

        

        ' Go to another switchboard.

        Case conCmdGotoSwitchboard

            Me.Filter = "[ItemNumber] = 0 AND [SwitchboardID]=" & rs![Argument]

            

        ' Open a form in Add mode.

        Case conCmdOpenFormAdd

            DoCmd.OpenForm rs![Argument], , , , acAdd



        ' Open a form.

        Case conCmdOpenFormBrowse

            DoCmd.OpenForm rs![Argument]



        ' Open a report.

        Case conCmdOpenReport

            DoCmd.OpenReport rs![Argument], acPreview



        ' Customize the Switchboard.

        Case conCmdCustomizeSwitchboard

            ' Handle the case where the Switchboard Manager

            ' is not installed (e.g. Minimal Install).

            On Error Resume Next

            Application.Run "ACWZMAIN.sbm_Entry"

            If (Err <> 0) Then MsgBox "ไม่มีคำสั่งให้ใช้"

            On Error GoTo 0

            ' Update the form.

            Me.Filter = "[ItemNumber] = 0 AND [Argument] = 'Default' "

            Me.Caption = Nz(Me![ItemText], "")

            FillOptions



        ' Exit the application.

        Case conCmdExitApplication

            CloseCurrentDatabase



        ' Run a macro.

        Case conCmdRunMacro

            DoCmd.RunMacro rs![Argument]



        ' Run code.

        Case conCmdRunCode

            Application.Run rs![Argument]



        ' Open a Data Access Page

        Case conCmdOpenPage

            DoCmd.OpenDataAccessPage rs![Argument]



        ' Any other command is unrecognized.

        Case Else

            MsgBox "เป็นตัวเลือกที่ไม่รู้จัก"

    

    End Select



    ' Close the recordset and the database.

    rs.Close

    

HandleButtonClick_Exit:

On Error Resume Next

    Set rs = Nothing

    Set con = Nothing

    Exit Function



HandleButtonClick_Err:

    ' If the action was cancelled by the user for

    ' some reason, don't display an error message.

    ' Instead, resume on the next line.

    If (Err = conErrDoCmdCancelled) Then

        Resume Next

    Else

        MsgBox "มีข้อผิดพลาดเกิดขึ้นขณะดำเนินการคำสั่งอยู่", vbCritical

        Resume HandleButtonClick_Exit

    End If

    

End Function
```

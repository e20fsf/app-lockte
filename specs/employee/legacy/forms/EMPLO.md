# ฟอร์ม: EMPLO

- เรียกจาก: เมนู 2.1 เพิ่มข้อมูลพนักงาน (Switchboard)
- สร้าง 2021-07-07 · แก้ล่าสุด 2025-11-18
- RecordSource: `EMPLO` → [EMPLO](<../tables.md#t-EMPLO>)
- OrderBy: EMPLO.ID
- OrderByOn: NotDefault
- Caption: EMPLO
- DefaultView: Single Form
- DataEntry: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 1.80 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label40 | เพิ่มข้อมูลพนักงาน |  |  | 6.40,0.50 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 12.90 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | ID Label | รหัส |  |  | 2.00,0.10 |
| TextBox | ID | ID | ControlSource: `ID` | AfterUpdate→[Event Procedure] | 4.62,0.10 |
| Label | Label64 | ชื่อไทย |  |  | 6.40,0.10 |
| TextBox | Text63 | NAME | ControlSource: `NAME` |  | 7.62,0.10 |
| Label | Label66 | คำนำหน้า |  |  | 2.90,0.80 |
| ComboBox | Combo65 | NAME TITLE | ControlSource: `NAME TITLE`; RowSourceType: `Table/Query`; RowSource: `SELECT TITLE.TITLE, TITLE.[TITLE THAI] FROM TITLE; `; ColumnCount: `2`; ColumnWidths: `567;1134` |  | 4.61,0.80 |
| Label | NAME Label | ชื่ออังกฤษ |  |  | 5.00,0.80 |
| TextBox | NAME | NAME ENGLISH | ControlSource: `NAME ENGLISH` |  | 7.62,0.80 |
| Label | SEX Label | เพศ |  |  | 3.89,1.50 |
| ComboBox | SEX | SEX | ControlSource: `SEX`; RowSourceType: `Table/Query`; RowSource: `SELECT เพศ.SEX, เพศ.THAI FROM เพศ ORDER BY เพศ.SEX; `; ColumnCount: `2`; ColumnWidths: `284;567` |  | 4.61,1.50 |
| TextBox | SALARY | SALARY | ControlSource: `SALARY`; Format: `Standard`; DecimalPlaces: `2` | BeforeUpdate→[Event Procedure]; AfterUpdate→[Event Procedure] | 12.40,1.50 |
| Label | Label60 | สัญชาติ |  |  | 6.40,1.50 |
| TextBox | Combo59 | NATION | ControlSource: `NATION` |  | 7.62,1.50 |
| Label | SALARY Label | เงินเดือน |  |  | 11.00,1.50 |
| Label | POSITION Label | ตำแหน่ง |  |  | 6.00,2.20 |
| ComboBox | POSITION | POSITION | ControlSource: `POSITION`; RowSourceType: `Table/Query`; RowSource: `ตำแหน่ง` |  | 7.62,2.20 |
| Label | SA_DAY Label | ค่าแรง/วัน |  |  | 9.80,2.20 |
| TextBox | SA_DAY | SA_DAY | ControlSource: `SA_DAY`; Format: `Standard`; DecimalPlaces: `2` |  | 12.42,2.20 |
| Label | BORN Label | วันเกิด |  |  | 2.01,2.20 |
| TextBox | BORN | BORN | ControlSource: `BORN`; Format: `Short Date`; ขยายได้ |  | 4.61,2.20 |
| Label | ID_CODE Label | เลขที่บัตรประชาชน |  |  | 1.69,2.89 |
| TextBox | ID_CODE | ID_CODE | ControlSource: `ID_CODE` | AfterUpdate→[Event Procedure] | 4.61,2.89 |
| Label | CLAS Label | ประเภท |  |  | 5.80,2.90 |
| ComboBox | CLAS | CLAS | ControlSource: `CLAS`; RowSourceType: `Table/Query`; RowSource: `SELECT [ประเภทพนักงาน].TYPE, [ประเภทพนักงาน].EXPER FROM ประเภทพนักงาน ORDER BY [ประเภทพนักงาน].TYPE; `; ColumnCount: `2`; ColumnWidths: `286;2268` |  | 8.42,2.90 |
| Label | ID_S Label | เลขประกันสังคม |  |  | 9.80,2.90 |
| TextBox | ID_S | ID_S | ControlSource: `ID_S` |  | 12.41,2.91 |
| Label | PER_ADD Label | ที่อยู่ตามบัตรประชาชน |  |  | 1.20,3.60 |
| TextBox | PER_ADD | PER_ADD | ControlSource: `PER_ADD` |  | 4.61,3.60 |
| Label | Label50 | ออกให้ ณ เขต |  |  | 1.69,4.28 |
| TextBox | ID_PLACE | ID_PLACE | ControlSource: `ID_PLACE` |  | 4.61,4.28 |
| TextBox | ACC | ACC | ControlSource: `ACC` |  | 9.62,4.28 |
| Label | ACC Label | เลขบัญชี1 |  |  | 8.19,4.30 |
| Label | Label62 | เลขบัญชี2 |  |  | 11.80,4.30 |
| TextBox | Text61 | ACC2 | ControlSource: `ACC2` |  | 13.30,4.30 |
| Label | Label51 | เมื่อวันที่ |  |  | 3.20,4.98 |
| TextBox | ID_DATE | ID_DATE | ControlSource: `ID_DATE`; Format: `Short Date` |  | 4.61,4.98 |
| TextBox | SHIFT | SHIFT | ControlSource: `SHIFT` |  | 13.30,5.00 |
| Label | SHIFT Label | กะ |  |  | 12.00,5.02 |
| Label | ADDRESS Label | ที่อยู่ปัจจุบัน |  |  | 1.99,5.68 |
| TextBox | ADDRESS | ADDRESS | ControlSource: `ADDRESS` |  | 4.61,5.68 |
| Label | Label44 | ค่าร้อน |  |  | 7.90,6.40 |
| ComboBox | HOT | HOT1 | ControlSource: `HOT1`; RowSourceType: `Table/Query`; RowSource: `YES,NO`; ColumnCount: `2`; ColumnWidths: `284;284`; DefaultValue: `"N"` |  | 9.76,6.40 |
| Label | START Label | วันเริ่มทำงาน |  |  | 1.99,6.41 |
| TextBox | START | START | ControlSource: `START`; Format: `Short Date` |  | 4.61,6.41 |
| Label | Label45 | ลำดับที่ |  |  | 3.50,7.10 |
| TextBox | NO | NO | ControlSource: `NO` |  | 4.62,7.10 |
| Label | Label49 | ฝ่าย |  |  | 6.20,7.10 |
| ComboBox | SECTION | SECTION | ControlSource: `SECTION`; RowSourceType: `Table/Query`; RowSource: `ฝ่าย` |  | 6.96,7.10 |
| Label | Label68 | เบอร์โทรศัพท์ |  |  | 11.22,7.10 |
| TextBox | Text67 | TELEPHONE | ControlSource: `TELEPHONE` |  | 13.17,7.10 |
| Label | Label46 | โรงพยาบาล |  |  | 7.98,7.80 |
| ComboBox | HOSPITAL | HOSPITAL | ControlSource: `HOSPITAL`; RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCT [โรงพยาบาลที่เลือก].HOSPITAL FROM โรงพยาบาลที่เลือก WHERE ((([โรงพยาบาลที่เลือก].HOSPITAL) Is Not Null)) ORDER BY [โรงพยาบาลที่เลือก].HOSPITAL; ` |  | 9.76,7.80 |
| Label | POINT Label |  วุฒิ |  |  | 3.90,7.80 |
| ComboBox | POINT | POINT | ControlSource: `POINT`; RowSourceType: `Table/Query`; RowSource: `วุฒิการศึกษา11` |  | 4.62,7.80 |
| Label | DEPARTMENT Label | แผนก |  |  | 2.00,8.50 |
| ComboBox | DEPARTMENT | DEPARTMENT | ControlSource: `DEPARTMENT`; RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCT แผนก.DEPART, แผนก.SECTION FROM แผนก ORDER BY แผนก.DEPART; `; ColumnCount: `2`; ColumnWidths: `2835;2835` | BeforeUpdate→[Event Procedure]; AfterUpdate→[Event Procedure] | 4.62,8.50 |
| Label | Label58 | หน้าที่ |  |  | 8.69,8.60 |
| ComboBox | Text57 | WORK | ControlSource: `WORK`; RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCT EMPLO.WORK FROM EMPLO WHERE (((EMPLO.WORK) Is Not Null)) ORDER BY EMPLO.WORK; `; Format: `Short Date` |  | 9.80,8.60 |
| CommandButton | คำสั่ง56 | พิมพ์แบบ คร2 |  | OnClick→[Event Procedure] | 1.20,9.60 |
| CommandButton | คำสั่ง54 | พิมพ์สัญญาจ้างรายวัน |  | OnClick→[Event Procedure] | 3.70,9.60 |
| CommandButton | คำสั่ง53 | พิมพ์สัญญาจ้างรายเดือน |  | OnClick→[Event Procedure] | 6.80,9.60 |
| CommandButton | คำสั่ง42 | รายการต่อไป |  | OnClick→[Event Procedure] | 13.40,9.60 |
| CommandButton | คำสั่ง41 | กลับเมนูเดิม |  | OnClick→ปรับปรุงข้อมูลพนักงาน | 16.50,9.60 |
| CommandButton | คำสั่ง52 | พิมพ์ฟอร์มการรับพนักงาน |  | OnClick→[Event Procedure] | 10.10,9.61 |

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





Private Sub DEPARTMENT_AfterUpdate()

Me!SECTION = Me!DEPARTMENT.Column(1)

End Sub



Private Sub DEPARTMENT_BeforeUpdate(Cancel As Integer)



End Sub



Private Sub ID_AfterUpdate()

ID = UCase([ID])

End Sub







Private Sub ID_CODE_AfterUpdate()

Me!ID_S = Me!ID_CODE

End Sub



Private Sub SALARY_AfterUpdate()

SA_DAY = Round(SALARY / 30, 2)

End Sub



Private Sub SALARY_BeforeUpdate(Cancel As Integer)



End Sub



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

Private Sub คำสั่ง52_Click()

On Error GoTo Err_คำสั่ง52_Click



    Dim stDocName As String

    Dim stLinkCriteria As String



    stDocName = "แบบฟอร์มการรับพนักงาน"

    DoCmd.OpenForm stDocName, , , stLinkCriteria



Exit_คำสั่ง52_Click:

    Exit Sub



Err_คำสั่ง52_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง52_Click

    

End Sub

Private Sub คำสั่ง53_Click()

On Error GoTo Err_คำสั่ง53_Click



    Dim stDocName As String

    Dim stLinkCriteria As String



    stDocName = "แบบฟอร์มการรับพนักงาน1"

    DoCmd.OpenForm stDocName, , , stLinkCriteria



Exit_คำสั่ง53_Click:

    Exit Sub



Err_คำสั่ง53_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง53_Click

    

End Sub

Private Sub คำสั่ง54_Click()

On Error GoTo Err_คำสั่ง54_Click



    Dim stDocName As String

    Dim stLinkCriteria As String



    stDocName = "แบบฟอร์มการรับพนักงาน2"

    DoCmd.OpenForm stDocName, , , stLinkCriteria



Exit_คำสั่ง54_Click:

    Exit Sub



Err_คำสั่ง54_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง54_Click

    

End Sub

Private Sub คำสั่ง56_Click()

On Error GoTo Err_คำสั่ง56_Click



    Dim stDocName As String

    Dim stLinkCriteria As String



    stDocName = "แบบ คร2"

    DoCmd.OpenForm stDocName, , , stLinkCriteria



Exit_คำสั่ง56_Click:

    Exit Sub



Err_คำสั่ง56_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง56_Click

    

End Sub
```

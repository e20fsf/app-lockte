# ฟอร์ม: EMPLO2

- เรียกจาก: ฟอร์ม [EMPLO1](<../forms/EMPLO1.md>) (Subform); ฟอร์ม [EMPLO4](<../forms/EMPLO4.md>) (Subform)
- สร้าง 2021-07-07 · แก้ล่าสุด 2025-11-19
- RecordSource: `EMPLO` → [EMPLO](<../tables.md#t-EMPLO>)
- OrderBy: EMPLO.ID
- OrderByOn: NotDefault
- Caption: EMPLO
- DefaultView: Single Form
- AllowAdditions: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน Section `ส่วนรายละเอียด` (สูง 9.40 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | ID Label | รหัส |  |  | 2.52,0.00 |
| TextBox | ID | ID | ControlSource: `ID` |  | 3.42,0.00 |
| Label | Label66 | ชื่อไทย |  |  | 4.90,0.00 |
| TextBox | Text65 | NAME | ControlSource: `NAME` |  | 6.09,0.00 |
| Label | Label60 | ไม่ส่งประกันสังคม |  |  | 0.00,0.20 |
| Label | Label68 | คำนำหน้า |  |  | 1.80,0.70 |
| ComboBox | Combo67 | NAME TITLE | ControlSource: `NAME TITLE`; RowSourceType: `Table/Query`; RowSource: `SELECT TITLE.TITLE, TITLE.[TITLE THAI] FROM TITLE; `; ColumnCount: `2`; ColumnWidths: `567;1134` |  | 3.42,0.70 |
| Label | NAME Label | ชื่ออังกฤษ |  |  | 4.60,0.70 |
| TextBox | NAME | NAME ENGLISH | ControlSource: `NAME ENGLISH` |  | 6.08,0.70 |
| TextBox | GRED_1 | GRED_1 | ControlSource: `GRED_1` |  | 0.10,0.80 |
| Label | SEX Label | เพศ |  |  | 2.69,1.40 |
| ComboBox | SEX | SEX | ControlSource: `SEX`; RowSourceType: `Table/Query`; RowSource: `SELECT เพศ.SEX, เพศ.THAI FROM เพศ ORDER BY เพศ.SEX; `; ColumnCount: `2`; ColumnWidths: `284;567` |  | 3.41,1.40 |
| Label | Label62 | สัญชาติ |  |  | 5.20,1.40 |
| TextBox | Text61 | NATION | ControlSource: `NATION` |  | 6.57,1.40 |
| Label | SALARY Label | เงินเดือน |  |  | 9.81,1.40 |
| TextBox | SALARY | SALARY | ControlSource: `SALARY`; Format: `Standard`; DecimalPlaces: `2` | BeforeUpdate→[Event Procedure]; AfterUpdate→[Event Procedure] | 11.22,1.40 |
| Label | POSITION Label | ตำแหน่ง |  |  | 5.22,2.10 |
| ComboBox | POSITION | POSITION | ControlSource: `POSITION`; RowSourceType: `Table/Query`; RowSource: `ตำแหน่ง` |  | 6.62,2.10 |
| Label | SA_DAY Label | ค่าแรง/วัน |  |  | 8.60,2.10 |
| TextBox | SA_DAY | SA_DAY | ControlSource: `SA_DAY`; Format: `Standard`; DecimalPlaces: `2` |  | 11.22,2.10 |
| Label | BORN Label | วันเกิด |  |  | 0.81,2.10 |
| TextBox | BORN | BORN | ControlSource: `BORN`; Format: `Short Date`; ขยายได้ |  | 3.41,2.10 |
| Label | ID_CODE Label | เลขที่บัตรประชาชน |  |  | 0.49,2.80 |
| TextBox | ID_CODE | ID_CODE | ControlSource: `ID_CODE` |  | 3.41,2.80 |
| Label | CLAS Label | ประเภท |  |  | 5.99,2.80 |
| ComboBox | CLAS | CLAS | ControlSource: `CLAS`; RowSourceType: `Table/Query`; RowSource: `SELECT [ประเภทพนักงาน].TYPE, [ประเภทพนักงาน].EXPER FROM ประเภทพนักงาน ORDER BY [ประเภทพนักงาน].TYPE; `; ColumnCount: `2`; ColumnWidths: `286;2268` |  | 7.32,2.80 |
| Label | ID_S Label | เลขประกันสังคม |  |  | 8.60,2.80 |
| TextBox | ID_S | ID_S | ControlSource: `ID_S` |  | 11.21,2.81 |
| Label | PER_ADD Label | ที่อยู่ตามบัตรประชาชน |  |  | 0.00,3.50 |
| TextBox | PER_ADD | PER_ADD | ControlSource: `PER_ADD` |  | 3.41,3.50 |
| Label | Label50 | ออกให้ ณ เขต |  |  | 0.49,4.18 |
| TextBox | ID_PLACE | ID_PLACE | ControlSource: `ID_PLACE` |  | 3.41,4.18 |
| Label | ACC Label | เลขบัญชี1 |  |  | 7.02,4.20 |
| Label | Label64 | เลขบัญชี2 |  |  | 10.61,4.20 |
| TextBox | ACC | ACC | ControlSource: `ACC` |  | 8.41,4.23 |
| TextBox | Text63 | ACC2 | ControlSource: `ACC2` |  | 12.11,4.23 |
| Label | Label51 | เมื่อวันที่ |  |  | 2.01,4.88 |
| TextBox | ID_DATE | ID_DATE | ControlSource: `ID_DATE`; Format: `Short Date` |  | 3.41,4.88 |
| Label | Label70 | ค่าแรงไม่รวมOTที่ได้วิคนี้ |  |  | 5.11,4.90 |
| TextBox | Text69 | POINT_1 | ControlSource: `POINT_1`; Format: `Standard`; DecimalPlaces: `0` |  | 8.89,4.90 |
| Label | SHIFT Label | กะ |  |  | 10.70,4.90 |
| TextBox | SHIFT | SHIFT | ControlSource: `SHIFT` |  | 12.11,4.90 |
| Label | ADDRESS Label | ที่อยู่ปัจจุบัน |  |  | 0.79,5.58 |
| TextBox | ADDRESS | ADDRESS | ControlSource: `ADDRESS` |  | 3.41,5.58 |
| TextBox | RESIGN | RESIGN | ControlSource: `RESIGN`; Format: `Short Date` |  | 11.70,6.30 |
| Label | Label44 | ค่าร้อน |  |  | 6.70,6.30 |
| ComboBox | HOT | HOT1 | ControlSource: `HOT1`; RowSourceType: `Table/Query`; RowSource: `YES,NO`; ColumnCount: `2`; ColumnWidths: `284;284` |  | 8.56,6.30 |
| Label | START Label | วันเริ่มทำงาน |  |  | 0.79,6.31 |
| TextBox | START | START | ControlSource: `START`; Format: `Short Date` |  | 3.41,6.31 |
| Label | Label52 | วันที่ลาออก |  |  | 9.90,6.34 |
| Label | Label45 | ลำดับที่ |  |  | 2.30,7.00 |
| TextBox | NO | NO | ControlSource: `NO` |  | 3.42,7.00 |
| Label | Label49 | ฝ่าย |  |  | 4.80,7.00 |
| ComboBox | SECTION | SECTION | ControlSource: `SECTION`; RowSourceType: `Table/Query`; RowSource: `ฝ่าย` |  | 5.74,7.00 |
| Label | Label72 | เบอร์โทรศัพท์ |  |  | 9.99,7.00 |
| TextBox | Text71 | TELEPHONE | ControlSource: `TELEPHONE` |  | 12.10,7.00 |
| Label | Label46 | โรงพยาบาล |  |  | 6.70,7.70 |
| ComboBox | HOSPITAL | HOSPITAL | ControlSource: `HOSPITAL`; RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCT [โรงพยาบาลที่เลือก].HOSPITAL FROM โรงพยาบาลที่เลือก WHERE ((([โรงพยาบาลที่เลือก].HOSPITAL) Is Not Null)) ORDER BY [โรงพยาบาลที่เลือก].HOSPITAL; ` |  | 8.56,7.70 |
| Label | POINT Label |  วุฒิ |  |  | 2.70,7.70 |
| ComboBox | POINT | POINT | ControlSource: `POINT`; RowSourceType: `Table/Query`; RowSource: `วุฒิการศึกษา11` |  | 3.42,7.70 |
| Label | Label58 | หน้าที่ |  |  | 7.30,8.40 |
| ComboBox | WORK | WORK | ControlSource: `WORK`; RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCT EMPLO.WORK FROM EMPLO WHERE (((EMPLO.WORK) Is Not Null)) ORDER BY EMPLO.WORK; ` |  | 8.56,8.40 |
| Label | DEPARTMENT Label | แผนก |  |  | 0.80,8.40 |
| ComboBox | DEPARTMENT | DEPARTMENT | ControlSource: `DEPARTMENT`; RowSourceType: `Table/Query`; RowSource: `SELECT DISTINCT แผนก.DEPART FROM แผนก ORDER BY แผนก.DEPART; ` |  | 3.42,8.40 |

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















Private Sub SALARY_AfterUpdate()

SA_DAY = SALARY / 30

End Sub



Private Sub SALARY_BeforeUpdate(Cancel As Integer)



End Sub
```

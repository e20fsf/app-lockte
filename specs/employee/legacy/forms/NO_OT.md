# ฟอร์ม: NO_OT

- เรียกจาก: ฟอร์ม [วันทำงาน](<../forms/วันทำงาน.md>) (VBA)
- สร้าง 2021-05-12 · แก้ล่าสุด 2024-04-10
- RecordSource: `NO_OT` → [NO_OT](<../tables.md#t-NO_OT>)
- Caption: NO_OT
- DefaultView: Single Form
- DataEntry: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน Section `ส่วนรายละเอียด` (สูง 14.80 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label2 | บันทึกพนักงานที่ไม่ทำ OT |  |  | 5.50,0.10 |
| Label | DATE1 Label | วันที่ |  |  | 4.30,1.70 |
| TextBox | DATE1 | DATE1 | ControlSource: `DATE1`; Format: `Short Date` |  | 5.25,1.70 |
| Subform | ลูก3 |  | SourceObject: `Form.NO_OT1`; LinkChildFields: `date1;id1`; LinkMasterFields: `date1;id1` |  | 8.20,1.70 |
| CommandButton | คำสั่ง6 | กลับเมนูเดิม |  | OnClick→[Event Procedure] | 11.50,8.79 |
| CommandButton | คำสั่ง5 | สร้างเวลาทำ OTทันที |  | OnClick→แมโคร3 | 6.20,8.80 |

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



Private Sub คำสั่ง6_Click()

On Error GoTo Err_คำสั่ง6_Click





    DoCmd.Close



Exit_คำสั่ง6_Click:

    Exit Sub



Err_คำสั่ง6_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง6_Click

    

End Sub
```

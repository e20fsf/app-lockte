# ฟอร์ม: SUNDAY

- เรียกจาก: มาโคร [SUNDAY](<../macros.md#m-SUNDAY>) (OpenForm); มาโคร [ลบวันอาทิตย์](<../macros.md#m-ลบวันอาทิตย์>) (OpenForm); มาโคร [ลบวันอาทิตย์1](<../macros.md#m-ลบวันอาทิตย์1>) (OpenForm); เมนู 7.3 เพิ่มและแก้ไขวันอาทิตย์ (Switchboard)
- สร้าง 2021-05-12 · แก้ล่าสุด 2021-07-26
- RecordSource: `SUNDAY` → [SUNDAY](<../tables.md#t-SUNDAY>)
- Caption: SUNDAY
- RecordSelectors: NotDefault
- ScrollBars: 2

## ส่วน FormHeader `FormHeader` (สูง 1.72 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label4 | เพิ่มและแก้ไขวันอาทิตย์ |  |  | 4.90,0.00 |
| Label | DATE Label | วันที่ |  |  | 6.90,1.30 |

## ส่วน Section `ส่วนรายละเอียด` (สูง 0.50 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| TextBox | DATE | DATE | ControlSource: `DATE`; Format: `Short Date` |  | 6.90,0.00 |
| TextBox | ITEM | ITEM | ControlSource: `ITEM` |  | 8.69,0.00 |

## ส่วน FormFooter `FormFooter` (สูง 2.65 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| CommandButton | คำสั่ง6 | เพิ่ม |  | OnClick→SUNDAY | 5.00,0.00 |
| CommandButton | Command7 | ลบ |  | OnClickEmMacro→ ⏎ Version =196611 ⏎  ⏎ ColumnsShown =0 ⏎  ⏎  ⏎ Action ="OpenForm" ⏎  ⏎ Argument ="ลบวันอาทิตย์" ⏎  ⏎ Argument ="0" ⏎  ⏎ Argument ="" ⏎  ⏎ Argument ="" ⏎  ⏎ Argument ="1" ⏎  ⏎ Argument ="0" ⏎  ⏎  ⏎  ⏎ Comment ="_AXL:<?xml version=\"1.0\" encoding=\"UTF-16\" standalone=\"no\"?>\015\012<UserI" ⏎  ⏎ "nterfaceMacro For=\"Command7\" Event=\"OnClick\" xmlns=\"http://schemas.microsof" ⏎  ⏎ "t.com/office/accessservices/2009/11/application\"><Statements><Action Name=\"Ope" ⏎  ⏎ "nForm\"><Argument Name=\"FormName\"" ⏎  ⏎  ⏎  ⏎ Comment ="_AXL:>ลบวันอาทิตย์</Argument><Argument Name=\"DataMode\">Edit</Argument></Action" ⏎  ⏎ "></Statements></UserInterfaceMacro>" ⏎  ⏎ ; OnClickEmMacro (Embedded) | 7.50,0.00 |

Embedded macro `Command7.OnClickEmMacro`:
```

Version =196611

ColumnsShown =0


Action ="OpenForm"

Argument ="ลบวันอาทิตย์"

Argument ="0"

Argument =""

Argument =""

Argument ="1"

Argument ="0"



Comment ="_AXL:<?xml version=\"1.0\" encoding=\"UTF-16\" standalone=\"no\"?>\015\012<UserI"

"nterfaceMacro For=\"Command7\" Event=\"OnClick\" xmlns=\"http://schemas.microsof"

"t.com/office/accessservices/2009/11/application\"><Statements><Action Name=\"Ope"

"nForm\"><Argument Name=\"FormName\""



Comment ="_AXL:>ลบวันอาทิตย์</Argument><Argument Name=\"DataMode\">Edit</Argument></Action"

"></Statements></UserInterfaceMacro>"


```

| CommandButton | คำสั่ง5 | กลับเมนูเดิม |  | OnClick→ลบวันอาทิตย์ | 10.10,0.00 |

## VBA (Code-behind)

```vb
Attribute VB_GlobalNameSpace = False

Attribute VB_Creatable = True

Attribute VB_PredeclaredId = True

Attribute VB_Exposed = False

Option Compare Database

Option Explicit



Private Sub คำสั่ง5_Click()

On Error GoTo Err_คำสั่ง5_Click





    DoCmd.Close



Exit_คำสั่ง5_Click:

    Exit Sub



Err_คำสั่ง5_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง5_Click

    

End Sub
```

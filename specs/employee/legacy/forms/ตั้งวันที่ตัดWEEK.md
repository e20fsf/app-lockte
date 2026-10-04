# ฟอร์ม: ตั้งวันที่ตัดWEEK

- เรียกจาก: มาโคร [คำนวณเวลาทำงาน](<../macros.md#m-คำนวณเวลาทำงาน>) (OpenForm); เมนู 14.1 พิมพ์เวลาทำงานช่วงที่1 (Switchboard); เมนู 15.1 พิมพ์เวลาทำงานช่วงที่2 (Switchboard); เมนู 6.1 พิมพ์เวลาทำงาน (Switchboard)
- สร้าง 2021-05-12 · แก้ล่าสุด 2025-01-13
- RecordSource: `ตั้งวันที่ตัดWEEK` → [ตั้งวันที่ตัดWEEK](<../tables.md#t-ตั้งวันที่ตัดWEEK>)
- Caption: ตั้งวันที่ตัดWEEK
- DefaultView: Single Form
- AllowAdditions: NotDefault
- NavigationButtons: NotDefault
- RecordSelectors: NotDefault
- ScrollBars: 0

## ส่วน FormHeader `FormHeader` (สูง 0.00 ซม.)
(ไม่มี control)

## ส่วน Section `ส่วนรายละเอียด` (สูง 13.50 ซม.)

| ชนิด | ชื่อ | ข้อความ/ที่มา | คุณสมบัติ | เหตุการณ์ | ตำแหน่ง ซ้าย,บน (ซม.) |
|---|---|---|---|---|---|
| Label | Label6 | คำนวณเวลาทำงาน |  |  | 7.79,1.10 |
| Label | Label14 | การกำหนดวันที่ให้ใส่วันที่ที่ตัดวิคจริงเช่น 27-11และ 12-26 ห้ามใส่วันที่อื่น ยกเว้นการตัดวิค 2ช่วง |  |  | 13.40,3.10 |
| Label | FROM_DA Label |               ตั้งแต่วันที่ |  |  | 7.50,3.15 |
| TextBox | FROM_DA | FROM_DA | ControlSource: `FROM_DA`; Format: `Short Date` |  | 10.35,3.30 |
| Label | TO_DA Label |                    ถึงวันที่ |  |  | 7.50,3.85 |
| TextBox | TO_DA | TO_DA | ControlSource: `TO_DA`; Format: `Short Date` |  | 10.35,4.00 |
| Label | PAY_D Label |       วันที่จ่ายค่าแรง |  |  | 7.50,4.55 |
| TextBox | PAY_D | PAY_D | ControlSource: `PAY_D`; Format: `Short Date` |  | 10.35,4.71 |
| Label | Label15 | รายเดือนจ่ายวันที่เหลือจาก 15 วัน |  |  | 2.98,5.40 |
| TextBox | DAY | DAY | ControlSource: `DAY` |  | 10.35,5.40 |
| Label | Label16 | วัน (ปกติไม่ต้องใส่) |  |  | 11.70,5.40 |
| CommandButton | คำสั่ง8 | คำนวณ |  | OnClick→คำนวณเวลาทำงาน | 7.20,6.80 |
| CommandButton | คำสั่ง7 | กลับเมนูเดิม |  | OnClick→[Event Procedure] | 11.60,6.80 |

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



Private Sub คำสั่ง7_Click()

On Error GoTo Err_คำสั่ง7_Click





    DoCmd.Close



Exit_คำสั่ง7_Click:

    Exit Sub



Err_คำสั่ง7_Click:

    MsgBox Err.Description

    Resume Exit_คำสั่ง7_Click

    

End Sub
```

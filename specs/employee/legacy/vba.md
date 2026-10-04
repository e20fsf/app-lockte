# VBA ทั้งหมด (ระบบเดิม)

## โมดูล ThaiNumber

```vb
Option Compare Database

Option Explicit





Public Function GetThaiNumber(InputArabicNumber) As String

 Dim TempInput As Integer

 TempInput = Val(Format$(InputArabicNumber))

 Select Case TempInput

  Case 0: GetThaiNumber = "ศูนย์"

  Case 1: GetThaiNumber = "หนึ่ง"

  Case 2: GetThaiNumber = "สอง"

  Case 3: GetThaiNumber = "สาม"

  Case 4: GetThaiNumber = "สี่"

  Case 5: GetThaiNumber = "ห้า"

  Case 6: GetThaiNumber = "หก"

  Case 7: GetThaiNumber = "เจ็ด"

  Case 8: GetThaiNumber = "แปด"

  Case 9: GetThaiNumber = "เก้า"

End Select

End Function



Public Function GetThaiUnit(ByVal InputArabicNumber) As String

 Dim TempInput  As String

 Dim TempFrontDot  As String

 Dim TempBackDot  As String

 Dim DotPosition As Integer

 Dim LengthFront As Integer

 Dim I As Integer

 Dim TempBackDigit As Integer

 Dim TempOutput As String

 TempOutput = " "

 TempInput = Trim$(Format$(Val(Format$(InputArabicNumber))))

   If Left$(TempInput, 1) <> "-" Then

      DotPosition = InStr(1, TempInput, ".")

   Else

      TempInput = Right$(TempInput, Len(TempInput) - 1)

      DotPosition = InStr(1, TempInput, ".")

      TempOutput = TempOutput & "ลบ"

   End If

   If DotPosition = 0 Then

      TempFrontDot = TempInput

      TempBackDot = " "

   Else

      TempFrontDot = Left$(TempInput, DotPosition - 1)

      TempBackDot = Right$(TempInput, Len(TempInput) - DotPosition)

      If Len(TempBackDot) > 2 Then

         TempBackDot = Left$(TempBackDot, 2)

      End If

   End If

   LengthFront = Len(TempFrontDot)

   If LengthFront <> 0 Then

      For I = 1 To LengthFront Step 1

                   TempBackDigit = (LengthFront - I) Mod 6

                   Select Case TempBackDigit

                        Case 0

                        If Mid$(TempFrontDot, I, 1) <> "0" Then

                           If (I <> 1) And (Mid$(TempFrontDot, I, 1) = "1") Then

                              If Mid$(TempFrontDot, I - 1, 1) <> "0" Then

                                 TempOutput = TempOutput & "เอ็ด"

                              Else

                                 TempOutput = TempOutput & GetThaiNumber(Mid$(TempFrontDot, I, 1))

                              End If

                          Else

                            TempOutput = TempOutput & GetThaiNumber(Mid$(TempFrontDot, I, 1))

                          End If

                        End If

                        

                        If (LengthFront - I) >= 6 Then

                           TempOutput = TempOutput & "ล้าน"

                        End If

                        

                          Case 1

                          If Mid$(TempFrontDot, I, 1) <> "0" Then

                             If Mid$(TempFrontDot, I, 1) = "1" Then

                             Else

                                   If Mid$(TempFrontDot, I, 1) = "2" Then

                                                TempOutput = TempOutput & "ยี่"

                                   Else

                                                TempOutput = TempOutput & GetThaiNumber(Mid$(TempFrontDot, I, 1))

                                   End If

                             End If

                             TempOutput = TempOutput & "สิบ"

                            End If

                            

                            Case 2

                            If Mid$(TempFrontDot, I, 1) <> "0" Then

                                         TempOutput = TempOutput & GetThaiNumber(Mid$(TempFrontDot, I, 1))

                                         TempOutput = TempOutput & "ร้อย"

                            End If

                            

                            Case 3

                            If Mid$(TempFrontDot, I, 1) <> "0" Then

                                         TempOutput = TempOutput & GetThaiNumber(Mid$(TempFrontDot, I, 1))

                                         TempOutput = TempOutput & "พัน"

                            End If

                            

                            Case 4

                            If Mid$(TempFrontDot, I, 1) <> "0" Then

                                         TempOutput = TempOutput & GetThaiNumber(Mid$(TempFrontDot, I, 1))

                                         TempOutput = TempOutput & "หมื่น"

                            End If

                            

                            Case 5

                            If Mid$(TempFrontDot, I, 1) <> "0" Then

                                         TempOutput = TempOutput & GetThaiNumber(Mid$(TempFrontDot, I, 1))

                                         TempOutput = TempOutput & "แสน"

                            End If

                                End Select

                                

      Next I

      If TempOutput <> " " Then

                     TempOutput = TempOutput & "บาท"

      End If

      End If

      If (TempBackDot = " ") Or (Val(TempBackDot) = 0) Then

         If TempOutput <> " " Then

                      TempOutput = TempOutput & "ถ้วน"

         Else

                      TempOutput = "ศูนย์บาทถ้วน"

         End If

       Else

       If Left$(TempBackDot, 1) <> "0" Then

                 If Mid$(TempBackDot, 1, 1) = "1" Then

                    TempOutput = TempOutput & "สิบ"

                 Else

                 If Mid$(TempBackDot, 1, 1) = "2" Then

                    TempOutput = TempOutput & "ยี่สิบ"

                 Else

                    TempOutput = TempOutput & GetThaiNumber(Mid$(TempBackDot, 1, 1)) & "สิบ"

                 End If

                 End If

      End If

      If Len(TempBackDot) = 2 Then

                 If Right$(TempBackDot, 1) <> "0" And Right$(TempBackDot, 1) <> " " Then

                    If Mid$(TempBackDot, 1, 1) <> "0" And Mid$(TempBackDot, 2, 1) = "1" Then

                                 TempOutput = TempOutput & "เอ็ด"

                    Else

                       TempOutput = TempOutput & GetThaiNumber(Mid$(TempBackDot, 2, 1))

                    End If

                  End If

       End If

       TempOutput = TempOutput & "สตางค์"

       End If

       GetThaiUnit = TempOutput

End Function





Public Function CutDecimal(Num As Double) As Double

Dim N As Double

N = Num

CutDecimal = ((((N) * 100) \ 1) / 100)

End Function
```

## Code-behind ของฟอร์ม/รายงาน

| วัตถุ | จำนวนบรรทัด | Sub/Function |
|---|---:|---|
| [EMPLO](<forms/EMPLO.md>) | 259 | DEPARTMENT_AfterUpdate, DEPARTMENT_BeforeUpdate, ID_AfterUpdate, ID_CODE_AfterUpdate, SALARY_AfterUpdate, SALARY_BeforeUpdate, คำส, คำส, คำส, คำส, คำส, คำส |
| [EMPLO1](<forms/EMPLO1.md>) | 151 | คำส, คำส, ผสม43_AfterUpdate, คำส, คำส |
| [EMPLO2](<forms/EMPLO2.md>) | 97 | คำส, คำส, SALARY_AfterUpdate, SALARY_BeforeUpdate |
| [EMPLO3](<forms/EMPLO3.md>) | 127 | ผสม4_AfterUpdate, คำส, คำส, คำส |
| [EMPLO4](<forms/EMPLO4.md>) | 173 | คำส, คำส, คำส, คำส, Command62_Click |
| [E_WORK](<forms/E_WORK.md>) | 69 | คำส, คำส |
| [E_WORK subform1](<forms/E_WORK subform1.md>) | 19 | ID_AfterUpdate |
| [E_WORK1](<forms/E_WORK1.md>) | 11 |  |
| [HOLIDAY](<forms/HOLIDAY.md>) | 41 | คำส |
| [LEAVE](<forms/LEAVE.md>) | 27 | ID_AfterUpdate |
| [LEAVE_A](<forms/LEAVE_A.md>) | 483 | Form_Current, Form_Load, ID_AfterUpdate, SAL_AfterUpdate, Command41_Click, Command63_Click, Command64_Click, Command65_Click, Command66_Click, ส |
| [NO_OT](<forms/NO_OT.md>) | 41 | คำส |
| [NO_WORK](<forms/NO_WORK.md>) | 137 | คำส, คำส, คำส, คำส |
| [NO_WORK2](<forms/NO_WORK2.md>) | 11 |  |
| [OT](<forms/OT.md>) | 97 | คำส, คำส, คำส |
| [OT1](<forms/OT1.md>) | 21 | ID_AfterUpdate |
| [SUNDAY](<forms/SUNDAY.md>) | 41 | คำส |
| [Switchboard](<forms/Switchboard.md>) | 373 | Form_Open, Form_Current, FillOptions, HandleButtonClick |
| [คำนวณพักร้อน](<forms/คำนวณพักร้อน.md>) | 193 | DEP_AfterUpdate, DEP_BeforeUpdate, คำส, คำส, คำส, คำส, คำส |
| [คำนวณโบนัส](<forms/คำนวณโบนัส.md>) | 193 | DEP_AfterUpdate, DEP_BeforeUpdate, คำส, คำส, คำส, คำส, คำส |
| [ตั้งวันที่ตัดWEEK](<forms/ตั้งวันที่ตัดWEEK.md>) | 41 | คำส |
| [ตั้งวันที่ตัดWEEK1](<forms/ตั้งวันที่ตัดWEEK1.md>) | 51 | CUS_AfterUpdate, คำส |
| [ตั้งวันที่พิมพ์ค่าแรงสิ้นเดือน](<forms/ตั้งวันที่พิมพ์ค่าแรงสิ้นเดือน.md>) | 81 | T_D_BeforeUpdate, คำส, Command15_Click |
| [ตั้งวันที่พิมพ์ค่าแรงแต่ละเดือน](<forms/ตั้งวันที่พิมพ์ค่าแรงแต่ละเดือน.md>) | 49 | T_D_AfterUpdate, คำส |
| [ตั้งวันที่พิมพ์รวมค่าแรง](<forms/ตั้งวันที่พิมพ์รวมค่าแรง.md>) | 49 | T_D_AfterUpdate, คำส |
| [ตั้งวันที่พิมพ์สรุปการลางาน](<forms/ตั้งวันที่พิมพ์สรุปการลางาน.md>) | 63 | MO_AfterUpdate, MO_BeforeUpdate, คำส, T_D_AfterUpdate |
| [ตั้งวันที่ย้ายข้อมูลใบเตือน](<forms/ตั้งวันที่ย้ายข้อมูลใบเตือน.md>) | 63 | MO_AfterUpdate, MO_BeforeUpdate, คำส, T_D_AfterUpdate |
| [ตั้งวันที่ลบข้อมูลปีที่แล้ว](<forms/ตั้งวันที่ลบข้อมูลปีที่แล้ว.md>) | 63 | MO_AfterUpdate, MO_BeforeUpdate, คำส, T_D_AfterUpdate |
| [ตั้งวันที่ส่งข้อมูล](<forms/ตั้งวันที่ส่งข้อมูล.md>) | 41 | คำส |
| [ตั้งวันที่ส่งข้อมูล1](<forms/ตั้งวันที่ส่งข้อมูล1.md>) | 81 | DEP_AfterUpdate, DEP_BeforeUpdate, TO_DAY_AfterUpdate, MON_AfterUpdate, MON_BeforeUpdate, คำส |
| [ตั้งวันลา](<forms/ตั้งวันลา.md>) | 75 | คำส, Command6_Click |
| [ทำล่วงเวลาจริง](<forms/ทำล่วงเวลาจริง.md>) | 41 | คำส |
| [บันทึกการประเมินผล](<forms/บันทึกการประเมินผล.md>) | 79 | ID_AfterUpdate, คำส, คำส |
| [บันทึกการประเมินผล1](<forms/บันทึกการประเมินผล1.md>) | 55 | ผสม0_AfterUpdate, คำส |
| [บันทึกการลางาน](<forms/บันทึกการลางาน.md>) | 41 | คำส |
| [ประเมินผลประจำปี](<forms/ประเมินผลประจำปี.md>) | 257 | DEP_AfterUpdate, DEP_BeforeUpdate, คำส, คำส, คำส, คำส, คำส, Command19_Click, Command20_Click |
| [ปรับค่าแรง](<forms/ปรับค่าแรง.md>) | 193 | DEP_AfterUpdate, DEP_BeforeUpdate, คำส, คำส, คำส, คำส, คำส |
| [ย้ายข้อมูลการทำงาน](<forms/ย้ายข้อมูลการทำงาน.md>) | 79 | ID_AfterUpdate, คำส, คำส |
| [ย้ายข้อมูลการทำงานกลับ](<forms/ย้ายข้อมูลการทำงานกลับ.md>) | 71 | Command1_Click, Command2_Click |
| [ย้ายข้อมูลการลางานกลับ](<forms/ย้ายข้อมูลการลางานกลับ.md>) | 71 | Command1_Click, Command2_Click |
| [รหัส1](<forms/รหัส1.md>) | 189 | ID_E_AfterUpdate, คำส, Command7_Click, Command8_Click, Command9_Click, Command10_Click |
| [รหัสพนักงาน](<forms/รหัสพนักงาน.md>) | 41 | คำส |
| [รหัสพนักงาน1](<forms/รหัสพนักงาน1.md>) | 41 | คำส |
| [รหัสพนักงาน2](<forms/รหัสพนักงาน2.md>) | 81 | ID_E_BeforeUpdate, คำส, คำส |
| [ลบข้อมูลพนักงานที่ลาออก](<forms/ลบข้อมูลพนักงานที่ลาออก.md>) | 71 | Command1_Click, Command2_Click |
| [ลบวันอาทิตย์](<forms/ลบวันอาทิตย์.md>) | 193 | DEP_AfterUpdate, DEP_BeforeUpdate, คำส, คำส, คำส, คำส, คำส |
| [ลางานทั้งแผนก](<forms/ลางานทั้งแผนก.md>) | 225 | DEP_AfterUpdate, DEP_BeforeUpdate, คำส, คำส, คำส, คำส, คำส, Command19_Click |
| [ลางานทั้งโรงงาน](<forms/ลางานทั้งโรงงาน.md>) | 91 | DEP_AfterUpdate, DEP_BeforeUpdate, คำส, คำส |
| [วันทำงาน](<forms/วันทำงาน.md>) | 107 | คำส, คำส, คำส |
| [วันทำงาน1](<forms/วันทำงาน1.md>) | 41 | คำส |
| [วันทำงาน2](<forms/วันทำงาน2.md>) | 41 | คำส |
| [วันทำงาน3](<forms/วันทำงาน3.md>) | 41 | คำส |
| [วันทำงาน4](<forms/วันทำงาน4.md>) | 41 | คำส |
| [วันทำงาน5](<forms/วันทำงาน5.md>) | 41 | คำส |
| [วันทำงาน6](<forms/วันทำงาน6.md>) | 41 | คำส |
| [วันทำงาน7](<forms/วันทำงาน7.md>) | 45 | คำส |
| [วันที่ช่วงเวลาทำงาน](<forms/วันที่ช่วงเวลาทำงาน.md>) | 41 | คำส |
| [วันที่ลบการลางาน](<forms/วันที่ลบการลางาน.md>) | 257 | DEP_AfterUpdate, DEP_BeforeUpdate, คำส, คำส, คำส, คำส, คำส, Command19_Click, Command20_Click |
| [สรุปชั่วโมงการทำงาน1](<forms/สรุปชั่วโมงการทำงาน1.md>) | 81 | DEP_AfterUpdate, DEP_BeforeUpdate, TO_DAY_AfterUpdate, MON_AfterUpdate, MON_BeforeUpdate, คำส |
| [สัญญาจ้างแรงงาน](<forms/สัญญาจ้างแรงงาน.md>) | 91 | DEP_AfterUpdate, DEP_BeforeUpdate, คำส, คำส |
| [เกษียณ](<forms/เกษียณ.md>) | 45 | คำส |
| [เตือนย้ายข้อมูลการทำงาน](<forms/เตือนย้ายข้อมูลการทำงาน.md>) | 71 | Command1_Click, Command2_Click |
| [เตือนย้ายข้อมูลการทำงานกลับ](<forms/เตือนย้ายข้อมูลการทำงานกลับ.md>) | 63 | MO_AfterUpdate, MO_BeforeUpdate, คำส, T_D_AfterUpdate |
| [เตือนย้ายข้อมูลการลางาน](<forms/เตือนย้ายข้อมูลการลางาน.md>) | 71 | Command1_Click, Command2_Click |
| [เตือนย้ายข้อมูลการลางานกลับ](<forms/เตือนย้ายข้อมูลการลางานกลับ.md>) | 63 | MO_AfterUpdate, MO_BeforeUpdate, คำส, T_D_AfterUpdate |
| [เตือนย้ายข้อมูลใบเตือน](<forms/เตือนย้ายข้อมูลใบเตือน.md>) | 71 | Command1_Click, Command2_Click |
| [เตือนลบข้อมูลการลางาน](<forms/เตือนลบข้อมูลการลางาน.md>) | 71 | Command1_Click, Command2_Click |
| [เตือนลบข้อมูลปีที่แล้ว](<forms/เตือนลบข้อมูลปีที่แล้ว.md>) | 71 | Command1_Click, Command2_Click |
| [เลือกแผนกที่พิมพ์](<forms/เลือกแผนกที่พิมพ์.md>) | 89 | NAME_DE_AfterUpdate, NAME_DE_BeforeUpdate, คำส, Command8_Click |
| [เลือกแผนกที่พิมพ์1](<forms/เลือกแผนกที่พิมพ์1.md>) | 41 | คำส |
| [เลือกแผนกที่พิมพ์1ช่วงที่1](<forms/เลือกแผนกที่พิมพ์1ช่วงที่1.md>) | 41 | คำส |
| [เลือกแผนกที่พิมพ์1ช่วงที่2](<forms/เลือกแผนกที่พิมพ์1ช่วงที่2.md>) | 41 | คำส |
| [แบบ คร2](<forms/แบบ คร2.md>) | 67 | ID_AfterUpdate, START_AfterUpdate, START_BeforeUpdate, คำส |
| [แบบฟอร์มการรับพนักงาน](<forms/แบบฟอร์มการรับพนักงาน.md>) | 71 | Command1_Click, Command2_Click |
| [แบบฟอร์มการรับพนักงาน1](<forms/แบบฟอร์มการรับพนักงาน1.md>) | 67 | ID_AfterUpdate, START_AfterUpdate, START_BeforeUpdate, คำส |
| [แบบฟอร์มการรับพนักงาน2](<forms/แบบฟอร์มการรับพนักงาน2.md>) | 67 | ID_AfterUpdate, START_AfterUpdate, START_BeforeUpdate, คำส |
| [แผนก2](<forms/แผนก2.md>) | 41 | คำส |
| [ใบอนุมัติบรรจุพนักงาน](<forms/ใบอนุมัติบรรจุพนักงาน.md>) | 71 | Command1_Click, Command2_Click |
| [ใบเตือน](<forms/ใบเตือน.md>) | 159 | CODE_AfterUpdate, ID_AfterUpdate, คำส, คำส, คำส, Command19_Click |
| [ใบเตือน1](<forms/ใบเตือน1.md>) | 33 | CODE_AfterUpdate, ID_AfterUpdate |
| [ใบเตือน2](<forms/ใบเตือน2.md>) | 45 | Command4_Click |
| [พิมพ์ใบลาออก](<reports/พิมพ์ใบลาออก.md>) | 11 |  |

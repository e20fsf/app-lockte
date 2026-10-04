# คิวรีของ flow: Macro1

มาโคร: [Macro1](<../macros.md#m-Macro1>) — ลำดับคิวรีตามขั้นตอนของมาโคร (รวมคิวรี SELECT ที่ถูกอ้างถึง)

<a id="q-จัดข้อมูลการลางาน"></a>
## จัดข้อมูลการลางาน

- ชนิด: **DELETE** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [LEAVE](<../tables.md#t-LEAVE>)
- ใช้โดย: มาโคร [Macro1](<../macros.md#m-Macro1>) (OpenQuery); มาโคร [บันทึกการลางาน](<../macros.md#m-บันทึกการลางาน>) (OpenQuery); มาโคร [ลางานรายคน1](<../macros.md#m-ลางานรายคน1>) (OpenQuery)

```sql
DELETE LEAVE.SUM_D
FROM LEAVE
WHERE (((LEAVE.SUM_D) Is Null));
```

<a id="q-ลางานได้ค่าแรง"></a>
## ลางานได้ค่าแรง

- ชนิด: **UPDATE** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [LEAVE](<../tables.md#t-LEAVE>)
- ใช้โดย: มาโคร [Macro1](<../macros.md#m-Macro1>) (OpenQuery)

```sql
UPDATE LEAVE SET LEAVE.PAY = [sum_d]*[sala_day]
WHERE (((LEAVE.PAY)=0) AND ((LEAVE.SAL)="y"));
```

<a id="q-คัดลอกเวลาลางาน"></a>
## คัดลอกเวลาลางาน

- ชนิด: **DELETE** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [LEAVE_A](<../tables.md#t-LEAVE_A>)
- ใช้โดย: มาโคร [Macro1](<../macros.md#m-Macro1>) (OpenQuery)

```sql
DELETE LEAVE_A.*, *
FROM LEAVE_A;
```

<a id="q-คัดลอกเวลาลางาน1"></a>
## คัดลอกเวลาลางาน1

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [LEAVE_A](<../tables.md#t-LEAVE_A>), [PAY](<../tables.md#t-PAY>)
- ใช้โดย: มาโคร [Macro1](<../macros.md#m-Macro1>) (OpenQuery)

```sql
INSERT INTO LEAVE_A ( [DATE], ID, FROM_D, TO_D, FROM_T, TO_T, SUM_D, SR, SAL, REMARK, MARK, SALA_DAY, PAY, CODE_A )
SELECT LEAVE.Date, LEAVE.ID, LEAVE.FROM_D, LEAVE.TO_D, LEAVE.FROM_T, LEAVE.TO_T, LEAVE.SUM_D, LEAVE.SR, LEAVE.SAL, LEAVE.REMARK, LEAVE.MARK, LEAVE.SALA_DAY, LEAVE.PAY, LEAVE.CODE_A, *
FROM LEAVE;
```

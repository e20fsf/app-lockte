# คิวรีของ flow: SLIPช่วงที่1

มาโคร: [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) — ลำดับคิวรีตามขั้นตอนของมาโคร (รวมคิวรี SELECT ที่ถูกอ้างถึง)

<a id="q-SLIP2ช่วงที่1"></a>
## SLIP2ช่วงที่1

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง2ช่วง](<../queries/SLIPช่วงที่1.md#q-คำนวณค่าแรง2ช่วง>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, SIGN )
SELECT [คำนวณค่าแรง2ช่วง].ID, EMPLO.NAME, [คำนวณค่าแรง2ช่วง].SumOfW, [คำนวณค่าแรง2ช่วง].SumOfL, [คำนวณค่าแรง2ช่วง].SumOfO1, [คำนวณค่าแรง2ช่วง].SumOfS, [คำนวณค่าแรง2ช่วง].SumOfO2, [คำนวณค่าแรง2ช่วง].SumOfH, [คำนวณค่าแรง2ช่วง].SumOfO3, [คำนวณค่าแรง2ช่วง].SumOfHOLI, [คำนวณค่าแรง2ช่วง].SumOfSUN, [คำนวณค่าแรง2ช่วง].Expr2, [คำนวณค่าแรง2ช่วง].Expr3, [คำนวณค่าแรง2ช่วง].Expr4, [คำนวณค่าแรง2ช่วง].Expr5, [คำนวณค่าแรง2ช่วง].Expr6, [คำนวณค่าแรง2ช่วง].Expr7, [คำนวณค่าแรง2ช่วง].PAY_D, [คำนวณค่าแรง2ช่วง].FROM_DA, [คำนวณค่าแรง2ช่วง].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7] AS Expr10, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7] AS Expr11, EMPLO.SALARY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, ตาราง3.Sign
FROM ตาราง3, EMPLO INNER JOIN คำนวณค่าแรง2ช่วง ON EMPLO.ID = [คำนวณค่าแรง2ช่วง].ID
WHERE (((EMPLO.ACC) Is Not Null));
```

<a id="q-คำนวณค่าแรง2ช่วง"></a>
## คำนวณค่าแรง2ช่วง

- ชนิด: **SELECT** · แก้ล่าสุด 2026-01-26
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [คำนวณค่าแรง1](<../queries/SLIP.md#q-คำนวณค่าแรง1>), [ตั้งวันที่ตัดWEEK](<../tables.md#t-ตั้งวันที่ตัดWEEK>)
- ใช้โดย: คิวรี [SLIP2ช่วงที่1](<../queries/SLIPช่วงที่1.md#q-SLIP2ช่วงที่1>) (SQL); คิวรี [SLIP2ช่วงที่2](<../queries/SLIPช่วงที่2.md#q-SLIP2ช่วงที่2>) (SQL); คิวรี [SLIP3ช่วงที่1](<../queries/SLIPช่วงที่1.md#q-SLIP3ช่วงที่1>) (SQL); คิวรี [SLIP3ช่วงที่2](<../queries/SLIPช่วงที่2.md#q-SLIP3ช่วงที่2>) (SQL); คิวรี [คำนวณค่าแรง5ช่วงที่1](<../queries/คำนวณค่าแรงช่วงที่1.md#q-คำนวณค่าแรง5ช่วงที่1>) (SQL); คิวรี [คำนวณค่าแรง5ช่วงที่2](<../queries/คำนวณค่าแรงช่วงที่2.md#q-คำนวณค่าแรง5ช่วงที่2>) (SQL); มาโคร [คำนวณค่าแรงช่วงที่1](<../macros.md#m-คำนวณค่าแรงช่วงที่1>) (OpenQuery); มาโคร [คำนวณค่าแรงช่วงที่2](<../macros.md#m-คำนวณค่าแรงช่วงที่2>) (OpenQuery)

```sql
SELECT [คำนวณค่าแรง1].ID, [คำนวณค่าแรง1].SumOfW, [คำนวณค่าแรง1].SumOfL, [คำนวณค่าแรง1].SumOfO1, [คำนวณค่าแรง1].SumOfS, [คำนวณค่าแรง1].SumOfO2, [คำนวณค่าแรง1].SumOfH, [คำนวณค่าแรง1].SumOfO3, [คำนวณค่าแรง1].FROM_DA, [คำนวณค่าแรง1].TO_DA, [คำนวณค่าแรง1].SumOfHOLI, [คำนวณค่าแรง1].SumOfSUN, Val(Format(IIf(
    [sumofw] + [sumofl] + [sumofholi] + [sumofsun] >= [ตั้งวันที่ตัดWEEK].[Day],
    [ตั้งวันที่ตัดWEEK].[Day] * [sa_day],
    ([sumofw] + [sumofl] + [sumofholi] + [sumofsun]) * [sa_day]
),"#")) AS Expr2, Val(Format([SumOfO1]*[sa_day]/8*1.5,"#")) AS Expr3, Val(Format([sumofs]*[sa_day]/8,"#")) AS Expr4, Val(Format([sumofo2]*[sa_day]/8*3,"#")) AS Expr5, Val(Format([sumofh]*[sa_day]/8,"#")) AS Expr6, Val(Format([sumofo3]*[sa_day]/8*3,"#")) AS Expr7, Val(Format([sumofs]+[sumofo2]+[sumofh]+[sumofo3],"#")) AS Expr8, Val(Format([expr3]+[expr4]+[expr5]+[expr6]+[expr7],"#")) AS Expr9, EMPLO.SALARY, EMPLO.SA_DAY, [คำนวณค่าแรง1].PAY_D, [ตั้งวันที่ตัดWEEK].Day
FROM ตั้งวันที่ตัดWEEK, คำนวณค่าแรง1 INNER JOIN EMPLO ON [คำนวณค่าแรง1].ID = EMPLO.ID
WHERE ((([คำนวณค่าแรง1].CLAS)="1"));
```

<a id="q-SLIP3ช่วงที่1"></a>
## SLIP3ช่วงที่1

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง2ช่วง](<../queries/SLIPช่วงที่1.md#q-คำนวณค่าแรง2ช่วง>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, ATM, TT, NET, SA_D, TT_D, W_D, SIGN )
SELECT [คำนวณค่าแรง2ช่วง].ID, EMPLO.NAME, [คำนวณค่าแรง2ช่วง].SumOfW, [คำนวณค่าแรง2ช่วง].SumOfL, [คำนวณค่าแรง2ช่วง].SumOfO1, [คำนวณค่าแรง2ช่วง].SumOfS, [คำนวณค่าแรง2ช่วง].SumOfO2, [คำนวณค่าแรง2ช่วง].SumOfH, [คำนวณค่าแรง2ช่วง].SumOfO3, [คำนวณค่าแรง2ช่วง].SumOfHOLI, [คำนวณค่าแรง2ช่วง].SumOfSUN, [คำนวณค่าแรง2ช่วง].Expr2, [คำนวณค่าแรง2ช่วง].Expr3, [คำนวณค่าแรง2ช่วง].Expr4, [คำนวณค่าแรง2ช่วง].Expr5, [คำนวณค่าแรง2ช่วง].Expr6, [คำนวณค่าแรง2ช่วง].Expr7, [คำนวณค่าแรง2ช่วง].PAY_D, [คำนวณค่าแรง2ช่วง].FROM_DA, [คำนวณค่าแรง2ช่วง].TO_DA, 0 AS Expr1, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7] AS Expr10, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7] AS Expr11, EMPLO.SALARY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, ตาราง3.Sign
FROM ตาราง3, EMPLO INNER JOIN คำนวณค่าแรง2ช่วง ON EMPLO.ID = [คำนวณค่าแรง2ช่วง].ID
WHERE (((EMPLO.ACC) Is Null));
```

<a id="q-SLIP4ช่วงที่1"></a>
## SLIP4ช่วงที่1

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง3](<../queries/SLIP.md#q-คำนวณค่าแรง3>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, HOT, SIGN )
SELECT [คำนวณค่าแรง3].ID, EMPLO.NAME, [คำนวณค่าแรง3].SumOfW, [คำนวณค่าแรง3].SumOfL, [คำนวณค่าแรง3].SumOfO1, [คำนวณค่าแรง3].SumOfS, [คำนวณค่าแรง3].SumOfO2, [คำนวณค่าแรง3].SumOfH, [คำนวณค่าแรง3].SumOfO3, [คำนวณค่าแรง3].SumOfHOLI, [คำนวณค่าแรง3].SumOfSUN, [คำนวณค่าแรง3].Expr2, [คำนวณค่าแรง3].Expr3, [คำนวณค่าแรง3].Expr4, [คำนวณค่าแรง3].Expr5, [คำนวณค่าแรง3].Expr6, [คำนวณค่าแรง3].Expr7, [คำนวณค่าแรง3].PAY_D, [คำนวณค่าแรง3].FROM_DA, [คำนวณค่าแรง3].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง3].Expr10, ตาราง3.Sign
FROM ตาราง3, EMPLO INNER JOIN คำนวณค่าแรง3 ON EMPLO.ID = [คำนวณค่าแรง3].ID
WHERE (((EMPLO.ACC) Is Not Null));
```

<a id="q-SLIP41ช่วงที่1"></a>
## SLIP41ช่วงที่1

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง31](<../queries/SLIP.md#q-คำนวณค่าแรง31>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, HOT, SIGN )
SELECT [คำนวณค่าแรง31].ID, EMPLO.NAME, [คำนวณค่าแรง31].SumOfW, [คำนวณค่าแรง31].SumOfL, [คำนวณค่าแรง31].SumOfO1, [คำนวณค่าแรง31].SumOfS, [คำนวณค่าแรง31].SumOfO2, [คำนวณค่าแรง31].SumOfH, [คำนวณค่าแรง31].SumOfO3, [คำนวณค่าแรง31].SumOfHOLI, [คำนวณค่าแรง31].SumOfSUN, [คำนวณค่าแรง31].Expr2, [คำนวณค่าแรง31].Expr3, [คำนวณค่าแรง31].Expr4, [คำนวณค่าแรง31].Expr5, [คำนวณค่าแรง31].Expr6, [คำนวณค่าแรง31].Expr7, [คำนวณค่าแรง31].PAY_D, [คำนวณค่าแรง31].FROM_DA, [คำนวณค่าแรง31].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง31].Expr10, ตาราง3.Sign
FROM ตาราง3, คำนวณค่าแรง31 INNER JOIN EMPLO ON [คำนวณค่าแรง31].ID = EMPLO.ID
WHERE (((EMPLO.ACC) Is Not Null));
```

<a id="q-SLIP6ช่วงที่1"></a>
## SLIP6ช่วงที่1

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง4](<../queries/SLIP.md#q-คำนวณค่าแรง4>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, HOT, SIGN )
SELECT [คำนวณค่าแรง4].ID, EMPLO.NAME, [คำนวณค่าแรง4].SumOfW, [คำนวณค่าแรง4].SumOfL, [คำนวณค่าแรง4].SumOfO1, [คำนวณค่าแรง4].SumOfS, [คำนวณค่าแรง4].SumOfO2, [คำนวณค่าแรง4].SumOfH, [คำนวณค่าแรง4].SumOfO3, [คำนวณค่าแรง4].SumOfHOLI, [คำนวณค่าแรง4].SumOfSUN, [คำนวณค่าแรง4].Expr2, [คำนวณค่าแรง4].Expr3, [คำนวณค่าแรง4].Expr4, [คำนวณค่าแรง4].Expr5, [คำนวณค่าแรง4].Expr6, [คำนวณค่าแรง4].Expr7, [คำนวณค่าแรง4].PAY_D, [คำนวณค่าแรง4].FROM_DA, [คำนวณค่าแรง4].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง4].Expr10, ตาราง3.Sign
FROM ตาราง3, คำนวณค่าแรง4 INNER JOIN EMPLO ON [คำนวณค่าแรง4].ID = EMPLO.ID
WHERE (((EMPLO.ACC) Is Not Null));
```

<a id="q-SLIP61ช่วงที่1"></a>
## SLIP61ช่วงที่1

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง41](<../queries/SLIP.md#q-คำนวณค่าแรง41>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, HOT, SIGN )
SELECT [คำนวณค่าแรง41].ID, EMPLO.NAME, [คำนวณค่าแรง41].SumOfW, [คำนวณค่าแรง41].SumOfL, [คำนวณค่าแรง41].SumOfO1, [คำนวณค่าแรง41].SumOfS, [คำนวณค่าแรง41].SumOfO2, [คำนวณค่าแรง41].SumOfH, [คำนวณค่าแรง41].SumOfO3, [คำนวณค่าแรง41].SumOfHOLI, [คำนวณค่าแรง41].SumOfSUN, [คำนวณค่าแรง41].Expr2, [คำนวณค่าแรง41].Expr3, [คำนวณค่าแรง41].Expr4, [คำนวณค่าแรง41].Expr5, [คำนวณค่าแรง41].Expr6, [คำนวณค่าแรง41].Expr7, [คำนวณค่าแรง41].PAY_D, [คำนวณค่าแรง41].FROM_DA, [คำนวณค่าแรง41].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง41].Expr10, ตาราง3.Sign
FROM ตาราง3, EMPLO INNER JOIN คำนวณค่าแรง41 ON EMPLO.ID = [คำนวณค่าแรง41].ID
WHERE (((EMPLO.ACC) Is Not Null));
```

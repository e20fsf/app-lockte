# คิวรีของ flow: SLIPรวมโบนัส

มาโคร: [SLIPรวมโบนัส](<../macros.md#m-SLIPรวมโบนัส>) — ลำดับคิวรีตามขั้นตอนของมาโคร (รวมคิวรี SELECT ที่ถูกอ้างถึง)

<a id="q-SLIP22"></a>
## SLIP22

- ชนิด: **APPEND** · แก้ล่าสุด 2025-01-13
- อ่านจาก: [EMPLOYEEโบนัส](<../tables.md#t-EMPLOYEEโบนัส>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง2](<../queries/SLIP.md#q-คำนวณค่าแรง2>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIPรวมโบนัส](<../macros.md#m-SLIPรวมโบนัส>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, SIGN, N )
SELECT [คำนวณค่าแรง2].ID, EMPLO.NAME, [คำนวณค่าแรง2].SumOfW, [คำนวณค่าแรง2].SumOfL, [คำนวณค่าแรง2].SumOfO1, [คำนวณค่าแรง2].SumOfS, [คำนวณค่าแรง2].SumOfO2, [คำนวณค่าแรง2].SumOfH, [คำนวณค่าแรง2].SumOfO3, [คำนวณค่าแรง2].SumOfHOLI, [คำนวณค่าแรง2].SumOfSUN, [คำนวณค่าแรง2].Expr2, [คำนวณค่าแรง2].Expr3, [คำนวณค่าแรง2].Expr4, [คำนวณค่าแรง2].Expr5, [คำนวณค่าแรง2].Expr6, [คำนวณค่าแรง2].Expr7, [คำนวณค่าแรง2].PAY_D, [คำนวณค่าแรง2].FROM_DA, [คำนวณค่าแรง2].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7] AS Expr10, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[amount] AS Expr11, EMPLO.SALARY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, ตาราง3.Sign, [EMPLOYEEโบนัส].AMOUNT
FROM ตาราง3, (คำนวณค่าแรง2 INNER JOIN EMPLO ON [คำนวณค่าแรง2].ID = EMPLO.ID) INNER JOIN EMPLOYEEโบนัส ON EMPLO.ID = [EMPLOYEEโบนัส].EMP_ID
WHERE (((EMPLO.ACC) Is Not Null));
```

<a id="q-SLIP33"></a>
## SLIP33

- ชนิด: **APPEND** · แก้ล่าสุด 2025-01-13
- อ่านจาก: [EMPLOYEEโบนัส](<../tables.md#t-EMPLOYEEโบนัส>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง2](<../queries/SLIP.md#q-คำนวณค่าแรง2>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIPรวมโบนัส](<../macros.md#m-SLIPรวมโบนัส>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, ATM, TT, NET, SA_D, TT_D, W_D, SIGN, N )
SELECT [คำนวณค่าแรง2].ID, EMPLO.NAME, [คำนวณค่าแรง2].SumOfW, [คำนวณค่าแรง2].SumOfL, [คำนวณค่าแรง2].SumOfO1, [คำนวณค่าแรง2].SumOfS, [คำนวณค่าแรง2].SumOfO2, [คำนวณค่าแรง2].SumOfH, [คำนวณค่าแรง2].SumOfO3, [คำนวณค่าแรง2].SumOfHOLI, [คำนวณค่าแรง2].SumOfSUN, [คำนวณค่าแรง2].Expr2, [คำนวณค่าแรง2].Expr3, [คำนวณค่าแรง2].Expr4, [คำนวณค่าแรง2].Expr5, [คำนวณค่าแรง2].Expr6, [คำนวณค่าแรง2].Expr7, [คำนวณค่าแรง2].PAY_D, [คำนวณค่าแรง2].FROM_DA, [คำนวณค่าแรง2].TO_DA, 0 AS Expr1, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7] AS Expr10, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[amount] AS Expr11, EMPLO.SALARY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, ตาราง3.Sign, [EMPLOYEEโบนัส].AMOUNT
FROM ตาราง3, (คำนวณค่าแรง2 INNER JOIN EMPLO ON [คำนวณค่าแรง2].ID = EMPLO.ID) INNER JOIN EMPLOYEEโบนัส ON EMPLO.ID = [EMPLOYEEโบนัส].EMP_ID
WHERE (((EMPLO.ACC) Is Null));
```

<a id="q-SLIP44"></a>
## SLIP44

- ชนิด: **APPEND** · แก้ล่าสุด 2025-01-13
- อ่านจาก: [EMPLOYEEโบนัส](<../tables.md#t-EMPLOYEEโบนัส>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง3](<../queries/SLIP.md#q-คำนวณค่าแรง3>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIPรวมโบนัส](<../macros.md#m-SLIPรวมโบนัส>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, HOT, SIGN, N )
SELECT [คำนวณค่าแรง3].ID, EMPLO.NAME, [คำนวณค่าแรง3].SumOfW, [คำนวณค่าแรง3].SumOfL, [คำนวณค่าแรง3].SumOfO1, [คำนวณค่าแรง3].SumOfS, [คำนวณค่าแรง3].SumOfO2, [คำนวณค่าแรง3].SumOfH, [คำนวณค่าแรง3].SumOfO3, [คำนวณค่าแรง3].SumOfHOLI, [คำนวณค่าแรง3].SumOfSUN, [คำนวณค่าแรง3].Expr2, [คำนวณค่าแรง3].Expr3, [คำนวณค่าแรง3].Expr4, [คำนวณค่าแรง3].Expr5, [คำนวณค่าแรง3].Expr6, [คำนวณค่าแรง3].Expr7, [คำนวณค่าแรง3].PAY_D, [คำนวณค่าแรง3].FROM_DA, [คำนวณค่าแรง3].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10]+[amount] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง3].Expr10, ตาราง3.Sign, [EMPLOYEEโบนัส].AMOUNT
FROM ตาราง3, (EMPLO INNER JOIN คำนวณค่าแรง3 ON EMPLO.ID = [คำนวณค่าแรง3].ID) INNER JOIN EMPLOYEEโบนัส ON EMPLO.ID = [EMPLOYEEโบนัส].EMP_ID
WHERE (((EMPLO.ACC) Is Not Null));
```

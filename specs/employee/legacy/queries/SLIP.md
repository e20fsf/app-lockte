# คิวรีของ flow: SLIP

มาโคร: [SLIP](<../macros.md#m-SLIP>) — ลำดับคิวรีตามขั้นตอนของมาโคร (รวมคิวรี SELECT ที่ถูกอ้างถึง)

<a id="q-SLIP1"></a>
## SLIP1

- ชนิด: **DELETE** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [SLIP](<../tables.md#t-SLIP>)
- ใช้โดย: มาโคร [SLIP](<../macros.md#m-SLIP>) (OpenQuery); มาโคร [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) (OpenQuery); มาโคร [SLIPช่วงที่2](<../macros.md#m-SLIPช่วงที่2>) (OpenQuery); มาโคร [SLIPรวมโบนัส](<../macros.md#m-SLIPรวมโบนัส>) (OpenQuery)

```sql
DELETE SLIP.*
FROM SLIP;
```

<a id="q-SLIP2"></a>
## SLIP2

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง2](<../queries/SLIP.md#q-คำนวณค่าแรง2>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIP](<../macros.md#m-SLIP>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, SIGN )
SELECT [คำนวณค่าแรง2].ID, EMPLO.NAME, [คำนวณค่าแรง2].SumOfW, [คำนวณค่าแรง2].SumOfL, [คำนวณค่าแรง2].SumOfO1, [คำนวณค่าแรง2].SumOfS, [คำนวณค่าแรง2].SumOfO2, [คำนวณค่าแรง2].SumOfH, [คำนวณค่าแรง2].SumOfO3, [คำนวณค่าแรง2].SumOfHOLI, [คำนวณค่าแรง2].SumOfSUN, [คำนวณค่าแรง2].Expr2, [คำนวณค่าแรง2].Expr3, [คำนวณค่าแรง2].Expr4, [คำนวณค่าแรง2].Expr5, [คำนวณค่าแรง2].Expr6, [คำนวณค่าแรง2].Expr7, [คำนวณค่าแรง2].PAY_D, [คำนวณค่าแรง2].FROM_DA, [คำนวณค่าแรง2].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7] AS Expr10, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7] AS Expr11, EMPLO.SALARY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, ตาราง3.Sign
FROM ตาราง3, คำนวณค่าแรง2 INNER JOIN EMPLO ON [คำนวณค่าแรง2].ID = EMPLO.ID
WHERE (((EMPLO.ACC) Is Not Null));
```

<a id="q-คำนวณค่าแรง2"></a>
## คำนวณค่าแรง2

- ชนิด: **SELECT** · แก้ล่าสุด 2026-01-27
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [คำนวณค่าแรง1](<../queries/SLIP.md#q-คำนวณค่าแรง1>)
- ใช้โดย: คิวรี [SLIP2](<../queries/SLIP.md#q-SLIP2>) (SQL); คิวรี [SLIP22](<../queries/SLIPรวมโบนัส.md#q-SLIP22>) (SQL); คิวรี [SLIP3](<../queries/SLIP.md#q-SLIP3>) (SQL); คิวรี [SLIP33](<../queries/SLIPรวมโบนัส.md#q-SLIP33>) (SQL); คิวรี [คำนวณค่าแรง5](<../queries/คำนวณค่าแรง.md#q-คำนวณค่าแรง5>) (SQL); มาโคร [~TMPCLPMacro](<../macros.md#m-~TMPCLPMacro>) (OpenQuery); มาโคร [คำนวณค่าแรง](<../macros.md#m-คำนวณค่าแรง>) (OpenQuery); มาโคร [คำนวณเวลาทำงาน1](<../macros.md#m-คำนวณเวลาทำงาน1>) (OpenQuery)

```sql
SELECT [คำนวณค่าแรง1].ID, [คำนวณค่าแรง1].SumOfW, [คำนวณค่าแรง1].SumOfL, [คำนวณค่าแรง1].SumOfO1, [คำนวณค่าแรง1].SumOfS, [คำนวณค่าแรง1].SumOfO2, [คำนวณค่าแรง1].SumOfH, [คำนวณค่าแรง1].SumOfO3, [คำนวณค่าแรง1].FROM_DA, [คำนวณค่าแรง1].TO_DA, [คำนวณค่าแรง1].SumOfHOLI, [คำนวณค่าแรง1].SumOfSUN, Val(Format([SumOfO1]*[sa_day]/8*1.5,"#")) AS Expr3, Val(Format([sumofs]*[sa_day]/8,"#")) AS Expr4, Val(Format([sumofo2]*[sa_day]/8*3,"#")) AS Expr5, Val(Format([sumofh]*[sa_day]/8,"#")) AS Expr6, Val(Format([sumofo3]*[sa_day]/8*3,"#")) AS Expr7, Val(Format([sumofs]+[sumofo2]+[sumofh]+[sumofo3],"#")) AS Expr8, Val(Format([expr3]+[expr4]+[expr5]+[expr6]+[expr7],"#")) AS Expr9, EMPLO.SALARY, EMPLO.SA_DAY, [คำนวณค่าแรง1].PAY_D, [คำนวณค่าแรง1].[SumOfL(NO)], [คำนวณค่าแรง1].DAT, EMPLO.RESIGN, [คำนวณค่าแรง1].SumOfSUN_DAY AS Expr10, [คำนวณค่าแรง1].Day AS Expr11, Val(Format(IIf([point_1] Is Not Null,[point_1],(IIf([resign] Is Null,(IIf([expr1]+[sumofholi]>=[to_da]-[from_da]+1-[sumofsun],[salary]/2,(15-[sumofl(no)])*[sa_day])),(IIf([expr1]=0,0,(IIf([resign]=[to_da]+1,[salary]/2,(IIf([resign]>[dat],([expr1]+[sumofsun_day]+[sumofholi])*[sa_day],([expr1]+[sumofsun_day])*[sa_day]))))))))),"#")) AS Expr2
FROM คำนวณค่าแรง1 INNER JOIN EMPLO ON [คำนวณค่าแรง1].ID = EMPLO.ID
WHERE ((([คำนวณค่าแรง1].CLAS)="1"));
```

<a id="q-คำนวณค่าแรง1"></a>
## คำนวณค่าแรง1

- ชนิด: **SELECT** · แก้ล่าสุด 2026-01-26
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [คำนวณค่าแรง](<../queries/SLIP.md#q-คำนวณค่าแรง>), [คำนวณเวลาทำงาน13](<../queries/SLIP.md#q-คำนวณเวลาทำงาน13>), [ตั้งวันที่ตัดWEEK](<../tables.md#t-ตั้งวันที่ตัดWEEK>)
- ใช้โดย: คิวรี [คำนวณค่าแรง11](<../queries/_ไม่ถูกเรียกใช้.md#q-คำนวณค่าแรง11>) (SQL); คิวรี [คำนวณค่าแรง2](<../queries/SLIP.md#q-คำนวณค่าแรง2>) (SQL); คิวรี [คำนวณค่าแรง21](<../queries/_ไม่ถูกเรียกใช้.md#q-คำนวณค่าแรง21>) (SQL); คิวรี [คำนวณค่าแรง2ช่วง](<../queries/SLIPช่วงที่1.md#q-คำนวณค่าแรง2ช่วง>) (SQL); คิวรี [คำนวณค่าแรง3](<../queries/SLIP.md#q-คำนวณค่าแรง3>) (SQL); คิวรี [คำนวณค่าแรง31](<../queries/SLIP.md#q-คำนวณค่าแรง31>) (SQL); คิวรี [คำนวณค่าแรง4](<../queries/SLIP.md#q-คำนวณค่าแรง4>) (SQL); คิวรี [คำนวณค่าแรง41](<../queries/SLIP.md#q-คำนวณค่าแรง41>) (SQL); มาโคร [~TMPCLPMacro](<../macros.md#m-~TMPCLPMacro>) (OpenQuery); มาโคร [คำนวณค่าแรง](<../macros.md#m-คำนวณค่าแรง>) (OpenQuery); มาโคร [คำนวณค่าแรงช่วงที่1](<../macros.md#m-คำนวณค่าแรงช่วงที่1>) (OpenQuery); มาโคร [คำนวณค่าแรงช่วงที่2](<../macros.md#m-คำนวณค่าแรงช่วงที่2>) (OpenQuery); มาโคร [คำนวณเวลาทำงาน1](<../macros.md#m-คำนวณเวลาทำงาน1>) (OpenQuery)

```sql
SELECT [คำนวณเวลาทำงาน13].ID, [คำนวณเวลาทำงาน13].NAME, [คำนวณเวลาทำงาน13].SumOfW, [คำนวณเวลาทำงาน13].SumOfL, [คำนวณเวลาทำงาน13].SumOfO1, [คำนวณเวลาทำงาน13].SumOfS, [คำนวณเวลาทำงาน13].SumOfO2, [คำนวณเวลาทำงาน13].SumOfH, [คำนวณเวลาทำงาน13].SumOfO3, [คำนวณเวลาทำงาน13].FROM_DA, [คำนวณเวลาทำงาน13].TO_DA, [คำนวณเวลาทำงาน13].SumOfHOLI, [คำนวณเวลาทำงาน13].SumOfSUN, EMPLO.CLAS, [sumofw]+[sumofl] AS Expr1, [sumofw]+[sumofl] AS Expr2, [คำนวณเวลาทำงาน13].PAY_D, [คำนวณเวลาทำงาน13].[SumOfL(NO)], [คำนวณค่าแรง].DAT, [คำนวณเวลาทำงาน13].SumOfSUN_DAY, [ตั้งวันที่ตัดWEEK].DAY
FROM คำนวณค่าแรง, ตั้งวันที่ตัดWEEK, EMPLO INNER JOIN คำนวณเวลาทำงาน13 ON EMPLO.ID = [คำนวณเวลาทำงาน13].ID
WHERE ((([คำนวณเวลาทำงาน13].ID) Not Like "lt*"));
```

<a id="q-คำนวณเวลาทำงาน13"></a>
## คำนวณเวลาทำงาน13

- ชนิด: **SELECT** · แก้ล่าสุด 2026-01-26
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [ตั้งวันที่ตัดWEEK](<../tables.md#t-ตั้งวันที่ตัดWEEK>), [เวลาทำงาน](<../tables.md#t-เวลาทำงาน>)
- ใช้โดย: คิวรี [คำนวณค่าแรง1](<../queries/SLIP.md#q-คำนวณค่าแรง1>) (SQL); คิวรี [คำนวณเวลาทำงาน200](<../queries/_ไม่ถูกเรียกใช้.md#q-คำนวณเวลาทำงาน200>) (SQL); คิวรี [คำนวณเวลาทำงาน400](<../queries/_ไม่ถูกเรียกใช้.md#q-คำนวณเวลาทำงาน400>) (SQL); มาโคร [คำนวณเวลาทำงาน](<../macros.md#m-คำนวณเวลาทำงาน>) (OpenQuery); มาโคร [คำนวณเวลาทำงาน1](<../macros.md#m-คำนวณเวลาทำงาน1>) (OpenQuery); รายงาน [คำนวณเวลาทำงาน13](<../reports/คำนวณเวลาทำงาน13.md>) (RecordSource); รายงาน [คำนวณเวลาทำงาน130](<../reports/คำนวณเวลาทำงาน130.md>) (RecordSource)

```sql
SELECT [เวลาทำงาน].ID, EMPLO.NAME, Sum([เวลาทำงาน].W) AS SumOfW, Sum([เวลาทำงาน].L) AS SumOfL, Sum([เวลาทำงาน].O1) AS SumOfO1, Sum([เวลาทำงาน].S) AS SumOfS, Sum([เวลาทำงาน].O2) AS SumOfO2, Sum([เวลาทำงาน].H) AS SumOfH, Sum([เวลาทำงาน].O3) AS SumOfO3, [ตั้งวันที่ตัดWEEK].FROM_DA, [ตั้งวันที่ตัดWEEK].TO_DA, Sum([เวลาทำงาน].HOLI) AS SumOfHOLI, Sum([เวลาทำงาน].SUN) AS SumOfSUN, [ตั้งวันที่ตัดWEEK].PAY_D, Sum([w]+[l]+[s]+[h]+[holi]+[sun]) AS Expr1, Sum([เวลาทำงาน].[L(NO)]) AS [SumOfL(NO)], Sum([เวลาทำงาน].SUN_DAY) AS SumOfSUN_DAY
FROM ตั้งวันที่ตัดWEEK, เวลาทำงาน INNER JOIN EMPLO ON [เวลาทำงาน].ID = EMPLO.ID
GROUP BY [เวลาทำงาน].ID, EMPLO.NAME, [ตั้งวันที่ตัดWEEK].FROM_DA, [ตั้งวันที่ตัดWEEK].TO_DA, [ตั้งวันที่ตัดWEEK].PAY_D
HAVING (((Sum([w]+[l]+[s]+[h]+[holi]+[sun]))>0));
```

<a id="q-คำนวณค่าแรง"></a>
## คำนวณค่าแรง

- ชนิด: **SELECT** · แก้ล่าสุด 2026-01-26
- อ่านจาก: [คำนวณเวลาทำงาน14](<../queries/SLIP.md#q-คำนวณเวลาทำงาน14>)
- ใช้โดย: คิวรี [คำนวณค่าแรง1](<../queries/SLIP.md#q-คำนวณค่าแรง1>) (SQL); มาโคร [~TMPCLPMacro](<../macros.md#m-~TMPCLPMacro>) (OpenQuery); มาโคร [คำนวณค่าแรง](<../macros.md#m-คำนวณค่าแรง>) (OpenQuery)

```sql
SELECT Min([date]) AS DAT
FROM คำนวณเวลาทำงาน14;
```

<a id="q-คำนวณเวลาทำงาน14"></a>
## คำนวณเวลาทำงาน14

- ชนิด: **SELECT** · แก้ล่าสุด 2026-01-26
- อ่านจาก: [HOLIDAY](<../tables.md#t-HOLIDAY>), [ตั้งวันที่ตัดWEEK](<../tables.md#t-ตั้งวันที่ตัดWEEK>)
- ใช้โดย: คิวรี [คำนวณค่าแรง](<../queries/SLIP.md#q-คำนวณค่าแรง>) (SQL); คิวรี [คำนวณเวลาทำงาน15](<../queries/คำนวณเวลาทำงาน.md#q-คำนวณเวลาทำงาน15>) (SQL); คิวรี [คำนวณเวลาทำงาน151](<../queries/คำนวณเวลาทำงาน.md#q-คำนวณเวลาทำงาน151>) (SQL); คิวรี [คำนวณเวลาทำงาน408](<../queries/_ไม่ถูกเรียกใช้.md#q-คำนวณเวลาทำงาน408>) (SQL); มาโคร [คำนวณเวลาทำงาน](<../macros.md#m-คำนวณเวลาทำงาน>) (OpenQuery); มาโคร [คำนวณเวลาทำงาน1](<../macros.md#m-คำนวณเวลาทำงาน1>) (OpenQuery)

```sql
SELECT HOLIDAY.Date, HOLIDAY.ITEM, [ตั้งวันที่ตัดWEEK].FROM_DA, [ตั้งวันที่ตัดWEEK].TO_DA, 1 AS QTY
FROM HOLIDAY, ตั้งวันที่ตัดWEEK
GROUP BY HOLIDAY.Date, HOLIDAY.ITEM, [ตั้งวันที่ตัดWEEK].FROM_DA, [ตั้งวันที่ตัดWEEK].TO_DA, 1
HAVING (((HOLIDAY.Date)>=[from_da] And (HOLIDAY.Date)<=[to_da]));
```

<a id="q-SLIP3"></a>
## SLIP3

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง2](<../queries/SLIP.md#q-คำนวณค่าแรง2>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIP](<../macros.md#m-SLIP>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, SIGN )
SELECT [คำนวณค่าแรง2].ID, EMPLO.NAME, [คำนวณค่าแรง2].SumOfW, [คำนวณค่าแรง2].SumOfL, [คำนวณค่าแรง2].SumOfO1, [คำนวณค่าแรง2].SumOfS, [คำนวณค่าแรง2].SumOfO2, [คำนวณค่าแรง2].SumOfH, [คำนวณค่าแรง2].SumOfO3, [คำนวณค่าแรง2].SumOfHOLI, [คำนวณค่าแรง2].SumOfSUN, [คำนวณค่าแรง2].Expr2, [คำนวณค่าแรง2].Expr3, [คำนวณค่าแรง2].Expr4, [คำนวณค่าแรง2].Expr5, [คำนวณค่าแรง2].Expr6, [คำนวณค่าแรง2].Expr7, [คำนวณค่าแรง2].PAY_D, [คำนวณค่าแรง2].FROM_DA, [คำนวณค่าแรง2].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7] AS Expr10, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7] AS Expr11, EMPLO.SALARY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, ตาราง3.Sign
FROM ตาราง3, คำนวณค่าแรง2 INNER JOIN EMPLO ON [คำนวณค่าแรง2].ID = EMPLO.ID
WHERE (((EMPLO.ACC) Is Null));
```

<a id="q-SLIP4"></a>
## SLIP4

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง3](<../queries/SLIP.md#q-คำนวณค่าแรง3>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIP](<../macros.md#m-SLIP>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, HOT, SIGN )
SELECT [คำนวณค่าแรง3].ID, EMPLO.NAME, [คำนวณค่าแรง3].SumOfW, [คำนวณค่าแรง3].SumOfL, [คำนวณค่าแรง3].SumOfO1, [คำนวณค่าแรง3].SumOfS, [คำนวณค่าแรง3].SumOfO2, [คำนวณค่าแรง3].SumOfH, [คำนวณค่าแรง3].SumOfO3, [คำนวณค่าแรง3].SumOfHOLI, [คำนวณค่าแรง3].SumOfSUN, [คำนวณค่าแรง3].Expr2, [คำนวณค่าแรง3].Expr3, [คำนวณค่าแรง3].Expr4, [คำนวณค่าแรง3].Expr5, [คำนวณค่าแรง3].Expr6, [คำนวณค่าแรง3].Expr7, [คำนวณค่าแรง3].PAY_D, [คำนวณค่าแรง3].FROM_DA, [คำนวณค่าแรง3].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง3].Expr10, ตาราง3.Sign
FROM ตาราง3, EMPLO INNER JOIN คำนวณค่าแรง3 ON EMPLO.ID = [คำนวณค่าแรง3].ID
WHERE (((EMPLO.ACC) Is Not Null));
```

<a id="q-คำนวณค่าแรง3"></a>
## คำนวณค่าแรง3

- ชนิด: **SELECT** · แก้ล่าสุด 2026-01-26
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [คำนวณค่าแรง1](<../queries/SLIP.md#q-คำนวณค่าแรง1>)
- ใช้โดย: คิวรี [SLIP4](<../queries/SLIP.md#q-SLIP4>) (SQL); คิวรี [SLIP44](<../queries/SLIPรวมโบนัส.md#q-SLIP44>) (SQL); คิวรี [SLIP4ช่วงที่1](<../queries/SLIPช่วงที่1.md#q-SLIP4ช่วงที่1>) (SQL); คิวรี [SLIP4ช่วงที่2](<../queries/SLIPช่วงที่2.md#q-SLIP4ช่วงที่2>) (SQL); คิวรี [SLIP5](<../queries/SLIP.md#q-SLIP5>) (SQL); คิวรี [คำนวณค่าแรง6](<../queries/คำนวณค่าแรง.md#q-คำนวณค่าแรง6>) (SQL); คิวรี [คำนวณค่าแรง6ช่วงที่1](<../queries/คำนวณค่าแรงช่วงที่1.md#q-คำนวณค่าแรง6ช่วงที่1>) (SQL); คิวรี [คำนวณค่าแรง6ช่วงที่2](<../queries/คำนวณค่าแรงช่วงที่2.md#q-คำนวณค่าแรง6ช่วงที่2>) (SQL); มาโคร [~TMPCLPMacro](<../macros.md#m-~TMPCLPMacro>) (OpenQuery); มาโคร [คำนวณค่าแรง](<../macros.md#m-คำนวณค่าแรง>) (OpenQuery); มาโคร [คำนวณค่าแรงช่วงที่1](<../macros.md#m-คำนวณค่าแรงช่วงที่1>) (OpenQuery); มาโคร [คำนวณค่าแรงช่วงที่2](<../macros.md#m-คำนวณค่าแรงช่วงที่2>) (OpenQuery); มาโคร [คำนวณเวลาทำงาน1](<../macros.md#m-คำนวณเวลาทำงาน1>) (OpenQuery)

```sql
SELECT [คำนวณค่าแรง1].ID, [คำนวณค่าแรง1].SumOfW, [คำนวณค่าแรง1].SumOfL, [คำนวณค่าแรง1].Expr1, [คำนวณค่าแรง1].CLAS, [คำนวณค่าแรง1].SumOfO1, [คำนวณค่าแรง1].SumOfS, [คำนวณค่าแรง1].SumOfO2, [คำนวณค่าแรง1].SumOfH, [คำนวณค่าแรง1].SumOfO3, [คำนวณค่าแรง1].FROM_DA, [คำนวณค่าแรง1].TO_DA, EMPLO.SALARY, [คำนวณค่าแรง1].SumOfHOLI, [คำนวณค่าแรง1].SumOfSUN, EMPLO.SA_DAY, Val(Format(IIf([point_1] Is Not Null,[point_1],(IIf([resign] Is Null,(IIf([expr1]+[sumofholi]>=[to_da]-[from_da]+1-[sumofsun],[sa_day]*([to_da]-[from_da]+1-[sumofsun]),([expr1]+[sumofholi])*[sa_day])),(IIf([resign]>[dat],(IIf([expr1]+[sumofholi]>=[to_da]-[from_da]+1-[sumofsun],[sa_day]*([to_da]-[from_da]+1-[sumofsun]),([expr1]+[sumofholi])*[sa_day])),[expr1]*[sa_day]))))),"#")) AS Expr2, Val(Format([sumofO1]*[sa_day]/8*1.5,"#")) AS Expr3, Val(Format([sumofs]*[sa_day]/8*2,"#")) AS Expr4, Val(Format([sumofO2]*[sa_day]/8*3,"#")) AS Expr5, Val(Format([sumofh]*[sa_day]/8,"#")) AS Expr6, Val(Format([sumofo3]*[sa_day]/8*3,"#")) AS Expr7, Val(Format([sumofs]+[sumofo2]+[sumofh]+[sumofo3],"#")) AS Expr8, Val(Format([expr3]+[expr4]+[expr5]+[expr6]+[expr7],"#")) AS Expr9, [คำนวณค่าแรง1].PAY_D, 0 AS Expr10, EMPLO.HOT1, EMPLO.POINT_1, [คำนวณค่าแรง1].DAT, EMPLO.RESIGN
FROM คำนวณค่าแรง1 INNER JOIN EMPLO ON [คำนวณค่าแรง1].ID = EMPLO.ID
WHERE ((([คำนวณค่าแรง1].CLAS)="2") AND ((EMPLO.HOT1)="N"));
```

<a id="q-SLIP41"></a>
## SLIP41

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง31](<../queries/SLIP.md#q-คำนวณค่าแรง31>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIP](<../macros.md#m-SLIP>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, HOT, SIGN )
SELECT [คำนวณค่าแรง31].ID, EMPLO.NAME, [คำนวณค่าแรง31].SumOfW, [คำนวณค่าแรง31].SumOfL, [คำนวณค่าแรง31].SumOfO1, [คำนวณค่าแรง31].SumOfS, [คำนวณค่าแรง31].SumOfO2, [คำนวณค่าแรง31].SumOfH, [คำนวณค่าแรง31].SumOfO3, [คำนวณค่าแรง31].SumOfHOLI, [คำนวณค่าแรง31].SumOfSUN, [คำนวณค่าแรง31].Expr2, [คำนวณค่าแรง31].Expr3, [คำนวณค่าแรง31].Expr4, [คำนวณค่าแรง31].Expr5, [คำนวณค่าแรง31].Expr6, [คำนวณค่าแรง31].Expr7, [คำนวณค่าแรง31].PAY_D, [คำนวณค่าแรง31].FROM_DA, [คำนวณค่าแรง31].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง31].Expr10, ตาราง3.Sign
FROM ตาราง3, คำนวณค่าแรง31 INNER JOIN EMPLO ON [คำนวณค่าแรง31].ID = EMPLO.ID
WHERE (((EMPLO.ACC) Is Not Null));
```

<a id="q-คำนวณค่าแรง31"></a>
## คำนวณค่าแรง31

- ชนิด: **SELECT** · แก้ล่าสุด 2026-01-26
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [คำนวณค่าแรง1](<../queries/SLIP.md#q-คำนวณค่าแรง1>)
- ใช้โดย: คิวรี [SLIP41](<../queries/SLIP.md#q-SLIP41>) (SQL); คิวรี [SLIP41ช่วงที่1](<../queries/SLIPช่วงที่1.md#q-SLIP41ช่วงที่1>) (SQL); คิวรี [SLIP41ช่วงที่2](<../queries/SLIPช่วงที่2.md#q-SLIP41ช่วงที่2>) (SQL); คิวรี [SLIP51](<../queries/SLIP.md#q-SLIP51>) (SQL); คิวรี [คำนวณค่าแรง61](<../queries/คำนวณค่าแรง.md#q-คำนวณค่าแรง61>) (SQL); คิวรี [คำนวณค่าแรง61ช่วงที่1](<../queries/คำนวณค่าแรงช่วงที่1.md#q-คำนวณค่าแรง61ช่วงที่1>) (SQL); คิวรี [คำนวณค่าแรง61ช่วงที่2](<../queries/คำนวณค่าแรงช่วงที่2.md#q-คำนวณค่าแรง61ช่วงที่2>) (SQL); มาโคร [~TMPCLPMacro](<../macros.md#m-~TMPCLPMacro>) (OpenQuery); มาโคร [คำนวณค่าแรง](<../macros.md#m-คำนวณค่าแรง>) (OpenQuery); มาโคร [คำนวณค่าแรงช่วงที่1](<../macros.md#m-คำนวณค่าแรงช่วงที่1>) (OpenQuery); มาโคร [คำนวณค่าแรงช่วงที่2](<../macros.md#m-คำนวณค่าแรงช่วงที่2>) (OpenQuery); มาโคร [คำนวณเวลาทำงาน1](<../macros.md#m-คำนวณเวลาทำงาน1>) (OpenQuery)

```sql
SELECT [คำนวณค่าแรง1].ID, [คำนวณค่าแรง1].SumOfW, [คำนวณค่าแรง1].SumOfL, [คำนวณค่าแรง1].Expr1, [คำนวณค่าแรง1].CLAS, [คำนวณค่าแรง1].SumOfO1, [คำนวณค่าแรง1].SumOfS, [คำนวณค่าแรง1].SumOfO2, [คำนวณค่าแรง1].SumOfH, [คำนวณค่าแรง1].SumOfO3, [คำนวณค่าแรง1].FROM_DA, [คำนวณค่าแรง1].TO_DA, EMPLO.SALARY, [คำนวณค่าแรง1].SumOfHOLI, [คำนวณค่าแรง1].SumOfSUN, EMPLO.SA_DAY, Val(Format(IIf([expr1]+[sumofholi]>=[to_da]-[from_da]+1-[sumofsun],[sa_day]*([to_da]-[from_da]+1-[sumofsun]),([expr1]+[sumofholi])*[sa_day]),"#")) AS Expr2, Val(Format([sumofO1]*[sa_day]/8*1.5,"#")) AS Expr3, Val(Format([sumofs]*[sa_day]/8*2,"#")) AS Expr4, Val(Format([sumofO2]*[sa_day]/8*3,"#")) AS Expr5, Val(Format([sumofh]*[sa_day]/8,"#")) AS Expr6, Val(Format([sumofo3]*[sa_day]/8*3,"#")) AS Expr7, Val(Format([sumofs]+[sumofo2]+[sumofh]+[sumofo3],"#")) AS Expr8, Val(Format([expr3]+[expr4]+[expr5]+[expr6]+[expr7],"#")) AS Expr9, [คำนวณค่าแรง1].PAY_D, Round([sumofw]*10,2) AS Expr10, EMPLO.HOT1
FROM คำนวณค่าแรง1 INNER JOIN EMPLO ON [คำนวณค่าแรง1].ID = EMPLO.ID
WHERE ((([คำนวณค่าแรง1].CLAS)="2") AND ((EMPLO.HOT1)="Y"));
```

<a id="q-SLIP5"></a>
## SLIP5

- ชนิด: **APPEND** · แก้ล่าสุด 2025-01-13
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง3](<../queries/SLIP.md#q-คำนวณค่าแรง3>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIP](<../macros.md#m-SLIP>) (OpenQuery); มาโคร [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) (OpenQuery); มาโคร [SLIPช่วงที่2](<../macros.md#m-SLIPช่วงที่2>) (OpenQuery); มาโคร [SLIPรวมโบนัส](<../macros.md#m-SLIPรวมโบนัส>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, ATM, TT, NET, SA_D, TT_D, W_D, HOT, SIGN )
SELECT [คำนวณค่าแรง3].ID, EMPLO.NAME, [คำนวณค่าแรง3].SumOfW, [คำนวณค่าแรง3].SumOfL, [คำนวณค่าแรง3].SumOfO1, [คำนวณค่าแรง3].SumOfS, [คำนวณค่าแรง3].SumOfO2, [คำนวณค่าแรง3].SumOfH, [คำนวณค่าแรง3].SumOfO3, [คำนวณค่าแรง3].SumOfHOLI, [คำนวณค่าแรง3].SumOfSUN, [คำนวณค่าแรง3].Expr2, [คำนวณค่าแรง3].Expr3, [คำนวณค่าแรง3].Expr4, [คำนวณค่าแรง3].Expr5, [คำนวณค่าแรง3].Expr6, [คำนวณค่าแรง3].Expr7, [คำนวณค่าแรง3].PAY_D, [คำนวณค่าแรง3].FROM_DA, [คำนวณค่าแรง3].TO_DA, 0 AS Expr1, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง3].Expr10, ตาราง3.Sign
FROM ตาราง3, EMPLO INNER JOIN คำนวณค่าแรง3 ON EMPLO.ID = [คำนวณค่าแรง3].ID
WHERE (((EMPLO.ACC) Is Null));
```

<a id="q-SLIP51"></a>
## SLIP51

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง31](<../queries/SLIP.md#q-คำนวณค่าแรง31>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIP](<../macros.md#m-SLIP>) (OpenQuery); มาโคร [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) (OpenQuery); มาโคร [SLIPช่วงที่2](<../macros.md#m-SLIPช่วงที่2>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, ATM, TT, NET, SA_D, TT_D, W_D, HOT, SIGN )
SELECT [คำนวณค่าแรง31].ID, EMPLO.NAME, [คำนวณค่าแรง31].SumOfW, [คำนวณค่าแรง31].SumOfL, [คำนวณค่าแรง31].SumOfO1, [คำนวณค่าแรง31].SumOfS, [คำนวณค่าแรง31].SumOfO2, [คำนวณค่าแรง31].SumOfH, [คำนวณค่าแรง31].SumOfO3, [คำนวณค่าแรง31].SumOfHOLI, [คำนวณค่าแรง31].SumOfSUN, [คำนวณค่าแรง31].Expr2, [คำนวณค่าแรง31].Expr3, [คำนวณค่าแรง31].Expr4, [คำนวณค่าแรง31].Expr5, [คำนวณค่าแรง31].Expr6, [คำนวณค่าแรง31].Expr7, [คำนวณค่าแรง31].PAY_D, [คำนวณค่าแรง31].FROM_DA, [คำนวณค่าแรง31].TO_DA, 0 AS Expr1, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง31].Expr10, ตาราง3.Sign
FROM ตาราง3, คำนวณค่าแรง31 INNER JOIN EMPLO ON [คำนวณค่าแรง31].ID = EMPLO.ID
WHERE (((EMPLO.ACC) Is Null));
```

<a id="q-SLIP6"></a>
## SLIP6

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง4](<../queries/SLIP.md#q-คำนวณค่าแรง4>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIP](<../macros.md#m-SLIP>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, HOT, SIGN )
SELECT [คำนวณค่าแรง4].ID, EMPLO.NAME, [คำนวณค่าแรง4].SumOfW, [คำนวณค่าแรง4].SumOfL, [คำนวณค่าแรง4].SumOfO1, [คำนวณค่าแรง4].SumOfS, [คำนวณค่าแรง4].SumOfO2, [คำนวณค่าแรง4].SumOfH, [คำนวณค่าแรง4].SumOfO3, [คำนวณค่าแรง4].SumOfHOLI, [คำนวณค่าแรง4].SumOfSUN, [คำนวณค่าแรง4].Expr2, [คำนวณค่าแรง4].Expr3, [คำนวณค่าแรง4].Expr4, [คำนวณค่าแรง4].Expr5, [คำนวณค่าแรง4].Expr6, [คำนวณค่าแรง4].Expr7, [คำนวณค่าแรง4].PAY_D, [คำนวณค่าแรง4].FROM_DA, [คำนวณค่าแรง4].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง4].Expr10, ตาราง3.Sign
FROM ตาราง3, คำนวณค่าแรง4 INNER JOIN EMPLO ON [คำนวณค่าแรง4].ID = EMPLO.ID
WHERE (((EMPLO.ACC) Is Not Null));
```

<a id="q-คำนวณค่าแรง4"></a>
## คำนวณค่าแรง4

- ชนิด: **SELECT** · แก้ล่าสุด 2026-01-26
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [คำนวณค่าแรง1](<../queries/SLIP.md#q-คำนวณค่าแรง1>)
- ใช้โดย: คิวรี [SLIP6](<../queries/SLIP.md#q-SLIP6>) (SQL); คิวรี [SLIP6ช่วงที่1](<../queries/SLIPช่วงที่1.md#q-SLIP6ช่วงที่1>) (SQL); คิวรี [SLIP6ช่วงที่2](<../queries/SLIPช่วงที่2.md#q-SLIP6ช่วงที่2>) (SQL); คิวรี [SLIP7](<../queries/SLIP.md#q-SLIP7>) (SQL); คิวรี [คำนวณค่าแรง7](<../queries/คำนวณค่าแรง.md#q-คำนวณค่าแรง7>) (SQL); คิวรี [คำนวณค่าแรง7ช่วงที่1](<../queries/คำนวณค่าแรงช่วงที่1.md#q-คำนวณค่าแรง7ช่วงที่1>) (SQL); คิวรี [คำนวณค่าแรง7ช่วงที่2](<../queries/คำนวณค่าแรงช่วงที่2.md#q-คำนวณค่าแรง7ช่วงที่2>) (SQL); มาโคร [~TMPCLPMacro](<../macros.md#m-~TMPCLPMacro>) (OpenQuery); มาโคร [คำนวณค่าแรง](<../macros.md#m-คำนวณค่าแรง>) (OpenQuery); มาโคร [คำนวณค่าแรงช่วงที่1](<../macros.md#m-คำนวณค่าแรงช่วงที่1>) (OpenQuery); มาโคร [คำนวณค่าแรงช่วงที่2](<../macros.md#m-คำนวณค่าแรงช่วงที่2>) (OpenQuery); มาโคร [คำนวณเวลาทำงาน1](<../macros.md#m-คำนวณเวลาทำงาน1>) (OpenQuery)

```sql
SELECT [คำนวณค่าแรง1].ID, [คำนวณค่าแรง1].SumOfW, [คำนวณค่าแรง1].SumOfL, [คำนวณค่าแรง1].Expr1, [คำนวณค่าแรง1].CLAS, [คำนวณค่าแรง1].SumOfO1, [คำนวณค่าแรง1].SumOfS, [คำนวณค่าแรง1].SumOfO2, [คำนวณค่าแรง1].SumOfH, [คำนวณค่าแรง1].SumOfO3, [คำนวณค่าแรง1].FROM_DA, [คำนวณค่าแรง1].TO_DA, EMPLO.SALARY, [คำนวณค่าแรง1].SumOfHOLI, [คำนวณค่าแรง1].SumOfSUN, EMPLO.SA_DAY, Val(Format(IIf([expr1]>=[to_da]-[from_da]+1-[sumofsun]-[sumofholi],[sa_day]*([to_da]-[from_da]+1-[sumofsun]-[sumofholi]),[expr1]*[sa_day]),"#")) AS Expr2, Val(Format([sumofO1]*[sa_day]/8*1.5,"#")) AS Expr3, Val(Format([sumofs]*[sa_day]/8*2,"#")) AS Expr4, Val(Format([sumofO2]*[sa_day]/8*3,"#")) AS Expr5, Val(Format([sumofh]*[sa_day]/8*2,"#")) AS Expr6, Val(Format([sumofo3]*[sa_day]/8*3,"#")) AS Expr7, Val(Format([sumofs]+[sumofo2]+[sumofh]+[sumofo3],"#")) AS Expr8, Val(Format([expr3]+[expr4]+[expr5]+[expr6]+[expr7],"#")) AS Expr9, [คำนวณค่าแรง1].PAY_D, 0 AS Expr10, EMPLO.HOT1
FROM คำนวณค่าแรง1 INNER JOIN EMPLO ON [คำนวณค่าแรง1].ID = EMPLO.ID
WHERE ((([คำนวณค่าแรง1].CLAS)="3") AND ((EMPLO.HOT1)="N"));
```

<a id="q-SLIP61"></a>
## SLIP61

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง41](<../queries/SLIP.md#q-คำนวณค่าแรง41>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIP](<../macros.md#m-SLIP>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, TT, NET, SA_D, TT_D, W_D, HOT, SIGN )
SELECT [คำนวณค่าแรง41].ID, EMPLO.NAME, [คำนวณค่าแรง41].SumOfW, [คำนวณค่าแรง41].SumOfL, [คำนวณค่าแรง41].SumOfO1, [คำนวณค่าแรง41].SumOfS, [คำนวณค่าแรง41].SumOfO2, [คำนวณค่าแรง41].SumOfH, [คำนวณค่าแรง41].SumOfO3, [คำนวณค่าแรง41].SumOfHOLI, [คำนวณค่าแรง41].SumOfSUN, [คำนวณค่าแรง41].Expr2, [คำนวณค่าแรง41].Expr3, [คำนวณค่าแรง41].Expr4, [คำนวณค่าแรง41].Expr5, [คำนวณค่าแรง41].Expr6, [คำนวณค่าแรง41].Expr7, [คำนวณค่าแรง41].PAY_D, [คำนวณค่าแรง41].FROM_DA, [คำนวณค่าแรง41].TO_DA, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง41].Expr10, ตาราง3.Sign
FROM ตาราง3, EMPLO INNER JOIN คำนวณค่าแรง41 ON EMPLO.ID = [คำนวณค่าแรง41].ID
WHERE (((EMPLO.ACC) Is Not Null));
```

<a id="q-คำนวณค่าแรง41"></a>
## คำนวณค่าแรง41

- ชนิด: **SELECT** · แก้ล่าสุด 2026-01-26
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [คำนวณค่าแรง1](<../queries/SLIP.md#q-คำนวณค่าแรง1>)
- ใช้โดย: คิวรี [SLIP61](<../queries/SLIP.md#q-SLIP61>) (SQL); คิวรี [SLIP61ช่วงที่1](<../queries/SLIPช่วงที่1.md#q-SLIP61ช่วงที่1>) (SQL); คิวรี [SLIP61ช่วงที่2](<../queries/SLIPช่วงที่2.md#q-SLIP61ช่วงที่2>) (SQL); คิวรี [SLIP71](<../queries/SLIP.md#q-SLIP71>) (SQL); คิวรี [คำนวณค่าแรง71](<../queries/คำนวณค่าแรง.md#q-คำนวณค่าแรง71>) (SQL); คิวรี [คำนวณค่าแรง71ช่วงที่1](<../queries/คำนวณค่าแรงช่วงที่1.md#q-คำนวณค่าแรง71ช่วงที่1>) (SQL); คิวรี [คำนวณค่าแรง71ช่วงที่2](<../queries/คำนวณค่าแรงช่วงที่2.md#q-คำนวณค่าแรง71ช่วงที่2>) (SQL); มาโคร [~TMPCLPMacro](<../macros.md#m-~TMPCLPMacro>) (OpenQuery); มาโคร [คำนวณค่าแรง](<../macros.md#m-คำนวณค่าแรง>) (OpenQuery); มาโคร [คำนวณค่าแรงช่วงที่1](<../macros.md#m-คำนวณค่าแรงช่วงที่1>) (OpenQuery); มาโคร [คำนวณค่าแรงช่วงที่2](<../macros.md#m-คำนวณค่าแรงช่วงที่2>) (OpenQuery); มาโคร [คำนวณเวลาทำงาน1](<../macros.md#m-คำนวณเวลาทำงาน1>) (OpenQuery)

```sql
SELECT [คำนวณค่าแรง1].ID, [คำนวณค่าแรง1].SumOfW, [คำนวณค่าแรง1].SumOfL, [คำนวณค่าแรง1].Expr1, [คำนวณค่าแรง1].CLAS, [คำนวณค่าแรง1].SumOfO1, [คำนวณค่าแรง1].SumOfS, [คำนวณค่าแรง1].SumOfO2, [คำนวณค่าแรง1].SumOfH, [คำนวณค่าแรง1].SumOfO3, [คำนวณค่าแรง1].FROM_DA, [คำนวณค่าแรง1].TO_DA, EMPLO.SALARY, [คำนวณค่าแรง1].SumOfHOLI, [คำนวณค่าแรง1].SumOfSUN, EMPLO.SA_DAY, Val(Format(IIf([expr1]>=[to_da]-[from_da]+1-[sumofsun]-[sumofholi],[sa_day]*([to_da]-[from_da]+1-[sumofsun]-[sumofholi]),[expr1]*[sa_day]),"#")) AS Expr2, Val(Format([sumofO1]*[sa_day]/8*1.5,"#")) AS Expr3, Val(Format([sumofs]*[sa_day]/8*2,"#")) AS Expr4, Val(Format([sumofO2]*[sa_day]/8*3,"#")) AS Expr5, Val(Format([sumofh]*[sa_day]/8*2,"#")) AS Expr6, Val(Format([sumofo3]*[sa_day]/8*3,"#")) AS Expr7, Val(Format([sumofs]+[sumofo2]+[sumofh]+[sumofo3],"#")) AS Expr8, Val(Format([expr3]+[expr4]+[expr5]+[expr6]+[expr7],"#")) AS Expr9, [คำนวณค่าแรง1].PAY_D, Round([sumofw]*10,2) AS Expr10, EMPLO.HOT1
FROM คำนวณค่าแรง1 INNER JOIN EMPLO ON [คำนวณค่าแรง1].ID = EMPLO.ID
WHERE ((([คำนวณค่าแรง1].CLAS)="3") AND ((EMPLO.HOT1)="Y"));
```

<a id="q-SLIP7"></a>
## SLIP7

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง4](<../queries/SLIP.md#q-คำนวณค่าแรง4>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIP](<../macros.md#m-SLIP>) (OpenQuery); มาโคร [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) (OpenQuery); มาโคร [SLIPช่วงที่2](<../macros.md#m-SLIPช่วงที่2>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, ATM, TT, NET, SA_D, TT_D, W_D, HOT, SIGN )
SELECT [คำนวณค่าแรง4].ID, EMPLO.NAME, [คำนวณค่าแรง4].SumOfW, [คำนวณค่าแรง4].SumOfL, [คำนวณค่าแรง4].SumOfO1, [คำนวณค่าแรง4].SumOfS, [คำนวณค่าแรง4].SumOfO2, [คำนวณค่าแรง4].SumOfH, [คำนวณค่าแรง4].SumOfO3, [คำนวณค่าแรง4].SumOfHOLI, [คำนวณค่าแรง4].SumOfSUN, [คำนวณค่าแรง4].Expr2, [คำนวณค่าแรง4].Expr3, [คำนวณค่าแรง4].Expr4, [คำนวณค่าแรง4].Expr5, [คำนวณค่าแรง4].Expr6, [คำนวณค่าแรง4].Expr7, [คำนวณค่าแรง4].PAY_D, [คำนวณค่าแรง4].FROM_DA, [คำนวณค่าแรง4].TO_DA, 0 AS Expr1, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง4].Expr10, ตาราง3.Sign
FROM ตาราง3, คำนวณค่าแรง4 INNER JOIN EMPLO ON [คำนวณค่าแรง4].ID = EMPLO.ID
WHERE (((EMPLO.ACC) Is Null));
```

<a id="q-SLIP71"></a>
## SLIP71

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [EMPLO](<../tables.md#t-EMPLO>), [OT](<../tables.md#t-OT>), [SLIP](<../tables.md#t-SLIP>), [คำนวณค่าแรง41](<../queries/SLIP.md#q-คำนวณค่าแรง41>), [ตาราง3](<../tables.md#t-ตาราง3>)
- ใช้โดย: มาโคร [SLIP](<../macros.md#m-SLIP>) (OpenQuery); มาโคร [SLIPช่วงที่1](<../macros.md#m-SLIPช่วงที่1>) (OpenQuery); มาโคร [SLIPช่วงที่2](<../macros.md#m-SLIPช่วงที่2>) (OpenQuery)

```sql
INSERT INTO SLIP ( ID, NAME, W, L, O1, S, O2, H, O3, H_D, SUN_D, SALA, OT, S_W, OT2, H_W, OT3, [DATE], FROM_D, TO_D, ATM, TT, NET, SA_D, TT_D, W_D, HOT, SIGN )
SELECT [คำนวณค่าแรง41].ID, EMPLO.NAME, [คำนวณค่าแรง41].SumOfW, [คำนวณค่าแรง41].SumOfL, [คำนวณค่าแรง41].SumOfO1, [คำนวณค่าแรง41].SumOfS, [คำนวณค่าแรง41].SumOfO2, [คำนวณค่าแรง41].SumOfH, [คำนวณค่าแรง41].SumOfO3, [คำนวณค่าแรง41].SumOfHOLI, [คำนวณค่าแรง41].SumOfSUN, [คำนวณค่าแรง41].Expr2, [คำนวณค่าแรง41].Expr3, [คำนวณค่าแรง41].Expr4, [คำนวณค่าแรง41].Expr5, [คำนวณค่าแรง41].Expr6, [คำนวณค่าแรง41].Expr7, [คำนวณค่าแรง41].PAY_D, [คำนวณค่าแรง41].FROM_DA, [คำนวณค่าแรง41].TO_DA, 0 AS Expr1, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr19, [expr2]+[expr3]+[expr4]+[expr5]+[expr6]+[expr7]+[expr10] AS Expr11, EMPLO.SA_DAY, [to_da]-[from_da]+1 AS Expr12, [to_da]-[from_da]+1-[sumofholi]-[sumofsun] AS Expr13, [คำนวณค่าแรง41].Expr10, ตาราง3.Sign
FROM ตาราง3, EMPLO INNER JOIN คำนวณค่าแรง41 ON EMPLO.ID = [คำนวณค่าแรง41].ID
WHERE (((EMPLO.ACC) Is Null));
```

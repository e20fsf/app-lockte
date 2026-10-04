# คิวรีของ flow: SUNDAY

มาโคร: [SUNDAY](<../macros.md#m-SUNDAY>) — ลำดับคิวรีตามขั้นตอนของมาโคร (รวมคิวรี SELECT ที่ถูกอ้างถึง)

<a id="q-sunday11"></a>
## sunday11

- ชนิด: **SELECT** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [SUNDAY](<../tables.md#t-SUNDAY>)
- ใช้โดย: คิวรี [sunday12](<../queries/SUNDAY.md#q-sunday12>) (SQL); มาโคร [SUNDAY](<../macros.md#m-SUNDAY>) (OpenQuery)

```sql
SELECT Max(SUNDAY.Date) AS MaxOfDATE, SUNDAY.ITEM, Max([date]+7) AS Expr1
FROM SUNDAY
GROUP BY SUNDAY.ITEM;
```

<a id="q-sunday12"></a>
## sunday12

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [SUNDAY](<../tables.md#t-SUNDAY>), [sunday11](<../queries/SUNDAY.md#q-sunday11>)
- ใช้โดย: มาโคร [SUNDAY](<../macros.md#m-SUNDAY>) (OpenQuery)

```sql
INSERT INTO SUNDAY ( [DATE], ITEM )
SELECT sunday11.Expr1, sunday11.ITEM
FROM sunday11;
```

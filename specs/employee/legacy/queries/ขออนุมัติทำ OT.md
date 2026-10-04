# คิวรีของ flow: ขออนุมัติทำ OT

มาโคร: [ขออนุมัติทำ OT](<../macros.md#m-ขออนุมัติทำ-OT>) — ลำดับคิวรีตามขั้นตอนของมาโคร (รวมคิวรี SELECT ที่ถูกอ้างถึง)

<a id="q-ขออนุมัติทำOT1"></a>
## ขออนุมัติทำOT1

- ชนิด: **DELETE** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [OT](<../tables.md#t-OT>)
- ใช้โดย: มาโคร [ขออนุมัติทำ OT](<../macros.md#m-ขออนุมัติทำ-OT>) (OpenQuery)

```sql
DELETE OT.ID
FROM OT
WHERE (((OT.ID) Is Null));
```

<a id="q-ขออนุมัติทำOT3"></a>
## ขออนุมัติทำOT3

- ชนิด: **APPEND** · แก้ล่าสุด 2021-05-12
- อ่านจาก: [E_WORK](<../tables.md#t-E_WORK>), [OT](<../tables.md#t-OT>)
- ใช้โดย: มาโคร [ขออนุมัติทำ OT](<../macros.md#m-ขออนุมัติทำ-OT>) (OpenQuery)

```sql
INSERT INTO E_WORK ( [DATE], ID, W_IN, W_OUT, TYPE, REST_T )
SELECT OT.Date, OT.ID, OT.W_IN, OT.W_OUT, OT.TYPE, OT.REST_T
FROM OT;
```

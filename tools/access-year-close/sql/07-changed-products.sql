-- ขั้น 7: รายชื่อสินค้าที่ยอดคงเหลือเปลี่ยน (ก่อน / หลัง / ผลต่าง) — ใส่รายงานให้ผู้ใช้เซ็น
SELECT b.PD_C, b.BAL AS BEFORE_BAL, IIf(IsNull(a.BAL), 0, a.BAL) AS AFTER_BAL, IIf(IsNull(a.BAL), 0, a.BAL) - b.BAL AS DIFF
FROM rpt_bal_before_2569 AS b LEFT JOIN rpt_bal_after_2569 AS a ON b.PD_C = a.PD_C
WHERE IIf(IsNull(a.BAL), 0, a.BAL) <> b.BAL
ORDER BY b.PD_C

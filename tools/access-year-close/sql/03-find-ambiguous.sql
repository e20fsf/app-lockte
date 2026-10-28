-- ขั้น 3: แถว pd ของ 1–30 ธ.ค. 2568 ใน P_ST ที่มีคู่ใน P_ADDST (วันที่ + REF_NO + SUF_NO + PD_C ตรงกัน) แต่ "จำนวนไม่ตรง" — ขอบเขตไม่ชัด
-- ไม่ลบ ไม่แตะ: เก็บในตาราง chk_P_ST_ambiguous_2569 ให้ผู้ใช้ตัดสินเป็นรายการ (ตรวจว่าจำนวนใน P_ST ถูกแก้หลังตัดสต็อก หรือคีย์ผิด)
SELECT s.* INTO chk_P_ST_ambiguous_2569
FROM P_ST AS s INNER JOIN (SELECT DISTINCT [DATE], REF_NO, SUF_NO, PD_C FROM P_ADDST WHERE MARK = "pd" AND [DATE] >= #12/1/2025# AND [DATE] < #12/31/2025#) AS a
ON (s.[DATE] = a.[DATE]) AND (s.REF_NO = a.REF_NO) AND (s.SUF_NO = a.SUF_NO) AND (s.PD_C = a.PD_C)
WHERE s.MARK = "pd" AND s.[DATE] >= #12/1/2025# AND s.[DATE] < #12/31/2025#
AND NOT EXISTS (SELECT 1 FROM bak_P_ST_dup_2569 AS b WHERE b.[DATE] = s.[DATE] AND b.REF_NO = s.REF_NO AND b.SUF_NO = s.SUF_NO AND b.PD_C = s.PD_C AND b.QTY = s.QTY AND b.MARK = s.MARK)

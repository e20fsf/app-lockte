-- ขั้น 3: แถว pd ของ 1–30 ธ.ค. 2568 ใน P_ST ที่มีคู่ใน P_ADDST (วันที่ + REF_NO + SUF_NO + PD_C ตรงกัน) แต่ "จำนวนไม่ตรง" — ขอบเขตไม่ชัด
-- ผู้ใช้ตัดสินแล้ว (28 ต.ค. 2569): ลบได้ แต่ต้องให้คนเห็นแถวก่อน — เก็บในตาราง chk_P_ST_ambiguous_2569 (รันหลังขั้น 2 และก่อนขั้นลบทุกอย่าง)
-- ระบุแถวได้แน่นอนด้วยคีย์ครบ 6 ช่อง (วันที่ + REF_NO + SUF_NO + PD_C + QTY + MARK) ของแถวในตารางนี้ — ขั้น 5c ลบด้วยคีย์ครบเหล่านั้นเท่านั้น
SELECT s.* INTO chk_P_ST_ambiguous_2569
FROM P_ST AS s INNER JOIN (SELECT DISTINCT [DATE], REF_NO, SUF_NO, PD_C FROM P_ADDST WHERE MARK = "pd" AND [DATE] >= #12/1/2025# AND [DATE] < #12/31/2025#) AS a
ON (s.[DATE] = a.[DATE]) AND (s.REF_NO = a.REF_NO) AND (s.SUF_NO = a.SUF_NO) AND (s.PD_C = a.PD_C)
WHERE s.MARK = "pd" AND s.[DATE] >= #12/1/2025# AND s.[DATE] < #12/31/2025#
AND NOT EXISTS (SELECT 1 FROM bak_P_ST_dup_2569 AS b WHERE b.[DATE] = s.[DATE] AND b.REF_NO = s.REF_NO AND b.SUF_NO = s.SUF_NO AND b.PD_C = s.PD_C AND b.QTY = s.QTY AND b.MARK = s.MARK)

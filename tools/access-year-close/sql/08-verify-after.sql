-- ขั้น 8: ตรวจหลังลบ — ต้องได้ 0 (ไม่เหลือแถว pd ธ.ค. 2568 ใน P_ST ที่คีย์ (วันที่ + REF_NO + SUF_NO + PD_C) ตรงกับ P_ADDST ไม่ว่าจำนวนจะตรงหรือไม่)
SELECT Count(*) AS REMAINING_DUPLICATES
FROM P_ST AS s INNER JOIN (SELECT DISTINCT [DATE], REF_NO, SUF_NO, PD_C FROM P_ADDST WHERE MARK = "pd" AND [DATE] >= #12/1/2025# AND [DATE] < #12/31/2025#) AS a
ON (s.[DATE] = a.[DATE]) AND (s.REF_NO = a.REF_NO) AND (s.SUF_NO = a.SUF_NO) AND (s.PD_C = a.PD_C)
WHERE s.MARK = "pd" AND s.[DATE] >= #12/1/2025# AND s.[DATE] < #12/31/2025#

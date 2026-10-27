-- ขั้น 8: ตรวจหลังลบ — ต้องได้ 0 (ไม่เหลือแถว pd ธ.ค. 2568 ใน P_ST ที่ซ้ำเป๊ะกับ P_ADDST)
SELECT Count(*) AS REMAINING_EXACT_DUPLICATES
FROM P_ST AS s INNER JOIN (SELECT DISTINCT [DATE], REF_NO, SUF_NO, PD_C, QTY, MARK FROM P_ADDST WHERE MARK = "pd" AND [DATE] >= #12/1/2025# AND [DATE] < #12/31/2025#) AS a
ON (s.[DATE] = a.[DATE]) AND (s.REF_NO = a.REF_NO) AND (s.SUF_NO = a.SUF_NO) AND (s.PD_C = a.PD_C) AND (s.QTY = a.QTY) AND (s.MARK = a.MARK)
WHERE s.MARK = "pd" AND s.[DATE] >= #12/1/2025# AND s.[DATE] < #12/31/2025#

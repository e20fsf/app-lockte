-- ขั้น 4c: รายละเอียดแถวขอบเขตไม่ชัดให้ผู้ใช้ดูและจดลงใบเซ็น — จำนวนใน P_ST (ที่จะลบ) เทียบกับจำนวนใน P_ADDST (ที่ปิดงวดแล้ว เก็บไว้)
SELECT s.[DATE], s.REF_NO, s.SUF_NO, s.PD_C, s.QTY AS QTY_P_ST, a.QTY AS QTY_P_ADDST
FROM chk_P_ST_ambiguous_2569 AS s INNER JOIN P_ADDST AS a ON (s.[DATE] = a.[DATE]) AND (s.REF_NO = a.REF_NO) AND (s.SUF_NO = a.SUF_NO) AND (s.PD_C = a.PD_C)
WHERE a.MARK = "pd"
ORDER BY s.[DATE], s.REF_NO, s.SUF_NO

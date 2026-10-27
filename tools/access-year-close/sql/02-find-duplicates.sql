-- ขั้น 2: หาแถว pd ของ 1–30 ธ.ค. 2568 ใน P_ST ที่ "ซ้ำเป๊ะ" กับ P_ADDST (วันที่ + REF_NO + SUF_NO + PD_C + QTY + MARK ตรงกันทุกช่อง — Q-47)
-- ผลเก็บในตารางสำรอง bak_P_ST_dup_2569 (ทั้งแถวของ P_ST) ซึ่งเป็นทั้งรายการที่จะลบและสำเนาไว้ย้อนกลับ
-- ช่องใดว่าง (NULL) ที่ใช้เทียบ → ไม่ถือว่าตรง จะไม่ถูกลบ (ปลอดภัยไว้ก่อน)
SELECT s.* INTO bak_P_ST_dup_2569
FROM P_ST AS s INNER JOIN (SELECT DISTINCT [DATE], REF_NO, SUF_NO, PD_C, QTY, MARK FROM P_ADDST WHERE MARK = "pd" AND [DATE] >= #12/1/2025# AND [DATE] < #12/31/2025#) AS a
ON (s.[DATE] = a.[DATE]) AND (s.REF_NO = a.REF_NO) AND (s.SUF_NO = a.SUF_NO) AND (s.PD_C = a.PD_C) AND (s.QTY = a.QTY) AND (s.MARK = a.MARK)
WHERE s.MARK = "pd" AND s.[DATE] >= #12/1/2025# AND s.[DATE] < #12/31/2025#

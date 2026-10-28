-- ขั้น 5c: ลบแถวขอบเขตไม่ชัดออกจาก P_ST ด้วยคีย์ครบ 6 ช่องของแถวใน chk_P_ST_ambiguous_2569 — รันเมื่อผู้ใช้ดูขั้น 4b/4c แล้วเซ็น
DELETE FROM P_ST
WHERE EXISTS (SELECT 1 FROM chk_P_ST_ambiguous_2569 AS c WHERE c.[DATE] = P_ST.[DATE] AND c.REF_NO = P_ST.REF_NO AND c.SUF_NO = P_ST.SUF_NO AND c.PD_C = P_ST.PD_C AND c.QTY = P_ST.QTY AND c.MARK = P_ST.MARK)
AND P_ST.MARK = "pd" AND P_ST.[DATE] >= #12/1/2025# AND P_ST.[DATE] < #12/31/2025#

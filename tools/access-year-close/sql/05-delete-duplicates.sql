-- ขั้น 5: ลบแถวที่ระบุไว้ในตารางสำรอง (เฉพาะแถวที่ซ้ำเป๊ะ ตามขั้น 2) ออกจาก P_ST — รันเมื่อขั้น 4 ตรวจแล้วเท่านั้น
-- ลบตามคีย์ครบ 6 ช่อง; ถ้า P_ST มีแถวซ้ำกันเองเป๊ะ ๆ หลายแถว แถวเหล่านั้นถูกลบพร้อมกัน (ดูจำนวนในขั้น 4 ที่นับจาก P_ST ทั้งหมดแล้ว)
DELETE FROM P_ST
WHERE EXISTS (SELECT 1 FROM bak_P_ST_dup_2569 AS b WHERE b.[DATE] = P_ST.[DATE] AND b.REF_NO = P_ST.REF_NO AND b.SUF_NO = P_ST.SUF_NO AND b.PD_C = P_ST.PD_C AND b.QTY = P_ST.QTY AND b.MARK = P_ST.MARK)
AND P_ST.MARK = "pd" AND P_ST.[DATE] >= #12/1/2025# AND P_ST.[DATE] < #12/31/2025#

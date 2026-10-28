-- ขั้น 5b: สำรองแถวขอบเขตไม่ชัดก่อนลบ (ทั้งแถวของ P_ST) ไว้ย้อนกลับได้
SELECT * INTO bak_P_ST_ambiguous_2569 FROM chk_P_ST_ambiguous_2569

-- ขั้น 4b: ตัวเลขที่ต้องตรวจก่อนลบแถวขอบเขตไม่ชัด — จำนวนแถวและ Σ จำนวน (หน่วย) ของแถวใน chk_P_ST_ambiguous_2569
-- คาดไว้ประมาณ 1 แถว (ซ้อม 27 ต.ค. 2569); ถ้ามากกว่า 10 แถวให้หยุดและถามทีมพัฒนาก่อน
SELECT Count(*) AS AMBIGUOUS_ROWS, Sum(QTY) AS AMBIGUOUS_UNITS FROM chk_P_ST_ambiguous_2569

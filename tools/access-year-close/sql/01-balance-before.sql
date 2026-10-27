-- ขั้น 1: เก็บยอดคงเหลือต่อสินค้า "ก่อนลบ" (ยอดคงเหลือ = Σ QTY ของ P_ST ทุกแถว รวมยอดยกมา stock — BR-007)
SELECT PD_C, Sum(QTY) AS BAL INTO rpt_bal_before_2569 FROM P_ST GROUP BY PD_C

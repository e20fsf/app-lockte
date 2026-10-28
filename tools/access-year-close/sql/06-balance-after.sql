-- ขั้น 6: เก็บยอดคงเหลือต่อสินค้า "หลังลบ"
SELECT PD_C, Sum(QTY) AS BAL INTO rpt_bal_after_2569 FROM P_ST GROUP BY PD_C

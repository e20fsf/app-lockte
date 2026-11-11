using Lockte.Web.Reports.Order;
using Lockte.Web.Reports.Purch;

namespace Lockte.Web.Tests;

/// <summary>สรุปจำนวนใบขออนุมัติสั่งซื้อรายเดือนแยกตามแผนก (#420), without a database.</summary>
public sealed class PurchRequestMonthlyReportTests
{
    private static readonly Dictionary<int, string> Departments = new() { [1] = "แผนกหนึ่ง", [2] = "แผนกสอง", [3] = "แผนกสาม" };

    private static readonly (int, DateOnly)[] Requests =
    [
        (1, new(2026, 1, 5)), (1, new(2026, 1, 31)), (1, new(2026, 3, 1)),
        (2, new(2026, 2, 10)), (2, new(2026, 3, 15)),
        (3, new(2025, 12, 31)), (3, new(2027, 1, 1)), // other years
    ];

    [Fact]
    public void Counts_requests_per_department_and_month_with_totals()
    {
        var table = PurchRequestMonthlyReports.Build(Requests, Departments, 2026, 1, 3);
        Assert.Equal(["แผนก", "ม.ค.", "ก.พ.", "มี.ค.", "รวม"], table.Columns.Select(c => c.Header));
        Assert.Equal(2, table.Rows.Count); // แผนกสาม has none in 2026
        var one = table.Rows.Single(r => (string?)r[0] == "แผนกหนึ่ง");
        var two = table.Rows.Single(r => (string?)r[0] == "แผนกสอง");
        Assert.Equal(["แผนกหนึ่ง", 2, 0, 1, 3], one);
        Assert.Equal(["แผนกสอง", 0, 1, 1, 2], two);
        Assert.Equal(["รวมทั้งหมด", 2, 1, 2, 5], table.TotalRow);
        Assert.Contains("2569", table.Filter);
    }

    [Fact]
    public void Only_the_chosen_months_count_and_an_empty_range_has_no_total_row()
    {
        var table = PurchRequestMonthlyReports.Build(Requests, Departments, 2026, 2, 2);
        Assert.Equal(["แผนก", "ก.พ.", "รวม"], table.Columns.Select(c => c.Header));
        Assert.Equal([["แผนกสอง", 1, 1]], table.Rows);
        var none = PurchRequestMonthlyReports.Build(Requests, Departments, 2026, 6, 8);
        Assert.Empty(none.Rows);
        Assert.Null(none.TotalRow);
    }

    [Fact]
    public void The_excel_file_is_the_same_table()
    {
        var table = PurchRequestMonthlyReports.Build(Requests, Departments, 2026, 1, 3);
        Assert.True(XlsxWriter.Write(table).Length > 0);
    }

    [Theory]
    [InlineData(null, 1, 12, "กรุณาเลือกปี")]
    [InlineData(2026, null, 12, "กรุณาเลือกเดือน")]
    [InlineData(2026, 5, 3, "ตั้งแต่เดือนต้องไม่หลังถึงเดือน")]
    [InlineData(2026, 1, 12, null)]
    public void The_range_is_checked(int? year, int? from, int? to, string? problem)
        => Assert.Equal(problem, PurchRequestMonthlyReports.Problem(year, from, to));
}

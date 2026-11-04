using Lockte.Hr;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Reports.Hr;

/// <summary>สรุปใบลาออก as a <see cref="SummaryTable"/> for the Excel button: the rows of the printed report, one sheet, with รวม at the end.</summary>
public static class ResignationSummaryTable
{
    public static SummaryTable Build(ResignationSummaryReport report)
    {
        var columns = new List<SummaryColumn> { new("ที่", SummaryFormat.Integer), new("เดือน"), new("จำนวนใบลาออก (ใบ)", SummaryFormat.Integer) };
        var rows = report.Months.Select((m, i) => new object?[] { i + 1, ResignationSummaryReportView.MonthText(m), m.Count }).ToList();
        var filter = $"ตั้งแต่วันที่ {ReportText.Date(report.From)} ถึงวันที่ {ReportText.Date(report.To)} แผนก {report.DepartmentName ?? "ทั้งหมด"}";
        return new SummaryTable(ResignationSummaryReportView.Title, filter, columns, rows, [null, "รวม", report.Total]);
    }
}

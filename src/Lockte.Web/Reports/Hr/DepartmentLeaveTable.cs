using Lockte.Hr;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Reports.Hr;

/// <summary>
/// RPT-31 รายงานการลางานแผนก… as a <see cref="SummaryTable"/> for the Excel button of the form (#355): the rows of the report in its order —
/// ทุกแผนก grouped by แผนก with each แผนก's รวม and เปอร์เซ็นต์ — then รวมทั้งหมด and คิดเป็นเปอร์เซ็นต์, one sheet, days to 1 decimal as printed.
/// </summary>
public static class DepartmentLeaveTable
{
    public static SummaryTable Build(DepartmentLeaveReport report)
    {
        var types = DepartmentLeaveReport.Types;
        var columns = new List<SummaryColumn>
        {
            new("ที่", SummaryFormat.Integer), new("รหัส"), new("ชื่อ-นามสกุล"), new("แผนก"), new("เริ่มทำงาน"),
        };
        columns.AddRange(types.Select(t => new SummaryColumn(DepartmentLeaveReportView.Label(t), SummaryFormat.Quantity)));
        columns.Add(new("รวม", SummaryFormat.Quantity));

        object?[] Totals(string label, IReadOnlyCollection<DepartmentLeaveLine> lines)
            => [null, label, null, null, null, .. types.Select(t => (object?)Days(lines.Sum(l => l.DaysOf(t)))), Days(lines.Sum(l => l.Total))];
        object?[] Percents(IReadOnlyCollection<DepartmentLeaveLine> lines, int headcount)
            => [null, "คิดเป็นเปอร์เซ็นต์", null, null, null,
                .. types.Select(t => Percent(report.PercentOf(lines.Sum(l => l.DaysOf(t)), headcount))), Percent(report.PercentOf(lines.Sum(l => l.Total), headcount))];

        var rows = new List<object?[]>();
        var number = 0;
        object?[] Line(DepartmentLeaveLine l)
            => [++number, l.EmployeeCode, l.EmployeeName, l.DepartmentName ?? report.DepartmentName, l.StartDate is { } s ? ReportText.Date(s) : null,
                .. types.Select(t => (object?)Days(l.DaysOf(t))), Days(l.Total)];

        var groups = report.ByDepartment;
        if (groups.Count == 0) rows.AddRange(report.Lines.Select(Line));
        else
            foreach (var group in groups)
            {
                var members = group.ToList();
                rows.AddRange(members.Select(Line));
                rows.Add(Totals($"รวมแผนก{group.Key} {members.Count} คน", members));
                if (report.WorkDays is > 0) rows.Add(Percents(members, members.Count));
            }

        var grand = Totals($"{(groups.Count == 0 ? "รวมทั้งหมด" : "รวมทั้งหมดทุกแผนก")} {report.Employees} คน", report.Lines);
        var title = DepartmentLeaveReportView.TitleOf(report);
        var filter = $"ตั้งแต่วันที่ {ReportText.Date(report.From)} ถึงวันที่ {ReportText.Date(report.To)} จำนวนวันทำงาน {report.WorkDays?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""} วัน";
        if (report.WorkDays is > 0)
        {
            rows.Add(grand);
            return new SummaryTable(title, filter, columns, rows, Percents(report.Lines, report.Employees));
        }
        return new SummaryTable(title, filter, columns, rows, grand);
    }

    private static decimal Days(double value) => Math.Round((decimal)value, 1, MidpointRounding.AwayFromZero);

    private static object? Percent(double? value) => value is { } v ? Math.Round((decimal)v, 2, MidpointRounding.AwayFromZero) : null;
}

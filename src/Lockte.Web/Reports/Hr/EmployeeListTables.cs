using Lockte.Hr;
using Lockte.Web.Reports.Order;

namespace Lockte.Web.Reports.Hr;

/// <summary>
/// The employee lists (RPT-20, RPT-21) as <see cref="SummaryTable"/>s for the "ส่งออก Excel" button of SCR-04 (#344, #345): the same
/// rows, order, dates (พ.ศ. text, as printed) and footer as the report, so the file is the page cell for cell.
/// </summary>
public static class EmployeeListTables
{
    public static SummaryTable OnProbation(IEnumerable<EmployeeDto> employees)
    {
        var list = OnProbationReportView.Order(employees);
        return new SummaryTable(OnProbationReportView.Title, null,
            [new("ลำดับ", SummaryFormat.Integer), new("รหัส"), new("ชื่อ-นามสกุล"), new("แผนก"), new("ตำแหน่ง"), new("เริ่ม"), new("ประเภท"), new("กะ"), new("ถึงกำหนด")],
            list.Select((e, i) => new object?[]
            {
                i + 1, e.Code, e.Name, e.DepartmentName, e.Position, Date(e.StartDate), e.EmployeeType?.ToString(System.Globalization.CultureInfo.InvariantCulture), e.ShiftCode,
                Date(e.StartDate?.AddDays(OnProbationReportView.DueDays)),
            }).ToList(),
            [null, $"รวมทั้งหมด {list.Count} คน", null, null, null, null, null, null, null]);
    }

    public static SummaryTable Roster(DepartmentRoster roster)
        => new(roster.Title, null,
            [new("ลำดับ", SummaryFormat.Integer), new("รหัส"), new("ชื่อ-นามสกุล"), new("เริ่มทำงาน"), new("ตำแหน่ง"), new("อายุงาน(ปี)", SummaryFormat.Integer),
             new("(เดือน)", SummaryFormat.Integer), new("อายุ", SummaryFormat.Integer), new("เงินเดือน", SummaryFormat.Integer), new("รายวัน", SummaryFormat.Money)],
            roster.Lines.Select((l, i) => new object?[]
            {
                i + 1, l.Code, l.Name, Date(l.StartDate), l.Position, l.ServiceYears, l.ServiceMonths, l.Age, Whole(l.Salary), Money(l.DailyWage),
            }).ToList(),
            [null, $"รวม {roster.Lines.Count} คน   ชาย {roster.Male} คน   หญิง {roster.Female} คน", null, null, null, null, null, Whole(roster.AverageAge), null, null]);

    public static SummaryTable Working(WorkingEmployeeList list)
        => new(WorkingEmployeesView.Title, null,
            [new("ลำดับ", SummaryFormat.Integer), new("รหัส"), new("ชื่อ-นามสกุล"), new("แผนก"), new("ตำแหน่ง"), new("เกิด"), new("เริ่มทำงาน"), new("ประเภท"),
             new("อายุ", SummaryFormat.Integer), new("โรงพยาบาล")],
            list.Lines.Select((l, i) => new object?[]
            {
                i + 1, l.Code, l.Name, l.DepartmentName, l.Position, Date(l.BirthDate), Date(l.StartDate),
                l.EmployeeType?.ToString(System.Globalization.CultureInfo.InvariantCulture), Whole(l.Age), l.Hospital,
            }).ToList(),
            [null, $"รวมทั้งหมด {list.Lines.Count} คน   ชาย {list.Male} คน   หญิง {list.Female} คน", null, null, null, null, null, null, Whole(list.AverageAge), null]);

    public static SummaryTable Register(IReadOnlyList<EmployeeRegisterLine> lines, string title)
        => new(title, null,
            [new("ที่", SummaryFormat.Integer), new("รหัส"), new("ชื่อ-นามสกุล"), new("วันเกิด"), new("เพศ"), new("สัญชาติ"), new("ภูมิลำเนา"), new("ที่อยู่ปัจจุบัน"),
             new("วุฒิการศึกษา"), new("วันเริ่มทำงาน"), new("ตำแหน่ง"), new("แผนก"), new("เงินเดือนหรือค่าแรง/วัน", SummaryFormat.Money), new("วันสิ้นสุดของการจ้าง")],
            lines.Select((l, i) => new object?[]
            {
                i + 1, l.Code, l.Name, Date(l.BirthDate), l.Sex, l.Nationality, l.RegisteredAddress, l.CurrentAddress, l.Education, Date(l.StartDate),
                l.Position, l.DepartmentName, Money(l.Wage), Date(l.ResignDate),
            }).ToList());

    private static string? Date(DateOnly? date) => date is { } d ? ReportText.Date(d) : null;

    /// <summary>A whole number as the report prints it (<see cref="EmployeeListText.Whole"/>): half away from zero.</summary>
    private static decimal Money(double value) => Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);

    private static decimal? Whole(double? value) => value is { } v ? Math.Round((decimal)v, 0, MidpointRounding.AwayFromZero) : null;
}

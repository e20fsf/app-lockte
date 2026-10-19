namespace Lockte.Hr;

/// <summary>
/// BR-002 รวมยอดต่อพนักงานในงวด (คิวรี คำนวณเวลาทำงาน3–19, 13) on rows already read: no database, so the rules are
/// in one place. Every ⚠ of the legacy is kept (#41): leave across a cut day is in no period (Q-18), a newcomer gets no
/// SUN in the first period, START blank gets no SUN/HOLI (Access compares Null as false).
/// </summary>
internal static class PeriodTime
{
    internal sealed record Person(int Id, string Code, string Name, DateOnly? StartDate, DateOnly? ResignDate, int? EmployeeType);

    internal sealed record Work(int EmployeeId, string? Type, TimeOnly? WorkIn, TimeOnly? WorkOut, int RestMinutes);

    internal sealed record LeaveDays(int EmployeeId, DateOnly FromDate, DateOnly ToDate, double Days, bool? Paid);

    public static List<EmployeeTimeTotal> Total(PayPeriodDates period, IEnumerable<Person> people, IEnumerable<Work> work,
        IEnumerable<LeaveDays> leaves, IReadOnlyCollection<DateOnly> weeklyHolidays, IReadOnlyCollection<DateOnly> publicHolidays)
    {
        var (from, to) = (period.From, period.To);
        var weekly = weeklyHolidays.Where(d => d >= from && d <= to).ToList();
        var holidays = publicHolidays.Where(d => d >= from && d <= to).ToList();

        // BR-001: WORK_T per row (rows without both times have none, as Null in Access); Σ by TYPE per employee.
        var hours = work
            .Where(w => w.Type is not null && w.WorkIn is not null && w.WorkOut is not null)
            .GroupBy(w => (w.EmployeeId, w.Type))
            .ToDictionary(g => g.Key, g => g.Sum(w => WorkDuration.WorkT(w.Type, w.WorkIn!.Value, w.WorkOut!.Value, w.RestMinutes)));
        // L / L(NO): only leave inside the period (FROM_D ≥ FROM_DA AND TO_D ≤ TO_DA).
        var inside = leaves.Where(l => l.FromDate >= from && l.ToDate <= to).ToList();
        var paid = inside.Where(l => l.Paid == true).GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(l => l.Days));
        var unpaid = inside.Where(l => l.Paid == false).GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(l => l.Days));

        var totals = new List<EmployeeTimeTotal>();
        foreach (var p in people)
        {
            if (p.Code.StartsWith("LT", StringComparison.OrdinalIgnoreCase)) continue; // Not Like "LT*" (Q-10)
            double Sum(string type) => hours.GetValueOrDefault((p.Id, type));
            var stillThere = p.ResignDate is null || p.ResignDate > from;
            var holi = p.StartDate is { } start && stillThere ? holidays.Count(d => d >= start) : 0;
            var sun = (p.ResignDate is null && p.StartDate <= from) || p.ResignDate > from ? weekly.Count : 0;
            var sunDay = p.EmployeeType == EmployeeTypes.Monthly && p.ResignDate is { } resign && resign >= from && resign <= to
                ? weekly.Count(d => d <= resign) : 0;
            var total = new EmployeeTimeTotal(p.Id, p.Code, p.Name,
                Sum(WorkTypes.Normal), paid.GetValueOrDefault(p.Id), unpaid.GetValueOrDefault(p.Id), Sum(WorkTypes.Overtime),
                Sum(WorkTypes.Sunday), Sum(WorkTypes.SundayOvertime), Sum(WorkTypes.Holiday), Sum(WorkTypes.HolidayOvertime), holi, sun, sunDay);
            if (total.Normal + total.PaidLeave + total.Sunday + total.Holiday + holi + sun > 0) totals.Add(total);
        }
        return totals;
    }
}

using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel;

namespace Lockte.Web.Tests;

/// <summary>
/// ใบขออนุมัติทำ OT (#60, SCR-14, BR-012) through the public services of module พนักงานและค่าแรง: printing a sheet
/// writes its rows into เวลาทำงาน and returns what RPT-16 prints.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class OvertimeSheetTests(LocalDbFixture db)
{
    private static int lastCode;

    /// <summary>"V" keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)).</summary>
    private static string NewCode() => $"V{Interlocked.Increment(ref lastCode):D3}";

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    /// <summary>Far-away days of their own, clear of the days the other time tests use.</summary>
    private static DateOnly NewDay() => new DateOnly(Random.Shared.Next(8100, 8990), 1, 1).AddDays(Random.Shared.Next(0, 350));

    private static readonly TimeOnly OtIn = new(17, 20), OtOut = new(20, 20);

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private async Task<DepartmentDto> NewDepartmentAsync()
    {
        var sectionId = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveSectionAsync(new SectionInput(null, Unique("ผลิต"), 1, null)))).Id!.Value;
        var id = (await AsHrAsync(s => s.Get<OrgRegistry>().SaveDepartmentAsync(new DepartmentInput(
            null, Unique("D"), Unique("ประกอบ"), "V", 3, sectionId, null, [], null, null)))).Id!.Value;
        return (await AsHrAsync(s => s.Get<OrgDirectory>().GetDepartmentAsync(id)))!;
    }

    private async Task<EmployeeDto> NewEmployeeAsync(string? duty = null, string? grade2 = null, DateOnly? resigned = null, int? departmentId = null)
    {
        var shift = await AsHrAsync(async s => (await s.Get<OrgDirectory>().FindShiftByCodeAsync("A"))!);
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], ShiftId = shift.Id, Duty = duty,
            Evaluation2Grade = grade2, ResignDate = resigned, DepartmentId = departmentId,
        }));
        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    private static OvertimeSheetInput Sheet(DateOnly date, int? departmentId, params OvertimeSheetLineInput[] lines)
        => new(date, departmentId, OtIn, OtOut, 0, WorkTypes.Overtime, lines);

    private static OvertimeSheetLineInput Line(EmployeeDto? employee, string? memo = null) => new(employee?.Id, memo);

    private Task<OvertimeSheetOutcome> PrintAsync(OvertimeSheetInput input) => AsHrAsync(s => s.Get<OvertimeSheetRegistry>().PrintAsync(input));

    private Task<IReadOnlyList<WorkTimeDto>> DayAsync(DateOnly date) => AsHrAsync(s => s.Get<WorkTimeDirectory>().ListAsync(date));

    [Fact] // #411: คนละช่วงเวลา แผนกเดียวกัน วันเดียวกัน → ใบเดียว แต่ละแถวมีเวลา/ประเภทของตน และบันทึกเวลาทำงานตามนั้น
    public async Task One_sheet_holds_lines_of_different_times_each_saved_with_its_own()
    {
        var day = NewDay();
        var early = await NewEmployeeAsync();
        var late = await NewEmployeeAsync();
        var plain = await NewEmployeeAsync(); // no times of its own → the sheet's

        var outcome = await PrintAsync(Sheet(day, null,
            new OvertimeSheetLineInput(early.Id, "ช่วงบ่าย", new TimeOnly(15, 20), new TimeOnly(18, 20), 0, "O2"),
            new OvertimeSheetLineInput(late.Id, "ช่วงเย็น", new TimeOnly(17, 20), new TimeOnly(20, 20), 20, null),
            Line(plain)));

        Assert.True(outcome.Result.Succeeded, string.Join(", ", outcome.Result.Errors));
        var printed = outcome.Sheet!.Lines.ToDictionary(l => l.EmployeeCode);
        Assert.Equal((new TimeOnly(15, 20), new TimeOnly(18, 20)), (printed[early.Code].WorkIn, printed[early.Code].WorkOut));
        Assert.Equal((new TimeOnly(17, 20), new TimeOnly(20, 20)), (printed[late.Code].WorkIn, printed[late.Code].WorkOut));
        Assert.Equal((OtIn, OtOut), (printed[plain.Code].WorkIn, printed[plain.Code].WorkOut));
        var rows = (await DayAsync(day)).ToDictionary(r => r.EmployeeCode);
        Assert.Equal(("O2", new TimeOnly(15, 20), new TimeOnly(18, 20), 0), (rows[early.Code].Type, rows[early.Code].WorkIn, rows[early.Code].WorkOut, rows[early.Code].RestMinutes));
        Assert.Equal(("O1", new TimeOnly(17, 20), new TimeOnly(20, 20), 20), (rows[late.Code].Type, rows[late.Code].WorkIn, rows[late.Code].WorkOut, rows[late.Code].RestMinutes));
        Assert.Equal(("O1", OtIn, OtOut), (rows[plain.Code].Type, rows[plain.Code].WorkIn, rows[plain.Code].WorkOut));
    }

    [Fact] // BR-012 #4: กดพิมพ์ → คัดลอกทุกแถวเข้า E_WORK (DATE, ID, W_IN, W_OUT, TYPE, REST_T) → RPT-16 เรียงตามรหัส
    public async Task Printing_a_sheet_saves_one_time_row_per_employee_and_returns_the_lines_by_code()
    {
        var day = NewDay();
        var department = await NewDepartmentAsync();
        var second = await NewEmployeeAsync();
        var first = await NewEmployeeAsync(); // a later code, keyed first below
        (first, second) = string.CompareOrdinal(first.Code, second.Code) < 0 ? (first, second) : (second, first);

        var outcome = await PrintAsync(Sheet(day, department.Id, Line(second, "ช่วยแผนกประกอบ"), Line(first, "D11 ฉีดก้ามเบรก"))
            with { WorkIn = new TimeOnly(17, 20), WorkOut = new TimeOnly(21, 20), RestMinutes = 20, Type = "O2" });

        Assert.True(outcome.Result.Succeeded, string.Join(", ", outcome.Result.Errors));
        Assert.Equal(2, outcome.Result.Id);
        var sheet = outcome.Sheet!;
        Assert.Equal((day, department.Name), (sheet.Date, sheet.DepartmentName));
        Assert.All(sheet.Lines, l => Assert.Equal((new TimeOnly(17, 20), new TimeOnly(21, 20)), (l.WorkIn, l.WorkOut)));
        Assert.Equal([(first.Code, first.Name, "D11 ฉีดก้ามเบรก"), (second.Code, second.Name, "ช่วยแผนกประกอบ")],
            sheet.Lines.Select(l => (l.EmployeeCode, l.EmployeeName, l.Memo)));

        var rows = await DayAsync(day);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(("O2", (TimeOnly?)new TimeOnly(17, 20), (TimeOnly?)new TimeOnly(21, 20), 20),
            (r.Type, r.WorkIn, r.WorkOut, r.RestMinutes)));
        Assert.Equal([first.Id, second.Id], rows.Select(r => r.EmployeeId));
    }

    [Fact] // แก้ครั้งที่ 12: พิมพ์ช่วง 15:20 แล้วกดหลายกะพิมพ์ช่วง 17:20 → ใบที่สองมีทุกคนทุกช่วงของวัน/แผนกนั้น และบันทึกเวลาเฉพาะแถวใหม่
    public async Task A_later_print_of_the_same_date_and_department_prints_every_range_so_far()
    {
        var day = NewDay();
        var department = await NewDepartmentAsync();
        var a = await NewEmployeeAsync();
        var b = await NewEmployeeAsync();
        var c = await NewEmployeeAsync();
        var elsewhere = await NewEmployeeAsync();
        await PrintAsync(Sheet(day, department.Id, new OvertimeSheetLineInput(a.Id, "ควบคุมงาน", new TimeOnly(15, 0), new TimeOnly(17, 0))));
        await PrintAsync(Sheet(day, null, Line(elsewhere, "แผนกอื่น")));                    // no แผนก: another sheet
        await PrintAsync(Sheet(day.AddDays(1), department.Id, Line(elsewhere, "วันอื่น"))); // another day: another sheet

        var outcome = await PrintAsync(Sheet(day, department.Id, Line(c, "รับและส่งสินค้า"), Line(b, "ขับรถส่งของ")));

        Assert.True(outcome.Result.Succeeded, string.Join(", ", outcome.Result.Errors));
        Assert.Equal(2, outcome.Result.Id);
        Assert.Equal(
            new[] { (a.Code, new TimeOnly(15, 0), new TimeOnly(17, 0), "ควบคุมงาน"), (b.Code, OtIn, OtOut, "ขับรถส่งของ"), (c.Code, OtIn, OtOut, "รับและส่งสินค้า") }
                .OrderBy(l => l.Item1, StringComparer.Ordinal),
            outcome.Sheet!.Lines.Select(l => (l.EmployeeCode, l.WorkIn, l.WorkOut, l.Memo!)));
        Assert.Equal(4, (await DayAsync(day)).Count); // a, b, c and the sheet without แผนก — nothing saved twice
        Assert.Equal(outcome.Sheet.Lines, await AsHrAsync(s => s.Get<OvertimeSheetRegistry>().GetDayLinesAsync(day, department.Id)));
    }

    [Fact] // แก้ครั้งที่ 12: กดพิมพ์โดยไม่มีแถวใหม่ → พิมพ์ใบเดิมของวัน/แผนกนั้นซ้ำ ไม่บันทึกอะไรเพิ่ม
    public async Task Printing_without_new_lines_prints_the_sheet_so_far_again()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();
        await PrintAsync(Sheet(day, null, Line(employee, "งานเดิม")));

        var outcome = await PrintAsync(Sheet(day, null));

        Assert.Equal(0, outcome.Result.Id);
        var line = Assert.Single(outcome.Sheet!.Lines);
        Assert.Equal((employee.Code, "งานเดิม"), (line.EmployeeCode, line.Memo));
        Assert.Single(await DayAsync(day));
    }

    [Fact] // แก้ครั้งที่ 12: the sheet follows เวลาทำงาน — an edited time shows, a deleted row leaves the sheet
    public async Task The_sheet_follows_edits_and_deletes_of_the_work_time_rows()
    {
        var day = NewDay();
        var kept = await NewEmployeeAsync();
        var removed = await NewEmployeeAsync();
        await PrintAsync(Sheet(day, null, Line(kept, "แก้เวลา"), Line(removed)));
        var rows = (await DayAsync(day)).ToDictionary(r => r.EmployeeId);
        var edit = rows[kept.Id];
        var saved = await AsHrAsync(s => s.Get<WorkTimeRegistry>().SaveAsync(new WorkTimeInput(edit.Id, day, kept.Id, edit.Type, edit.WorkIn,
            new TimeOnly(21, 20), edit.RestMinutes, null, null, edit.RowVersion)));
        Assert.True(saved.Succeeded, string.Join(", ", saved.Errors));
        var deleted = await AsHrAsync(s => s.Get<WorkTimeRegistry>().DeleteAsync(rows[removed.Id].Id, rows[removed.Id].RowVersion));
        Assert.True(deleted.Succeeded, string.Join(", ", deleted.Errors));

        var line = Assert.Single(await AsHrAsync(s => s.Get<OvertimeSheetRegistry>().GetDayLinesAsync(day)));

        Assert.Equal((kept.Code, new TimeOnly(21, 20), "แก้เวลา"), (line.EmployeeCode, line.WorkOut, line.Memo));
    }

    [Fact] // แก้ครั้งที่ 12: เลือกวัน → ทุกคนที่มีเวลา OT วันนั้น (รวมที่บันทึกจากหน้าอื่น/ย้ายจาก Access) กรองแผนกและเวลาเข้าได้
    public async Task The_day_lists_every_overtime_row_by_department_and_start_time()
    {
        var day = NewDay();
        var department = await NewDepartmentAsync();
        var keyedElsewhere = await NewEmployeeAsync(duty: "รับและส่งสินค้า", departmentId: department.Id);
        var onSheet = await NewEmployeeAsync(departmentId: department.Id);
        var otherDepartment = await NewEmployeeAsync(duty: "ขับรถ");
        async Task KeyAsync(EmployeeDto e, string type, TimeOnly workIn, TimeOnly workOut)
        {
            var result = await AsHrAsync(s => s.Get<WorkTimeRegistry>().SaveAsync(new WorkTimeInput(null, day, e.Id, type, workIn, workOut, 0, null, null, null)));
            Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        }
        await KeyAsync(keyedElsewhere, WorkTypes.Overtime, new TimeOnly(15, 20), new TimeOnly(18, 20));
        await KeyAsync(keyedElsewhere, WorkTypes.Normal, new TimeOnly(8, 0), new TimeOnly(17, 0)); // เวลาปกติ: not OT
        await KeyAsync(otherDepartment, WorkTypes.Overtime, new TimeOnly(17, 20), new TimeOnly(20, 20));
        await PrintAsync(Sheet(day, null, new OvertimeSheetLineInput(onSheet.Id, "ควบคุมงาน", new TimeOnly(17, 20), new TimeOnly(20, 20))));

        Task<IReadOnlyList<OvertimeSheetLine>> ListAsync(int? departmentId, TimeOnly? workIn)
            => AsHrAsync(s => s.Get<OvertimeSheetRegistry>().GetDayLinesAsync(day, departmentId, workIn));

        var all = await ListAsync(null, null);
        Assert.Equal(new[] { keyedElsewhere.Code, onSheet.Code, otherDepartment.Code }.Order(StringComparer.Ordinal), all.Select(l => l.EmployeeCode));
        var elsewhere = all.Single(l => l.EmployeeCode == keyedElsewhere.Code);
        Assert.Equal(("รับและส่งสินค้า", department.Name, new TimeOnly(15, 20)), (elsewhere.Memo, elsewhere.DepartmentName, elsewhere.WorkIn)); // หน้าที่, the employee's แผนก
        Assert.Equal("ควบคุมงาน", all.Single(l => l.EmployeeCode == onSheet.Code).Memo);
        Assert.Equal(new[] { keyedElsewhere.Code, onSheet.Code }.Order(StringComparer.Ordinal), (await ListAsync(department.Id, null)).Select(l => l.EmployeeCode));
        Assert.Equal(new[] { onSheet.Code, otherDepartment.Code }.Order(StringComparer.Ordinal), (await ListAsync(null, new TimeOnly(17, 20))).Select(l => l.EmployeeCode));
        Assert.Equal([onSheet.Code], (await ListAsync(department.Id, new TimeOnly(17, 20))).Select(l => l.EmployeeCode));

        var printed = await PrintAsync(Sheet(day, department.Id) with { ListWorkIn = new TimeOnly(15, 20) });
        Assert.Equal([keyedElsewhere.Code], printed.Sheet!.Lines.Select(l => l.EmployeeCode)); // the sheet prints what the screen lists
    }

    [Fact] // BR-012 #4 ลบแถวที่ ID ว่าง ก่อนคัดลอก
    public async Task Lines_without_an_employee_are_dropped()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();

        var outcome = await PrintAsync(Sheet(day, null, Line(null, "ว่าง"), Line(employee), Line(null)));

        Assert.True(outcome.Result.Succeeded, string.Join(", ", outcome.Result.Errors));
        Assert.Equal(employee.Code, Assert.Single(outcome.Sheet!.Lines).EmployeeCode);
        Assert.Equal(OvertimeSheetRegistry.AllDepartmentsLabel, outcome.Sheet.DepartmentName); // แก้ครั้งที่ 12: no แผนก = ทุกแผนก
        Assert.Equal(employee.Id, Assert.Single(await DayAsync(day)).EmployeeId);
    }

    [Fact] // D-6: a sheet without anyone still prints (as "ไม่มีข้อมูล") and saves nothing
    public async Task A_sheet_without_employees_prints_empty_and_saves_nothing()
    {
        var day = NewDay();

        var outcome = await PrintAsync(Sheet(day, null, Line(null)));

        Assert.True(outcome.Result.Succeeded, string.Join(", ", outcome.Result.Errors));
        Assert.Equal(0, outcome.Result.Id);
        Assert.Empty(outcome.Sheet!.Lines);
        Assert.Empty(await DayAsync(day));
    }

    [Fact] // BR-013 ⚠ kept: as the legacy APPEND, the same person twice (or a second print) adds the rows again
    public async Task The_same_employee_twice_gives_two_rows()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();

        await PrintAsync(Sheet(day, null, Line(employee), Line(employee)));

        Assert.Equal(2, (await DayAsync(day)).Count);
    }

    [Fact] // the values BR-001 needs, as WorkTimeRegistry asks for them; nothing is saved when one is wrong
    public async Task A_sheet_needs_a_date_a_type_both_times_and_known_employees_and_department()
    {
        var day = NewDay();
        var employee = await NewEmployeeAsync();
        var ok = Sheet(day, null, Line(employee));

        Assert.Equal(["กรุณากรอกวันที่"], (await PrintAsync(ok with { Date = null })).Result.Errors);
        Assert.Equal(["กรุณาเลือกประเภท"], (await PrintAsync(ok with { Type = " " })).Result.Errors);
        Assert.Single((await PrintAsync(ok with { Type = "X" })).Result.Errors, e => e.Contains("O1"));
        Assert.Equal(["กรุณากรอกเวลาเริ่มและเวลาเลิก"], (await PrintAsync(ok with { WorkOut = null })).Result.Errors);
        Assert.Equal(["นาทีพักต้องไม่ติดลบ"], (await PrintAsync(ok with { RestMinutes = -10 })).Result.Errors);
        Assert.Equal(["ไม่พบพนักงานที่เลือก"], (await PrintAsync(ok with { Lines = [Line(employee), new(-1, null)] })).Result.Errors);
        Assert.Equal(["ไม่พบแผนกที่เลือก"], (await PrintAsync(ok with { DepartmentId = -1 })).Result.Errors);
        Assert.Single((await PrintAsync(ok with { Lines = [Line(employee, new string('ก', 256))] })).Result.Errors, e => e.Contains("255"));
        Assert.Empty(await DayAsync(day));
        Assert.Null((await PrintAsync(ok with { Type = null })).Sheet);
    }

    [Fact] // BR-012 #1–2 not kept: the sheet is not a table (#41) and เกรดประเมินครั้งที่ 2 is never cleared
    public async Task Printing_a_sheet_leaves_the_second_evaluation_grade_alone()
    {
        var employee = await NewEmployeeAsync(grade2: "B");

        await AsHrAsync(s => s.Get<OvertimeSheetRegistry>().GetChoicesAsync(NewDay()));
        await PrintAsync(Sheet(NewDay(), null, Line(employee)));

        Assert.Equal("B", (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(employee.Id)))!.Evaluation2Grade);
    }

    [Fact] // ฟอร์ม OT1: รหัส from EMPLO with RESIGN blank, by code; picking one fills ชื่อ and MEMO = หน้าที่ (WORK)
    public async Task The_choices_are_working_employees_with_their_duty_and_the_shift_overtime_times()
    {
        var day = NewDay();
        var working = await NewEmployeeAsync(duty: "D11 ฉีดก้ามเบรก");
        var resigned = await NewEmployeeAsync(resigned: day.AddDays(-30));

        var choices = await AsHrAsync(s => s.Get<OvertimeSheetRegistry>().GetChoicesAsync(day));

        var item = Assert.Single(choices.Employees, e => e.Id == working.Id);
        Assert.Equal((working.Code, working.Name, "D11 ฉีดก้ามเบรก", false), (item.Code, item.Name, item.Duty, item.IsAbsent));
        Assert.DoesNotContain(choices.Employees, e => e.Id == resigned.Id);
        Assert.Equal(choices.Employees.OrderBy(e => e.Code, StringComparer.Ordinal).Select(e => e.Code), choices.Employees.Select(e => e.Code));
        var shifts = await AsHrAsync(s => s.Get<OrgDirectory>().GetShiftsAsync());
        Assert.Equal(OvertimeSheetRegistry.WithExtras(shifts.Select(s => s.OvertimeIn).OfType<TimeOnly>(), OvertimeSheetRegistry.ExtraStartTimes), choices.StartTimes); // + the extras (#356)
        Assert.Equal(OvertimeSheetRegistry.WithExtras(shifts.Select(s => s.OvertimeOut).OfType<TimeOnly>(), OvertimeSheetRegistry.ExtraEndTimes), choices.EndTimes);
    }

    [Fact] // BR-012 #2 / BR-061: the people ไม่มาทำงาน that day are marked (GRED_2 = "NO" in the legacy), not hidden
    public async Task Employees_recorded_absent_that_day_are_marked()
    {
        var day = NewDay();
        var dayShift = await NewEmployeeAsync();
        var nightShift = await NewEmployeeAsync();
        var otherDay = await NewEmployeeAsync();
        async Task AbsentAsync(EmployeeDto e, DateOnly date, TimeOnly workIn)
        {
            var result = await AsHrAsync(s => s.Get<AbsenceRegistry>().SaveAsync(new AbsenceInput(null, date, e.Id, workIn, null, null)));
            Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        }
        await AbsentAsync(dayShift, day, new TimeOnly(8, 0));
        await AbsentAsync(nightShift, day.AddDays(-1), new TimeOnly(20, 10));
        await AbsentAsync(otherDay, day.AddDays(-1), new TimeOnly(8, 0));

        var choices = await AsHrAsync(s => s.Get<OvertimeSheetRegistry>().GetChoicesAsync(day));

        bool Absent(EmployeeDto e) => choices.Employees.Single(c => c.Id == e.Id).IsAbsent;
        Assert.Equal((true, true, false), (Absent(dayShift), Absent(nightShift), Absent(otherDay)));
    }
}

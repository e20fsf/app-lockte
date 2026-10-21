using Lockte.Hr;
using Lockte.Org;
using Lockte.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Web.Tests;

/// <summary>
/// งวดที่ย้ายมาจากระบบเดิม (#51): a closed period the migration created is never calculated again and keeps its dates —
/// its legacy time, leave and rates did not all move, so a new calculation would replace the right ค่าแรง with wrong
/// amounts — while its reports still print. The open period the migration found (#83) works as any other.
/// </summary>
[Collection(LocalDbCollection.Name)]
public sealed class MigratedPayPeriodTests(LocalDbFixture db)
{
    private static int lastCode;
    private static int lastYear = 450; // below PayrollTests' years (600+): PayPeriodTests' default-dates test needs the latest period

    /// <summary>"B" keeps clear of the other test classes' codes (T-EMPLO.ID is Text(4)).</summary>
    private static string NewCode() => $"B{Interlocked.Increment(ref lastCode):D3}";

    private static int NewYear() => Interlocked.Increment(ref lastYear);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private async Task<T> AsHrAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("hr1");
        return await action(scope);
    }

    private static void Ok(ServiceResult result) => Assert.True(result.Succeeded, string.Join(", ", result.Errors));

    private static void Refused(ServiceResult result) => Assert.Equal([PayPeriodRegistry.MigratedMessage], result.Errors);

    private async Task<EmployeeDto> NewEmployeeAsync()
    {
        var result = await AsHrAsync(s => s.Get<EmployeeRegistry>().SaveAsync(new EmployeeInput
        {
            Code = NewCode(), Name = "นาย ทดสอบ " + Guid.NewGuid().ToString("N")[..4], StartDate = D(400, 1, 1),
            EmployeeType = EmployeeTypes.Daily, DailyWage = 372,
        }));
        Ok(result);
        return (await AsHrAsync(s => s.Get<EmployeeDirectory>().GetAsync(result.Id!.Value)))!;
    }

    /// <summary>One งวด with its ค่าแรง, as #51 moves the legacy history in; returns the period.</summary>
    private async Task<PayPeriodDto> ImportAsync(DateOnly from, DateOnly to, EmployeeDto employee, decimal payNormal)
    {
        await AsHrAsync(async s =>
        {
            await ActivatorUtilities.CreateInstance<HrPayHistoryImport>(s.Get<IServiceProvider>())
                .AddAsync([new PayPeriodImport(from, to, to.AddDays(5), [new(employee.Id, 0, 10, 0, 0, 0, payNormal, 0, 0, 0, 0)])]);
            return 0;
        });
        return (await PeriodsAsync()).Single(p => p.From == from);
    }

    private Task<IReadOnlyList<PayPeriodDto>> PeriodsAsync() => AsHrAsync(s => s.Get<PayPeriodDirectory>().ListAsync());

    private Task<PayPeriodDto> GetAsync(int id) => AsHrAsync(async s => (await s.Get<PayPeriodDirectory>().GetAsync(id))!);

    private Task<IReadOnlyList<EmployeePay>> PayAsync(int id) => AsHrAsync(s => s.Get<PayPeriodDirectory>().GetPayAsync(id));

    private Task<ServiceResult> RegistryAsync(Func<PayPeriodRegistry, Task<ServiceResult>> action) => AsHrAsync(s => action(s.Get<PayPeriodRegistry>()));

    [Fact]
    public async Task A_migrated_period_is_closed_and_marked()
    {
        var year = NewYear();
        var period = await ImportAsync(D(year, 8, 12), D(year, 8, 26), await NewEmployeeAsync(), 4000m);

        Assert.True(period.Migrated);
        Assert.Equal(PayPeriodStep.SlipPrinted, period.CompletedStep);
        Assert.False(period.CanDo(PayPeriodStep.TimeCalculated));
        Assert.False(period.CanDo(PayPeriodStep.PayCalculated));
        Assert.True(period.CanDo(PayPeriodStep.SummaryPrinted));
        Assert.True(period.CanDo(PayPeriodStep.SlipPrinted));
    }

    [Fact] // the legacy ค่าแรง stays: calculating again would delete it (ForgetPay) and pay from incomplete time
    public async Task Calculating_the_time_of_a_migrated_period_is_refused_and_its_pay_stays()
    {
        var year = NewYear();
        var employee = await NewEmployeeAsync();
        var period = await ImportAsync(D(year, 8, 12), D(year, 8, 26), employee, 4000m);

        Refused(await RegistryAsync(r => r.CalculateTimeAsync(period.Id)));

        var pay = Assert.Single(await PayAsync(period.Id));
        Assert.Equal((employee.Id, 4000m), (pay.EmployeeId, pay.PayNormal));
        var after = await GetAsync(period.Id);
        Assert.Equal((PayPeriodStep.SlipPrinted, (DateTimeOffset?)null), (after.CompletedStep, after.TimeCalculatedAt));
    }

    [Fact]
    public async Task Calculating_the_pay_of_a_migrated_period_is_refused_and_its_pay_stays()
    {
        var year = NewYear();
        var employee = await NewEmployeeAsync();
        var period = await ImportAsync(D(year, 8, 27), D(year, 9, 11), employee, 5208m);

        Refused(await RegistryAsync(r => r.CalculatePayAsync(period.Id)));
        Refused(await RegistryAsync(r => r.CompleteStepAsync(period.Id, PayPeriodStep.PayCalculated)));

        Assert.Equal(5208m, Assert.Single(await PayAsync(period.Id)).PayNormal);
        Assert.Equal(PayPeriodStep.SlipPrinted, (await GetAsync(period.Id)).CompletedStep);
    }

    [Fact]
    public async Task The_dates_of_a_migrated_period_cannot_change()
    {
        var year = NewYear();
        var period = await ImportAsync(D(year, 9, 12), D(year, 9, 26), await NewEmployeeAsync(), 4650m);

        Refused(await RegistryAsync(r => r.SaveAsync(new PayPeriodInput(period.Id, period.From, period.To.AddDays(1), period.PayDate, period.RowVersion))));
        Refused(await RegistryAsync(r => r.SaveAsync(new PayPeriodInput(period.Id, period.From, period.To, period.PayDate.AddDays(1), period.RowVersion))));

        var after = await GetAsync(period.Id);
        Assert.Equal((period.From, period.To, period.PayDate), (after.From, after.To, after.PayDate));
        Assert.Single(await PayAsync(period.Id));
    }

    [Fact] // RPT-02, RPT-03 and the printing steps stay open; printing again keeps the period at its last step
    public async Task The_reports_of_a_migrated_period_still_print()
    {
        var year = NewYear();
        var employee = await NewEmployeeAsync();
        var period = await ImportAsync(D(year, 9, 27), D(year, 10, 11), employee, 4100m);

        var report = await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetPayReportAsync(period.Id, null));
        Assert.Equal(employee.Code, Assert.Single(report!.Lines).Pay.Code);
        var summary = await AsHrAsync(s => s.Get<PayPeriodDirectory>().GetPaySummaryAsync(period.Id));
        Assert.Equal(4100m, summary!.Sum.PayNormal);
        Ok(await RegistryAsync(r => r.CompleteStepAsync(period.Id, PayPeriodStep.SummaryPrinted)));
        Ok(await RegistryAsync(r => r.CompleteStepAsync(period.Id, PayPeriodStep.SlipPrinted)));

        Assert.Equal(PayPeriodStep.SlipPrinted, (await GetAsync(period.Id)).CompletedStep);
    }

    [Fact] // #83: the period already there (ตั้งวันที่ตัดWEEK) keeps its step, takes the results and calculates as usual
    public async Task The_open_period_the_migration_found_still_calculates()
    {
        var year = NewYear();
        var employee = await NewEmployeeAsync();
        var created = await RegistryAsync(r => r.SaveAsync(new PayPeriodInput(null, D(year, 10, 12), D(year, 10, 26), D(year, 11, 1), null)));
        Ok(created);
        var period = await ImportAsync(D(year, 10, 12), D(year, 10, 26), employee, 1000m);
        Assert.Equal((created.Id, false, PayPeriodStep.Set), (period.Id, period.Migrated, period.CompletedStep));

        Ok(await RegistryAsync(r => r.CalculateTimeAsync(period.Id)));
        Ok(await RegistryAsync(r => r.CalculatePayAsync(period.Id)));
        var after = await GetAsync(period.Id);
        Ok(await RegistryAsync(r => r.SaveAsync(new PayPeriodInput(period.Id, after.From, after.To, after.PayDate.AddDays(1), after.RowVersion))));

        Assert.Equal(PayPeriodStep.PayCalculated, (await GetAsync(period.Id)).CompletedStep);
    }
}

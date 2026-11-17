using Lockte.Hr.Data;
using Lockte.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Tests;

/// <summary>#354 (UAT-18): ค่าแรง/วัน is a whole number of baht, half up, rounded once where the model reads and writes it.</summary>
public sealed class WageRoundingTests
{
    [Theory]
    [InlineData(593.49, 593)]
    [InlineData(593.50, 594)]
    [InlineData(593.51, 594)]
    [InlineData(593.4, 593)]
    [InlineData(400, 400)]
    [InlineData(0, 0)]
    [InlineData(331.0000001, 331)]
    public void The_wage_rounds_half_up(double wage, double expected) => Assert.Equal(expected, Rounding.RndA(wage));

    [Theory] // the employee's wage and the wage copied onto a leave: both go through the rounding, in both directions
    [InlineData("Employee")]
    [InlineData("Leave")]
    public void The_model_rounds_the_wage_it_reads_and_writes(string entity)
    {
        using var db = new HrDbContext(new DbContextOptionsBuilder<HrDbContext>().UseSqlServer("Server=unused;Database=unused").Options);
        var converter = db.Model.GetEntityTypes().Single(e => e.ClrType.Name == entity).FindProperty("DailyWage")!.GetValueConverter();

        Assert.NotNull(converter);
        Assert.Equal(594d, converter!.ConvertToProvider(593.51));
        Assert.Equal(593d, converter.ConvertFromProvider(593.4));
        Assert.Equal(594d, converter.ConvertFromProvider(593.5));
    }
}

/// <summary>#392 (UAT-21): เงินเดือน is a whole number of baht, half up, rounded in the model like ค่าแรง/วัน.</summary>
public sealed class SalaryRoundingTests
{
    [Theory] // the edges .49 / .50 / .51
    [InlineData(17020.49, 17020)]
    [InlineData(17020.50, 17021)]
    [InlineData(17020.51, 17021)]
    [InlineData(22564.32, 22564)]
    [InlineData(0, 0)]
    public void The_model_rounds_the_salary_it_reads_and_writes(double salary, double expected)
    {
        using var db = new HrDbContext(new DbContextOptionsBuilder<HrDbContext>().UseSqlServer("Server=unused;Database=unused").Options);
        var converter = db.Model.GetEntityTypes().Single(e => e.ClrType.Name == "Employee").FindProperty("Salary")!.GetValueConverter();

        Assert.NotNull(converter);
        Assert.Equal(expected, converter!.ConvertToProvider(salary));
        Assert.Equal(expected, converter.ConvertFromProvider(salary));
    }
}

/// <summary>#392: the one-off rounding of stored เงินเดือน — dry run counts, apply rounds, ค่าแรง/วัน is left alone. A fresh test database, never Lockte.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class LegacyPayRoundingTests(LocalDbFixture db)
{
    private static int lastCode = 700;

    [Fact]
    public async Task The_dry_run_counts_and_the_apply_rounds_only_the_salary()
    {
        var code = $"R{Interlocked.Increment(ref lastCode)}";
        int id;
        await using (var scope = db.Scope("hr1"))
        {
            var saved = await scope.Get<Lockte.Hr.EmployeeRegistry>().SaveAsync(new Lockte.Hr.EmployeeInput { Code = code, Name = "ทดสอบปัดเงินเดือน" });
            Assert.True(saved.Succeeded);
            id = saved.Id!.Value;
        }
        await using (var scope = db.Scope("hr1"))
        {
            // the way old rows are: satang stored, the model not involved
            await scope.Get<HrDbContext>().Database.ExecuteSqlRawAsync("UPDATE hr.Employees SET Salary = 17020.49, DailyWage = 593.51 WHERE Id = {0}", id);
        }

        async Task<(double Salary, double Wage)> StoredAsync()
        {
            await using var scope = db.Scope("hr1");
            var rows = await scope.Get<HrDbContext>().Database.SqlQueryRaw<double>("SELECT Salary AS [Value] FROM hr.Employees WHERE Id = {0}", id).ToListAsync();
            var wages = await scope.Get<HrDbContext>().Database.SqlQueryRaw<double>("SELECT DailyWage AS [Value] FROM hr.Employees WHERE Id = {0}", id).ToListAsync();
            return (rows.Single(), wages.Single());
        }

        await using (var scope = db.Scope("hr1"))
        {
            var dry = await scope.Get<Lockte.Hr.LegacyPayRounding>().DryRunAsync();
            Assert.True(dry.Single().Rows >= 1);
        }
        Assert.Equal((17020.49, 593.51), await StoredAsync()); // a dry run changes nothing

        await using (var scope = db.Scope("hr1"))
            Assert.True((await scope.Get<Lockte.Hr.LegacyPayRounding>().ApplyAsync()).Single().Rows >= 1);
        Assert.Equal((17020d, 593.51), await StoredAsync());

        await using (var scope = db.Scope("hr1"))
            Assert.Equal(0, (await scope.Get<Lockte.Hr.LegacyPayRounding>().DryRunAsync()).Single().Rows); // nothing left
    }
}

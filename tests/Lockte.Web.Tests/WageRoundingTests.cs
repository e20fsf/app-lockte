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

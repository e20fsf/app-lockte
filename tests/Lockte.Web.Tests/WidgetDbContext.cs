using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Tests;

/// <summary>
/// A stand-in module (schema <c>test</c>) for checking the audit convention on an ordinary table, before the real
/// modules have tables. Created by <see cref="CreateTableAsync"/>, not by migrations.
/// </summary>
public sealed class WidgetDbContext(DbContextOptions<WidgetDbContext> options) : ModuleDbContext(options, Schema)
{
    public const string Schema = "test";
    public const string AuditName = "ชิ้นทดสอบ";

    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Widget>(w =>
        {
            w.HasAuditName(AuditName);
            w.Property(x => x.Code).HasMaxLength(20).HasAuditName("รหัส");
            w.Property(x => x.Name).HasMaxLength(100).HasAuditName("ชื่อ");
        });

    public Task CreateTableAsync() => Database.ExecuteSqlRawAsync(
        """
        IF SCHEMA_ID('test') IS NULL EXEC('CREATE SCHEMA test');
        CREATE TABLE test.Widgets (
            Id int IDENTITY PRIMARY KEY,
            Code nvarchar(20) NOT NULL,
            Name nvarchar(100) NOT NULL,
            Price decimal(18,2) NOT NULL,
            CreatedBy nvarchar(256) NOT NULL,
            CreatedAt datetimeoffset NOT NULL,
            UpdatedBy nvarchar(256) NOT NULL,
            UpdatedAt datetimeoffset NOT NULL,
            RowVersion rowversion NOT NULL);
        """);
}

public sealed class Widget : IAudited
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }

    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    string IHasBusinessKey.BusinessKey => Code;
}

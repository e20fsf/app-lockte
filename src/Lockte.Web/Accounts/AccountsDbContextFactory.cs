using Lockte.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lockte.Web.Accounts;

/// <summary>For <c>dotnet ef migrations add</c> only (no database connection is made).</summary>
internal sealed class AccountsDbContextFactory : IDesignTimeDbContextFactory<AccountsDbContext>
{
    public AccountsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AccountsDbContext>();
        options.UseLockteSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=Lockte;Integrated Security=true",
            AccountsDbContext.Schema, new AuditStampInterceptor(new SystemUser(), TimeProvider.System));
        return new AccountsDbContext(options.Options);
    }
}

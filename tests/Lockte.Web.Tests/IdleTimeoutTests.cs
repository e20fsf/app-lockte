using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lockte.Web.Tests;

/// <summary>No database needed: the login cookie lifetime is the configured idle time.</summary>
public sealed class IdleTimeoutTests
{
    private static CookieAuthenticationOptions LoginCookie(params (string Key, string Value)[] settings)
    {
        var values = settings.ToDictionary(s => s.Key, s => (string?)s.Value);
        values[$"ConnectionStrings:{AccountsRegistration.ConnectionStringName}"] = "Server=unused";
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ICurrentUser, SystemUser>()
            .AddLockteAccounts(new ConfigurationBuilder().AddInMemoryCollection(values).Build())
            .BuildServiceProvider();
        return services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
    }

    [Fact]
    public void Idle_for_120_minutes_by_default_means_login_again()
    {
        var cookie = LoginCookie();

        Assert.Equal(TimeSpan.FromMinutes(120), cookie.ExpireTimeSpan);
        Assert.True(cookie.SlidingExpiration);
        Assert.Null(cookie.Cookie.MaxAge); // not kept after the browser closes
    }

    [Fact]
    public void Idle_time_comes_from_config()
    {
        Assert.Equal(TimeSpan.FromMinutes(10), LoginCookie(("Auth:IdleTimeoutMinutes", "10")).ExpireTimeSpan);
    }
}

using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lockte.Web.Accounts;

/// <summary>Config section <c>Auth</c>.</summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>ไม่ใช้งานเกินนี้ → ต้อง login ใหม่ (#39: เดิม 30 นาที; 28 ต.ค. 2569 ผู้ใช้สั่งเป็น 120 นาที, ตั้งทับได้ที่ Auth:IdleTimeoutMinutes ในไฟล์ตั้งค่าบน server).</summary>
    public int IdleTimeoutMinutes { get; set; } = 120;
}

public static class AccountsRegistration
{
    public const string ConnectionStringName = "Lockte";
    public const string HomeEndpoint = "/Account/Home";

    public static string ConnectionString(IConfiguration config)
        => config.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is not set.");

    /// <summary>
    /// Identity with the #39 rules. Used by the web host and by the tests, so both run the same policy.
    /// The host registers <see cref="ICurrentUser"/>.
    /// </summary>
    public static IServiceCollection AddLockteAccounts(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = ConnectionString(config);
        var auth = config.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.Section));

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddModuleDbContext<AuditDbContext>(connectionString, AuditDbContext.Schema);
        services.AddScoped<HistoryService>();
        services.AddModuleDbContext<AccountsDbContext>(connectionString, AccountsDbContext.Schema);

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = false;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";

                // "Confirmed" = active; see ActiveUserConfirmation.
                options.SignIn.RequireConfirmedAccount = true;
            })
            .AddEntityFrameworkStores<AccountsDbContext>()
            .AddSignInManager()
            .AddErrorDescriber<ThaiIdentityErrorDescriber>()
            .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();
        services.AddScoped<IUserConfirmation<AppUser>, ActiveUserConfirmation>();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "Lockte.Auth";
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = HomeEndpoint; // a page this user may not open → their own start page
            // A report/file the user may not have (#277: ข้อมูลค่าจ้าง) is a plain 403, not a redirect to a page.
            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/reports")) context.Response.StatusCode = StatusCodes.Status403Forbidden;
                else context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            // Session cookie; the ticket inside expires after the idle time. The idle script (idle.js) renews it
            // while the user is active and returns to login when not.
            options.ExpireTimeSpan = TimeSpan.FromMinutes(auth.IdleTimeoutMinutes);
            options.SlidingExpiration = true;
        });
        // Deactivated user / temporary password set: the old cookie stops working within a minute.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

        services.AddScoped<AccountService>();
        services.AddScoped<UserAdminService>();
        services.AddScoped<LastProgramService>();
        return services;
    }
}

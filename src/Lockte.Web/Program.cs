using Lockte.Hr;
using Lockte.Org;
using Lockte.Sales;
using Lockte.SharedKernel.Persistence;
using Lockte.Web;
using Lockte.Web.Accounts;
using Lockte.Web.Components;
using Lockte.Web.Reports;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = AccountsRegistration.ConnectionString(builder.Configuration);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
builder.Services.AddScoped<CircuitHandler, CurrentUserCircuitHandler>();
builder.Services.AddScoped<CircuitActivity>();
builder.Services.AddScoped<ScopedServices>();
builder.Services.AddScoped<CircuitHandler, CircuitActivityHandler>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingAuthStateProvider>();
builder.Services.AddLockteAccounts(builder.Configuration);
builder.Services.AddAuthorization(AuthPolicies.Configure);

builder.Services.AddOrgModule(connectionString);
builder.Services.AddHrModule(connectionString);
builder.Services.AddSalesModule(connectionString);
builder.Services.AddReportData(connectionString);
builder.Services.AddReportPrinting(builder.Configuration);

var app = builder.Build();

if (args is ["reset-admin", .. var rest])
    return await ResetAdminCommand.RunAsync(app.Services, rest);

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    await LockteDatabase.MigrateAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapReports();

app.MapPost("/Account/Logout", async (SignInManager<AppUser> signIn) =>
{
    await signIn.SignOutAsync();
    return TypedResults.LocalRedirect("~/Account/Login");
});

// Where a signed-in user starts; also the cookie's access-denied page, so a forbidden full page load
// (admin opening "/", a user who must change the password) lands on the right page instead of looping.
app.MapGet(AccountsRegistration.HomeEndpoint, (HttpContext context)
    => TypedResults.LocalRedirect("~/" + AccountClaims.HomeHref(context.User)))
    .RequireAuthorization(AuthPolicies.SignedIn);

// idle.js pings this while the user is active: a fresh cookie with the full idle window each time.
app.MapPost("/Account/KeepAlive", async (HttpContext context, SignInManager<AppUser> signIn) =>
{
    var user = await signIn.UserManager.GetUserAsync(context.User);
    if (user is null || !user.IsActive) return Results.Unauthorized();
    await signIn.RefreshSignInAsync(user);
    return Results.NoContent();
}).RequireAuthorization(AuthPolicies.SignedIn);

await app.RunAsync();
return 0;

using Lockte.Web.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lockte.Web.Tests;

/// <summary>The deployed package version shown after login (#453).</summary>
public sealed class AppVersionTests
{
    [Theory]
    [InlineData("20261114-420537a", "20261114-420537a")]
    [InlineData("20261114-420537a+420537af0c1d", "20261114-420537a")]
    [InlineData("20261114-420537a-dirty-app-only", "20261114-420537a")]
    [InlineData("20261114-420537a-app-only", "20261114-420537a")]
    [InlineData("20261114-420537a-dirty", "20261114-420537a")]
    [InlineData("1.0.0", "dev")]
    [InlineData("1.0.0+420537af0c1d", "dev")]
    [InlineData("", "dev")]
    [InlineData(null, "dev")]
    public void Text_drops_the_plus_hash_and_falls_back_to_dev(string? raw, string expected) =>
        Assert.Equal(expected, new AppVersion(raw).Text);

    [Fact]
    public void Test_assembly_without_Build_Package_shows_dev() =>
        Assert.Equal("dev", AppVersion.FromAssembly(typeof(AppVersion).Assembly).Text);

    [Fact]
    public async Task Label_shows_the_version() =>
        Assert.Contains("Version 20261114-420537a · Server time 2026-11-15 12:03", await Render());

    [Fact]
    public void Login_layout_has_the_label_and_the_main_layout_does_not()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(repo.FullName, "Lockte.slnx"))) repo = repo.Parent!;
        string Layout(string name) => File.ReadAllText(Path.Combine(repo.FullName, "src", "Lockte.Web", "Components", "Layout", name));

        Assert.Contains("<AppVersionLabel", Layout("AccountLayout.razor"));
        Assert.DoesNotContain("AppVersion", Layout("MainLayout.razor"));
    }

    private static async Task<string> Render()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(new FixedClock(new DateTimeOffset(2026, 11, 15, 12, 3, 41, TimeSpan.FromHours(7))));
        services.AddSingleton(new AppVersion("20261114-420537a+abcdef"));
        await using var sp = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(sp, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<AppVersionLabel>(ParameterView.Empty)).ToHtmlString());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("t", now.Offset, "t", "t");
    }
}

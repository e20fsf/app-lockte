using Lockte.Web.Menus;

namespace Lockte.Web.Tests;

/// <summary>Each program has its own icon, the same on the chooser cards and in the app bar's program menu.</summary>
public sealed class ProgramIconTests
{
    [Fact]
    public void Every_program_has_an_icon_of_its_own()
    {
        var icons = ProgramMenus.All.Select(p => p.Icon).ToList();

        Assert.All(icons, i => Assert.False(string.IsNullOrWhiteSpace(i)));
        Assert.Equal(icons.Count, icons.Distinct().Count());
    }
}

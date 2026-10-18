namespace Lockte.Web.Tests;

/// <summary>docs/brand/logo.svg is the source; the web serves a copy from wwwroot (see docs/brand/README.md).</summary>
public sealed class BrandLogoTests
{
    [Fact]
    public void Web_logo_is_the_same_file_as_docs_brand()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(repo.FullName, "Lockte.slnx"))) repo = repo.Parent!;

        Assert.Equal(
            File.ReadAllBytes(Path.Combine(repo.FullName, "docs", "brand", "logo.svg")),
            File.ReadAllBytes(Path.Combine(repo.FullName, "src", "Lockte.Web", "wwwroot", "brand", "logo.svg")));
    }
}

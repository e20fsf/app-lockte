using System.Reflection;

namespace Lockte.Web;

/// <summary>
/// The version of the deployed package (#453). deploy/Build-Package.ps1 passes the same string as version.txt
/// (yyyyMMdd-commit[-dirty][-app-only]) as the assembly's InformationalVersion; a build that did not come from it
/// (dev PC, tests) has only the SDK default (1.0.0, maybe +commit) and shows "dev".
/// </summary>
public sealed class AppVersion(string? informationalVersion)
{
    public const string Dev = "dev";

    /// <summary>What the page shows: the package version, or "dev".</summary>
    public string Text { get; } = Normalize(informationalVersion);

    public static AppVersion FromAssembly(Assembly assembly) =>
        new(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public static string Normalize(string? informationalVersion)
    {
        var v = informationalVersion ?? "";
        var plus = v.IndexOf('+');
        if (plus >= 0) v = v[..plus]; // the SDK's +<source revision> suffix
        v = v.Trim();
        return v.Length == 0 || v == "1.0.0" ? Dev : v;
    }
}

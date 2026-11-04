using System.Text;
using Microsoft.Extensions.Options;

namespace Lockte.Web.Reports;

/// <summary>The programs whose printed documents can each have their own font (<see cref="ReportOptions.ProgramFonts"/>).</summary>
public enum ReportProgram { Hr, Order, Prod, Purch, Calib, Cash }

/// <summary>
/// A font a document is printed in: the CSS family name and the <c>@font-face</c> rules that define it (#395).
/// Every print builds its CSS from one of these, so a font change is made here and nowhere in the layouts.
/// </summary>
public sealed record ReportFont(string Key, string Family, string FaceCss);

/// <summary>
/// The fonts reports can use. <see cref="ThSarabunNew"/> is the app's own (SIL OFL, files in <c>Reports/Fonts</c>);
/// <see cref="AngsanaNew"/> is Microsoft's — licensed, so never in the repo (public): it is the one installed on the server.
/// </summary>
public static class ReportFonts
{
    public const string ThSarabunNewKey = "THSarabunNew";
    public const string AngsanaNewKey = "AngsanaNew";

    /// <summary>The default for every program: the OFL font shipped with the app.</summary>
    public static readonly ReportFont ThSarabunNew = new(ThSarabunNewKey, "THSarabunNew", BuildSarabun());

    /// <summary>
    /// Angsana New from the server's installed fonts (<c>local()</c>). Its ascent/descent differ from TH Sarabun New's, so the face's metrics are overridden to stand where TH Sarabun New
    /// stood: every layout (fixed positions measured on the legacy) keeps its text on the same baseline
    /// (<see cref="FormHtml.BaselineEm"/>) and inside its box; <see cref="AngsanaSizeAdjust"/> scales the glyphs.
    /// </summary>
    public static readonly ReportFont AngsanaNew = new(AngsanaNewKey, "LockteAngsanaNew", BuildAngsana());

    /// <summary>
    /// Glyph scale of Angsana New. Measured in Chromium at the same size: Thai text is 1% wider than in TH Sarabun New,
    /// digits and Latin 10% narrower — so 100% keeps every fixed layout (measured on the legacy, which printed Angsana New at
    /// these very sizes) with no text running over; enlarging it would overflow the boxes. Change here if the look needs it.
    /// </summary>
    public const string AngsanaSizeAdjust = "100%";

    /// <summary>Looks a font up by its configured key; null when there is no such font.</summary>
    public static ReportFont? ByKey(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "thsarabunnew" or "sarabun" or "thsarabun" => ThSarabunNew,
        "angsananew" or "angsana" => AngsanaNew,
        _ => null,
    };

    private static string BuildSarabun()
    {
        var css = new StringBuilder();
        foreach (var (file, weight) in ReportHtml.Fonts)
            css.Append($"@font-face{{font-family:'THSarabunNew';font-weight:{weight};src:url('{ReportHtml.FontHost}{Uri.EscapeDataString(file)}') format('truetype')}}");
        return css.ToString();
    }

    private static string BuildAngsana()
    {
        // local() takes the full name (Windows) or the PostScript name; the Windows names of the .ttc/.ttf faces are listed.
        const string adjust = "size-adjust:" + AngsanaSizeAdjust + ";ascent-override:66.5%;descent-override:33.5%;line-gap-override:0%";
        return "@font-face{font-family:'LockteAngsanaNew';font-weight:normal;src:local('Angsana New'),local('AngsanaNew'),local('Angsana New Regular');" + adjust + "}"
               + "@font-face{font-family:'LockteAngsanaNew';font-weight:bold;src:local('Angsana New Bold'),local('AngsanaNew-Bold'),local('AngsanaNewBold');" + adjust + "}";
    }
}

/// <summary>
/// Which font each program prints in (<see cref="ReportOptions.ProgramFonts"/>), and the fall-back to TH Sarabun New with
/// a log line when the configured font is not installed on this machine (#395).
/// </summary>
public sealed class ReportFontResolver(IOptions<ReportOptions> options, ILogger<ReportFontResolver> logger)
{
    private readonly Dictionary<string, bool> lastInstalled = [];

    /// <summary>Whether the machine has a font by this family name; replaced in tests.</summary>
    public Func<string, bool> IsInstalled { get; init; } = InstalledFonts.Has;

    /// <summary>The key of the setting for programs without their own.</summary>
    public const string DefaultKey = "Default";

    public ReportFont For(ReportProgram? program)
    {
        var map = options.Value.ProgramFonts;
        if (!(program is { } p && map.TryGetValue(p.ToString(), out var key) && !string.IsNullOrWhiteSpace(key))
            && !map.TryGetValue(DefaultKey, out key))
            return ReportFonts.ThSarabunNew;
        var which = program?.ToString() ?? DefaultKey;
        var font = ReportFonts.ByKey(key);
        if (font is null)
        {
            logger.LogWarning("Reports:ProgramFonts:{Program} = '{Key}' is not a known font (THSarabunNew, AngsanaNew); printing in THSarabunNew", which, key);
            return ReportFonts.ThSarabunNew;
        }
        if (font.Key != ReportFonts.AngsanaNewKey) return font;

        var installed = IsInstalled("Angsana New");
        lock (lastInstalled)
        {
            if (!lastInstalled.TryGetValue(which, out var before) || before != installed)
            {
                lastInstalled[which] = installed;
                if (installed) logger.LogInformation("Font Angsana New found; {Program} documents print in it", which);
                else logger.LogWarning("Font Angsana New is NOT installed on this server; {Program} documents print in THSarabunNew instead. Install Angsana New (Windows Fonts) to print in it", which);
            }
        }
        return installed ? font : ReportFonts.ThSarabunNew;
    }
}

/// <summary>The fonts installed on this Windows machine (HKLM/HKCU Fonts registry names, as the Settings app lists them).</summary>
public static class InstalledFonts
{
    public static bool Has(string family)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            foreach (var hive in new[] { Microsoft.Win32.Registry.LocalMachine, Microsoft.Win32.Registry.CurrentUser })
            {
                using var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
                if (key?.GetValueNames().Any(n => n.Contains(family, StringComparison.OrdinalIgnoreCase)) == true) return true;
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { /* treat as not installed */ }
        return false;
    }
}

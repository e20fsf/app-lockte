using MudBlazor;

namespace Lockte.Web.Components.Layout;

/// <summary>
/// The one look of the whole app (light mode). Red from the company logo is only used where white text sits on it
/// (#C62828: 5.6:1 on white — the logo's own #FF0000 is 4:1 and fails WCAG AA for text); everything else is slate grey.
/// Text colours are all at least 4.5:1 on their background, input borders at least 3:1.
/// </summary>
public static class LockteTheme
{
    public static readonly string[] FontFamily = ["Sarabun", "Leelawadee UI", "Tahoma", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#C62828",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#455A64",
            SecondaryContrastText = "#FFFFFF",
            Info = "#01579B",
            Success = "#2E7D32",
            Warning = "#8A5100",
            Error = "#B71C1C",
            Background = "#F6F7F9",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#1F2933",
            DrawerBackground = "#FFFFFF",
            TextPrimary = "#1F2933",
            TextSecondary = "#52606D",
            TextDisabled = "#8B95A1",
            ActionDefault = "#52606D",
            ActionDisabled = "#B0B8C1",
            ActionDisabledBackground = "#E4E7EB",
            Divider = "#E4E7EB",
            DividerLight = "#EEF0F3",
            TableLines = "#E4E7EB",
            TableStriped = "#F8F9FA",
            TableHover = "#FDF2F2",
            LinesDefault = "#E4E7EB",
            LinesInputs = "#7B8794",
            OverlayDark = "rgba(31,41,51,0.5)",
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px", AppbarHeight = "64px" },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = FontFamily, FontSize = "1rem", LineHeight = "1.6" },
            H1 = new H1Typography { FontFamily = FontFamily, FontSize = "1.75rem", FontWeight = "700", LineHeight = "1.35", LetterSpacing = "0" },
            H2 = new H2Typography { FontFamily = FontFamily, FontSize = "1.5rem", FontWeight = "700", LineHeight = "1.35", LetterSpacing = "0" },
            H3 = new H3Typography { FontFamily = FontFamily, FontSize = "1.3rem", FontWeight = "700", LineHeight = "1.4", LetterSpacing = "0" },
            H4 = new H4Typography { FontFamily = FontFamily, FontSize = "1.2rem", FontWeight = "700", LineHeight = "1.4", LetterSpacing = "0" },
            H5 = new H5Typography { FontFamily = FontFamily, FontSize = "1.1rem", FontWeight = "700", LineHeight = "1.45", LetterSpacing = "0" },
            H6 = new H6Typography { FontFamily = FontFamily, FontSize = "1.05rem", FontWeight = "700", LineHeight = "1.5", LetterSpacing = "0" },
            Subtitle1 = new Subtitle1Typography { FontFamily = FontFamily, FontSize = "1.05rem", FontWeight = "500", LineHeight = "1.55", LetterSpacing = "0" },
            Subtitle2 = new Subtitle2Typography { FontFamily = FontFamily, FontSize = "1rem", FontWeight = "500", LineHeight = "1.55", LetterSpacing = "0" },
            Body1 = new Body1Typography { FontFamily = FontFamily, FontSize = "1.0625rem", FontWeight = "400", LineHeight = "1.6", LetterSpacing = "0" },
            Body2 = new Body2Typography { FontFamily = FontFamily, FontSize = "1rem", FontWeight = "400", LineHeight = "1.6", LetterSpacing = "0" },
            Button = new ButtonTypography { FontFamily = FontFamily, FontSize = "1rem", FontWeight = "500", LineHeight = "1.5", LetterSpacing = "0", TextTransform = "none" },
            Caption = new CaptionTypography { FontFamily = FontFamily, FontSize = ".9rem", FontWeight = "400", LineHeight = "1.5", LetterSpacing = "0" },
            Overline = new OverlineTypography { FontFamily = FontFamily, FontSize = ".85rem", FontWeight = "500", LineHeight = "1.5", LetterSpacing = "0", TextTransform = "none" },
        },
    };
}

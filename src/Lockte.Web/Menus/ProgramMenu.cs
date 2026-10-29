namespace Lockte.Web.Menus;

/// <summary>
/// An item on a menu page: opens another menu page, or a screen/report by its SCR/RPT code — its own
/// <see cref="Page"/> once built, the placeholder until then. <see cref="NewTab"/>: a report printed straight from the
/// menu (a PDF in a new tab, as Access opened its preview). <see cref="Code"/> is unique within its program (a suffix
/// tells apart items that share one SCR/RPT), because the placeholder is found by it.
/// </summary>
public sealed record MenuItem(string Text, int? SubMenu = null, string? Code = null, string? Page = null, bool NewTab = false);

/// <summary>A menu page; <see cref="Back"/> is where its "กลับเมนูเดิม" goes (null on the main menu).</summary>
public sealed record MenuPage(int Number, string Title, int? Back, IReadOnlyList<MenuItem> Items);

/// <summary>
/// The legacy Switchboard of one Access program, as menu pages that open each other (<see cref="EmployeeMenu"/>,
/// <see cref="OrderMenu"/>). Paths are relative to the site root, no leading slash: the main menu is at
/// <see cref="Root"/>, menu page n at <c>{MenuRoot}/n</c>, an item not built yet at <c>{PlaceholderRoot}/{Code}</c>.
/// </summary>
public sealed class ProgramMenu(string key, string title, string root, string menuRoot, string placeholderRoot, IReadOnlyList<MenuPage> pages)
{
    /// <summary>One step of the "you are here" trail; <see cref="Href"/> is null on the current page.</summary>
    public sealed record Crumb(string Text, string? Href);

    public const int Main = 1;

    /// <summary>Stored as the user's last program (<see cref="ProgramMenus"/>).</summary>
    public string Key { get; } = key;

    /// <summary>The program's name on the program chooser and the app bar.</summary>
    public string Title { get; } = title;

    public string Root { get; } = root;
    public string PlaceholderRoot { get; } = placeholderRoot;
    public IReadOnlyList<MenuPage> Pages { get; } = pages;

    public string HrefOf(int number) => number == Main ? Root : $"{menuRoot}/{number}";

    public string HrefOf(MenuItem item) => item.SubMenu is { } n ? HrefOf(n) : item.Page ?? $"{PlaceholderRoot}/{item.Code}";

    public MenuPage? Find(int number) => Pages.FirstOrDefault(p => p.Number == number);

    /// <summary>The menu page holding the item with this code, and the item.</summary>
    public (MenuPage Page, MenuItem Item)? FindItem(string code)
        => Pages.SelectMany(p => p.Items.Select(i => (Page: p, Item: i)))
            .Where(x => string.Equals(x.Item.Code, code, StringComparison.OrdinalIgnoreCase))
            .Select(x => ((MenuPage, MenuItem)?)(x.Page, x.Item))
            .FirstOrDefault();

    /// <summary>
    /// "You are here" for a page path: เมนูหลัก (เลือกโปรแกรม) → the program → … → the menu item, following each menu's
    /// "กลับเมนูเดิม" up to the program's main menu. A path below a menu item's page (an employee's own screen) belongs to that item. A path that is on none of
    /// this program's menus (ประวัติการแก้ไข, the other program) gets no trail.
    /// </summary>
    public IReadOnlyList<Crumb> Trail(string path)
    {
        path = Clean(path);
        MenuPage? menu = null;
        MenuItem? item = null;

        if (path.Equals(Root, StringComparison.OrdinalIgnoreCase)) menu = Find(Main);
        else if (path.StartsWith(menuRoot + "/", StringComparison.OrdinalIgnoreCase) && int.TryParse(path[(menuRoot.Length + 1)..], out var n)) menu = Find(n);
        else
        {
            var hit = Pages.SelectMany(p => p.Items.Where(i => i.SubMenu is null).Select(i => (Page: p, Item: i, Href: HrefOf(i))))
                .Where(x => path.Equals(x.Href, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(x.Href + "/", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Href.Length)
                .Select(x => ((MenuPage, MenuItem)?)(x.Page, x.Item))
                .FirstOrDefault();
            if (hit is null) return [];
            (menu, item) = hit.Value;
        }
        if (menu is null) return [];

        var trail = new List<Crumb>();
        if (item is not null) trail.Add(new Crumb(item.Text, null));
        for (var m = menu; m is not null; m = m.Back is { } b ? Find(b) : null)
            trail.Insert(0, new Crumb(m.Number == Main ? Title : m.Title, item is null && m == menu ? null : HrefOf(m.Number)));
        // UAT-5 (#281): เมนูหลัก is the program chooser; the program's own main menu is named after the program
        trail.Insert(0, new Crumb("เมนูหลัก", ProgramMenus.ChooserHref));
        return trail;
    }

    /// <summary>Whether the page at this path is one of this program's menus or screens.</summary>
    public bool Owns(string path)
        => Clean(path).Equals(PlaceholderRoot, StringComparison.OrdinalIgnoreCase)
            || Clean(path).StartsWith(PlaceholderRoot + "/", StringComparison.OrdinalIgnoreCase)
            || Trail(path).Count > 0;

    private static string Clean(string path) => path.Split('?', '#')[0].Trim('/');
}

/// <summary>
/// The programs a staff user chooses between after login (หน้าเลือกโปรแกรม), all in one app (ADR 0002). วัตถุดิบ is the third and สอบเทียบ the fourth;
/// เงินสดย่อย comes in a later phase.
/// </summary>
public static class ProgramMenus
{
    /// <summary>The program chooser; "/" sends the user to their last program, or here when there is none.</summary>
    public const string ChooserHref = "programs";

    public static IReadOnlyList<ProgramMenu> All { get; } = [EmployeeMenu.Menu, OrderMenu.Menu, PurchMenu.Menu, CalibMenu.Menu];

    public static ProgramMenu? Find(string? key) => All.FirstOrDefault(p => p.Key == key);

    /// <summary>The program whose menus or screens this path is on; null for pages of no program (ประวัติการแก้ไข).</summary>
    public static ProgramMenu? Of(string path) => All.FirstOrDefault(p => p.Owns(path));
}

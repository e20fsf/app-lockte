using Lockte.Org;
using Lockte.Prod;
using Lockte.SharedKernel;

namespace Lockte.Web.Reports.Order;

/// <summary>One stage of a Lot trace as the report prints it: the product, the quantity (the good quantity of the first stage, the total of the others) and the Lots.</summary>
public sealed record LotCell(DateOnly? Date, string? ProductCode, string ProductName, decimal Quantity, string? Lot, string? MaterialLot);

public sealed record LotTraceRow(IReadOnlyList<LotCell?> Cells);

/// <summary>RPT-17 เปรียบเทียบ Lot การผลิต: a row for each production line of the first stage, followed by the lines of the next stages that received its Lot (blank where none did).</summary>
public sealed record LotTraceReport(string Title, DateOnly From, DateOnly To, IReadOnlyList<string> Stages, IReadOnlyList<LotTraceRow> Rows);

/// <summary>
/// RPT-17 เปรียบเทียบ Lot การผลิต (#127), ระดับ B on A4 นอน: the Lot a department made is matched with the Lot of raw material the next department wrote down — the lines of the
/// chains ไดคาสท์ → ตกแต่ง → ประกอบ, อัดผ้าเบรก → ฝนผ้าเบรก → ประกอบ and อัดดิสเบรก → ดิสเบรก — so a defect found late can be followed back. The chains and the way each stage is
/// picked (machine code prefix, product code) are those of the legacy queries <c>เปรียบเทียบLOTการผลิต*</c>; the range of dates is that of the first stage (the others are of any date).
/// </summary>
public static class LotTraceReports
{
    private enum Stage { Die, Trim, Assembly, Grind, Press, PressG5, DiscPress, Disc }

    private enum Link { ReceivedLot1, ReceivedLot2, MadeLotOfLot1, MadeLotOfLot2 }

    private sealed record Step(Stage Stage, Link Link, bool Inner = false, bool InRange = false);

    private sealed record Chain(string Title, string[] StageNames, Stage First, Step[] Next, bool Distinct = false);

    public static readonly IReadOnlyDictionary<string, string> Titles = new Dictionary<string, string>
    {
        ["die-trim"] = "เปรียบเทียบ Lot การผลิต ไดคาสท์ → ตกแต่ง",
        ["die-trim-assembly"] = "เปรียบเทียบ Lot การผลิต ไดคาสท์ → ตกแต่ง → ประกอบ",
        ["press-grind"] = "เปรียบเทียบ Lot การผลิต อัดผ้าเบรก → ฝนผ้าเบรก",
        ["press-grind-assembly"] = "เปรียบเทียบ Lot การผลิต อัดผ้าเบรก → ฝนผ้าเบรก → ประกอบ",
        ["disc"] = "เปรียบเทียบ Lot การผลิต อัดดิสเบรก → ดิสเบรก",
        ["from-die"] = "เปรียบเทียบ Lot การผลิต ตลอดสาย (เริ่มที่ไดคาสท์)",
        ["from-press"] = "เปรียบเทียบ Lot การผลิต ตลอดสาย (เริ่มที่อัดผ้าเบรก)",
    };

    private static readonly Dictionary<string, Chain> Chains = new()
    {
        ["die-trim"] = new(Titles["die-trim"], ["ไดคาสท์", "ตกแต่ง"], Stage.Die, [new(Stage.Trim, Link.ReceivedLot1)]),
        ["die-trim-assembly"] = new(Titles["die-trim-assembly"], ["ไดคาสท์", "ตกแต่ง", "ประกอบ"], Stage.Die,
            [new(Stage.Trim, Link.ReceivedLot1), new(Stage.Assembly, Link.ReceivedLot1)], Distinct: true),
        ["press-grind"] = new(Titles["press-grind"], ["อัดผ้าเบรก", "ฝนผ้าเบรก"], Stage.PressG5, [new(Stage.Grind, Link.ReceivedLot1, Inner: true, InRange: true)]),
        ["press-grind-assembly"] = new(Titles["press-grind-assembly"], ["อัดผ้าเบรก", "ฝนผ้าเบรก", "ประกอบ"], Stage.PressG5,
            [new(Stage.Grind, Link.ReceivedLot1, Inner: true, InRange: true), new(Stage.Assembly, Link.ReceivedLot2, Inner: true)]),
        ["disc"] = new(Titles["disc"], ["อัดดิสเบรก", "ดิสเบรก"], Stage.DiscPress, [new(Stage.Disc, Link.ReceivedLot1)]),
        ["from-die"] = new(Titles["from-die"], ["ไดคาสท์", "ตกแต่ง", "ประกอบ", "ฝนผ้าเบรก", "อัดผ้าเบรก"], Stage.Die,
            [new(Stage.Trim, Link.ReceivedLot1), new(Stage.Assembly, Link.ReceivedLot1), new(Stage.Grind, Link.MadeLotOfLot2), new(Stage.Press, Link.MadeLotOfLot1)], Distinct: true),
        ["from-press"] = new(Titles["from-press"], ["อัดผ้าเบรก", "ฝนผ้าเบรก", "ประกอบ", "ตกแต่ง", "ไดคาสท์"], Stage.Press,
            [new(Stage.Grind, Link.ReceivedLot1), new(Stage.Assembly, Link.ReceivedLot2), new(Stage.Trim, Link.MadeLotOfLot1), new(Stage.Die, Link.MadeLotOfLot1)]),
    };

    private static bool StartsWith(string? text, string prefix) => text?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>The stage a line belongs to is told by its machine's code and its product (as the legacy filters): d… trimmed of P and G0 products, t…, a…, g…, b… (g5… for the brake-pad pairing, GD… for disc).</summary>
    private static bool Is(Stage stage, LotLine line, string? machine) => stage switch
    {
        Stage.Die => StartsWith(machine, "d") && !StartsWith(line.ProductCode, "P") && !StartsWith(line.ProductCode, "G0"),
        Stage.Trim => StartsWith(machine, "t"),
        Stage.Assembly => StartsWith(machine, "a"),
        Stage.Grind => StartsWith(machine, "g"),
        Stage.Press => StartsWith(machine, "b") && !StartsWith(line.ProductCode, "GD"),
        Stage.PressG5 => StartsWith(machine, "b") && StartsWith(line.ProductCode, "g5"),
        Stage.DiscPress => StartsWith(machine, "b") && StartsWith(line.ProductCode, "GD"),
        Stage.Disc => StartsWith(machine, "c") && StartsWith(line.ProductCode, "GD"),
        _ => false,
    };

    /// <summary>The key of the previous line a step looks for, and the key of the candidate that must equal it.</summary>
    private static (string? Wanted, Func<LotLine, string?> Key) Join(Link link, LotLine previous) => link switch
    {
        Link.ReceivedLot1 => (previous.Lot, c => c.MaterialLot1),
        Link.ReceivedLot2 => (previous.Lot, c => c.MaterialLot2),
        Link.MadeLotOfLot1 => (previous.MaterialLot1, c => c.Lot),
        _ => (previous.MaterialLot2, c => c.Lot),
    };

    private static async Task<IReadOnlyList<LotLine>> FetchAsync(ProductionReportDirectory reports, Link link, IReadOnlyCollection<string> keys) => link switch
    {
        Link.ReceivedLot1 => await reports.GetLotLinesByMaterialLotAsync(keys),
        Link.ReceivedLot2 => await reports.GetLotLinesByMaterialLotAsync(keys, second: true),
        _ => await reports.GetLotLinesByLotAsync(keys),
    };

    public static async Task<LotTraceReport?> BuildAsync(ProductionReportDirectory reports, OrgDirectory org, string kind, DateOnly from, DateOnly to)
    {
        if (!Chains.TryGetValue(kind, out var chain)) return null;
        var machines = (await org.GetMachinesAsync()).ToDictionary(m => m.Id, m => m.Code);
        string? MachineOf(LotLine l) => l.MachineId is { } id ? machines.GetValueOrDefault(id) : null;
        bool InRange(LotLine l) => l.Date >= from && l.Date <= to;

        var rows = (await reports.GetLotLinesAsync(from, to)).Where(l => Is(chain.First, l, MachineOf(l)))
            .OrderBy(l => l.Date).ThenBy(l => l.Lot, StringComparer.Ordinal).Select(l => new List<LotLine?> { l }).ToList();

        for (var s = 0; s < chain.Next.Length; s++)
        {
            var step = chain.Next[s];
            var keys = rows.Select(r => r[s]).Where(l => l is not null).Select(l => Join(step.Link, l!).Wanted).Where(k => !string.IsNullOrEmpty(k)).Select(k => k!).Distinct().ToList();
            var candidates = (await FetchAsync(reports, step.Link, keys)).Where(c => Is(step.Stage, c, MachineOf(c)) && (!step.InRange || InRange(c))).ToList();
            var next = new List<List<LotLine?>>();
            foreach (var row in rows)
            {
                var previous = row[s];
                var matches = new List<LotLine>();
                if (previous is not null)
                {
                    var (wanted, key) = Join(step.Link, previous);
                    if (!string.IsNullOrEmpty(wanted)) matches = candidates.Where(c => key(c) == wanted).ToList();
                }
                if (matches.Count == 0)
                {
                    if (!step.Inner) next.Add([.. row, null]); // a left join keeps the line, blank
                    continue;
                }
                next.AddRange(matches.Select(m => new List<LotLine?>([.. row, m])));
            }
            rows = next;
        }

        var cells = rows.Select(r => new LotTraceRow(r.Select((l, i) => l is null ? null : new LotCell(i == 0 ? l.Date : null, l.ProductCode, l.ProductName, i == 0 ? l.Good : l.Total, l.Lot, i == 0 ? l.MaterialLot1 : null)).ToList()));
        if (chain.Distinct)
            cells = cells.GroupBy(r => string.Join("|", r.Cells.Select(c => c is null ? "" : $"{c.Date}{c.ProductCode}{c.Quantity}{c.Lot}{c.MaterialLot}"))).Select(g => g.First());
        return new(chain.Title, from, to, chain.StageNames, cells.ToList());
    }

    public static RouteGroupBuilder MapLotTraceReports(this RouteGroupBuilder reports)
    {
        reports.MapGet("/order/lot-trace/{kind}", async (string kind, DateOnly? from, DateOnly? to, HttpContext http, ProductionReportDirectory directory, OrgDirectory org, ReportPrinter printer,
            CancellationToken ct) =>
        {
            if (HrAttendanceReports.Range(from, to) is { } bad) return bad;
            if (await BuildAsync(directory, org, kind, from!.Value, to!.Value) is not { } report) return Results.NotFound();
            return await HrAttendanceReports.PrintAsync<LotTraceView>(http, printer, report.Title, Paper.A4Landscape, report, report.Title, ct);
        });
        return reports;
    }
}

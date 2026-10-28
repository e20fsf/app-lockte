using System.Globalization;
using System.Text;
using Lockte.Hr;
using Lockte.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Lockte.Migration;

/// <summary>One acceptance criterion of a period compared: who was compared and what differs (both values).</summary>
public sealed record PeriodCriterion(string Id, string Title, string File, bool HasBaseline, IReadOnlyList<string> People,
    IReadOnlyList<PayMismatch> Mismatches, string? Note = null)
{
    public bool Passed => HasBaseline && Mismatches.Count == 0;

    /// <summary>ตรง / ไม่ตรง / – (no baseline file, or the person is not in it) for one person.</summary>
    public string Verdict(string code)
        => !HasBaseline ? "–" : Mismatches.Any(m => string.Equals(m.Code, code, StringComparison.OrdinalIgnoreCase)) ? "ไม่ตรง"
            : People.Contains(code, StringComparer.OrdinalIgnoreCase) ? "ตรง" : "–";
}

public sealed record PeriodComparisonResult(DateOnly From, DateOnly To, DateOnly PayDate, IReadOnlyList<PeriodCriterion> Criteria,
    IReadOnlyList<BankTransferSummary> NewBank, IReadOnlyList<BankTransferSummary> LegacyBank)
{
    public bool Passed => Criteria.All(c => c.Passed);
}

/// <summary>
/// รายงานผลเทียบย้อนหลังสำหรับตัดสินเปิดใช้ (#57, ข้อตัดสิน #29): one period closed in Access against the same period in the
/// database, in one run — เวลารวม (AC-02), ค่าแรง (AC-03), ยอดส่งธนาคาร (AC-06), สลิป (AC-07). The baseline folder holds CSVs
/// exported from a copy of <c>source/พนักงาน.accdb</c> (real data: never in git, the output neither):
/// <c>inputs.csv</c> (คำนวณเวลาทำงาน13 with EMPLO: ID,W,L,O1,S,O2,H,O3,HOLI,SUN …, as the slip golden master reads it),
/// <c>pay-history.csv</c> (<see cref="PayBaseline.Header"/>), <c>slip.csv</c> (the table SLIP), <c>bank.csv</c>
/// (<c>BANK,ACC,AMT</c>, BANK = TTB เงินเดือนส่งแบ็งค์ · KBANK เงินOTส่งแบ็งค์ · LETTER ค่าแรงส่งแบงค์10). A missing file is
/// reported as not compared, never as a pass. Rerun it for the next period when a comparison fails.
/// </summary>
public static class PeriodComparison
{
    public const string TimeFile = "inputs.csv", PayFile = "pay-history.csv", SlipFile = "slip.csv", BankFile = "bank.csv";

    /// <summary>
    /// Compares the period starting <paramref name="from"/>. With <paramref name="calculate"/> the period is created from the
    /// baseline's dates if missing and เวลาทำงาน then ค่าแรง are calculated first (writes — a database to throw away);
    /// without it the period must already be calculated.
    /// </summary>
    public static async Task<PeriodComparisonResult> RunAsync(IServiceProvider services, string folder, DateOnly from, bool calculate)
    {
        var payPath = Path.Combine(folder, PayFile);
        var pay = File.Exists(payPath) ? PayBaseline.Read(payPath).Where(p => p.From == from).ToList() : [];
        await using var scope = services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<PayPeriodDirectory>();
        var registry = scope.ServiceProvider.GetRequiredService<PayPeriodRegistry>();

        var period = (await directory.ListAsync()).FirstOrDefault(p => p.From == from);
        if (calculate)
        {
            if (period is null)
            {
                if (pay.Count == 0) throw new InvalidOperationException($"{PayFile} ไม่มีงวดที่เริ่ม {Day(from)} — ไม่รู้วันสิ้นสุดและวันจ่ายของงวด");
                var saved = await registry.SaveAsync(new PayPeriodInput(null, from, pay[0].To, pay[0].PayDate ?? pay[0].To, null));
                if (!saved.Succeeded) throw new InvalidOperationException($"สร้างงวดไม่ได้: {string.Join(", ", saved.Errors)}");
                period = await directory.GetAsync(saved.Id!.Value);
            }
            var id = period!.Id;
            foreach (var step in new Func<Task<ServiceResult>>[] { () => registry.CalculateTimeAsync(id), () => registry.CalculatePayAsync(id) })
            {
                var result = await step();
                if (!result.Succeeded) throw new InvalidOperationException($"คำนวณงวด {Day(from)} ไม่ได้: {string.Join(", ", result.Errors)}");
            }
            period = await directory.GetAsync(id);
        }
        if (period is null || period.CompletedStep < PayPeriodStep.PayCalculated)
            throw new InvalidOperationException($"งวดที่เริ่ม {Day(from)} ยังไม่ได้คำนวณค่าแรงในฐานข้อมูล — คำนวณก่อน หรือใช้ --calculate yes บนฐานข้อมูลที่ทิ้งได้");

        var times = await directory.GetTimeTotalsAsync(period.Id);
        var pays = await directory.GetPayAsync(period.Id);
        var batch = await directory.GetSlipsAsync(period.Id);
        var bank = BankTransfers.Build(await directory.GetBankInputsAsync(period.Id));

        var criteria = new List<PeriodCriterion>
        {
            Time(folder, from, times),
            Pay(folder, from, pay, pays),
            Bank(folder, from, bank, out var legacyBank),
            Slips(folder, period, batch),
        };
        return new PeriodComparisonResult(period.From, period.To, period.PayDate, criteria, BankTransfers.Summary(bank), legacyBank);
    }

    private static PeriodCriterion Time(string folder, DateOnly from, IReadOnlyList<EmployeeTimeTotal> times)
    {
        const string id = "AC-02", title = "เวลาทำงานรวมต่อคน";
        var path = Path.Combine(folder, TimeFile);
        if (!File.Exists(path)) return Missing(id, title, TimeFile);
        var legacy = ReadRows(path);
        return new(id, title, TimeFile, true,
            legacy.Select(r => r["ID"].ToUpperInvariant()).Union(times.Select(t => t.Code), StringComparer.OrdinalIgnoreCase).ToList(),
            TimeBaseline.Compare(from, legacy, times));
    }

    private static PeriodCriterion Pay(string folder, DateOnly from, IReadOnlyList<BaselinePay> legacy, IReadOnlyList<EmployeePay> pays)
    {
        const string id = "AC-03", title = "ค่าแรงต่อคน";
        if (!File.Exists(Path.Combine(folder, PayFile))) return Missing(id, title, PayFile);
        return new(id, title, PayFile, true, legacy.Select(l => l.Code).Union(pays.Select(p => p.Code), StringComparer.OrdinalIgnoreCase).ToList(),
            PayBaseline.Compare(from, legacy, pays), legacy.Count == 0 ? $"{PayFile} ไม่มีแถวของงวดนี้" : null);
    }

    private static PeriodCriterion Slips(string folder, PayPeriodDto period, PaySlipBatch? batch)
    {
        const string id = "AC-07", title = "สลิป (ทุกช่องตัวเลข)";
        var path = Path.Combine(folder, SlipFile);
        if (!File.Exists(path)) return Missing(id, title, SlipFile);
        if (batch is null)
            return new(id, title, SlipFile, true, [], [new(period.From, "(งวด)", "สลิป", "มี", "ไม่มี")],
                "งวดนี้พิมพ์สลิปไม่ได้ (ย้ายมาจากระบบเดิม ไม่มีส่วนย่อยของค่าแรง)");
        var from = period.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var legacy = SlipBaseline.Read(path)
            .Where(s => !s.Fields.TryGetValue("FROM_D", out var f) || f.Length == 0 || f.StartsWith(from, StringComparison.Ordinal)).ToList();
        var dates = new PayPeriodDates(period.From, period.To, period.PayDate);
        return new(id, title, SlipFile, true, legacy.Select(l => l.Code).Union(batch.Slips.Select(s => s.Code), StringComparer.OrdinalIgnoreCase).ToList(),
            SlipBaseline.Compare(dates, legacy, batch.Slips));
    }

    private static PeriodCriterion Bank(string folder, DateOnly from, IReadOnlyList<BankTransferRow> bank, out IReadOnlyList<BankTransferSummary> legacySummary)
    {
        const string id = "AC-06", title = "ยอดส่งธนาคาร";
        var path = Path.Combine(folder, BankFile);
        legacySummary = [];
        if (!File.Exists(path)) return Missing(id, title, BankFile);
        var legacy = ReadRows(path).Select(r => new BankTransferRow(r["BANK"].ToUpperInvariant(), BankTransfers.Account(r["ACC"]) ?? r["ACC"],
            decimal.Parse(r["AMT"].Length == 0 ? "0" : r["AMT"], CultureInfo.InvariantCulture), "")).ToList();
        legacySummary = BankTransfers.Summary(legacy);
        var codeOf = bank.GroupBy(b => b.Account).ToDictionary(g => g.Key, g => g.First().Code);
        var mismatches = new List<PayMismatch>();
        static string N(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        var old = legacy.GroupBy(r => (r.Bank, r.Account)).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
        var now = bank.GroupBy(r => (r.Bank, r.Account)).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
        var people = new List<string>();
        foreach (var key in old.Keys.Union(now.Keys).OrderBy(k => k.Bank, StringComparer.Ordinal).ThenBy(k => k.Account, StringComparer.Ordinal))
        {
            var code = codeOf.GetValueOrDefault(key.Account) ?? "บัญชี " + key.Account;
            people.Add(code);
            var hasOld = old.TryGetValue(key, out var legacyAmount);
            var hasNew = now.TryGetValue(key, out var newAmount);
            if (!hasNew) mismatches.Add(new(from, code, $"{key.Bank} รายการ", "มี", "ไม่มี"));
            else if (!hasOld) mismatches.Add(new(from, code, $"{key.Bank} รายการ", "ไม่มี", "มี"));
            else if (legacyAmount != newAmount) mismatches.Add(new(from, code, $"{key.Bank} ยอด", N(legacyAmount), N(newAmount)));
        }
        // จำนวนรายการ ยอดรวม ค่าธรรมเนียม (หนังสือนำส่งข้อมูล), per list.
        var summaryNew = BankTransfers.Summary(bank).ToDictionary(s => s.Bank);
        var summaryOld = legacySummary.ToDictionary(s => s.Bank);
        foreach (var list in summaryOld.Keys.Union(summaryNew.Keys).Order(StringComparer.Ordinal))
        {
            var o = summaryOld.GetValueOrDefault(list) ?? new BankTransferSummary(list, 0, 0, 0);
            var n = summaryNew.GetValueOrDefault(list) ?? new BankTransferSummary(list, 0, 0, 0);
            if (o.Items != n.Items) mismatches.Add(new(from, "(สรุป)", $"{list} จำนวนรายการ", o.Items.ToString(CultureInfo.InvariantCulture), n.Items.ToString(CultureInfo.InvariantCulture)));
            if (o.Total != n.Total) mismatches.Add(new(from, "(สรุป)", $"{list} ยอดรวม", N(o.Total), N(n.Total)));
            if (o.Fee != n.Fee) mismatches.Add(new(from, "(สรุป)", $"{list} ค่าธรรมเนียม", N(o.Fee), N(n.Fee)));
        }
        return new(id, title, BankFile, true, people.Distinct().ToList(), mismatches,
            "เทียบยอดและจำนวนรายการ ยังไม่ได้เทียบไฟล์ Excel ของธนาคาร (#54) · ยอดโอนใหม่ยังไม่หักเงินสงเคราะห์ลูกจ้าง (Q-54)");
    }

    private static PeriodCriterion Missing(string id, string title, string file)
        => new(id, title, file, false, [], [], $"ไม่พบ {file} ใน baseline — ยังไม่ได้เทียบ");

    /// <summary>The rows of a CSV exported from Access as column → value, headers upper-cased, UTF-8 BOM allowed.</summary>
    internal static List<Dictionary<string, string>> ReadRows(string path)
    {
        var lines = File.ReadAllLines(path).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0) return [];
        var header = lines[0].TrimStart('﻿').Split(',').Select(h => h.Trim().ToUpperInvariant()).ToArray();
        return lines.Skip(1).Select(l =>
        {
            var f = SlipBaseline.Csv(l);
            return header.Select((h, i) => (h, v: i < f.Length ? f[i].Trim() : "")).ToDictionary(x => x.h, x => x.v);
        }).ToList();
    }

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ---------- report ----------

    /// <summary>
    /// <c>period-compare-summary.md</c> (for the head of HR: per criterion ตรง/ไม่ตรง, then the differences with both sides),
    /// <c>period-compare-people.csv</c> (per person per criterion) and <c>period-compare-details.csv</c>. Real codes and
    /// amounts — keep the folder outside git.
    /// </summary>
    public static void WriteReport(string folder, PeriodComparisonResult r)
    {
        Directory.CreateDirectory(folder);
        var md = new StringBuilder();
        md.AppendLine($"# ผลเทียบงวด {Day(r.From)} – {Day(r.To)} (จ่าย {Day(r.PayDate)}) กับ Access").AppendLine();
        md.AppendLine($"**สรุป: {(r.Passed ? "ตรงทุกเกณฑ์" : "ยังไม่ผ่าน")}** — ข้อมูลจริง เก็บนอก git").AppendLine();
        md.AppendLine("| เกณฑ์ | ผล | คน/รายการที่เทียบ | ตรง | ไม่ตรง (ช่อง) |");
        md.AppendLine("|---|---|---|---|---|");
        foreach (var c in r.Criteria)
        {
            var differing = c.Mismatches.Select(m => m.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var matched = c.HasBaseline ? (c.People.Count - differing).ToString("N0", CultureInfo.InvariantCulture) : "–";
            var wrong = c.HasBaseline ? $"{differing:N0} ({c.Mismatches.Count:N0})" : "–";
            md.AppendLine($"| {c.Id} {c.Title} | {(!c.HasBaseline ? "ไม่ได้เทียบ" : c.Passed ? "ตรง" : "ไม่ตรง")} | {c.People.Count:N0} | {matched} | {wrong} |");
        }
        md.AppendLine();
        md.AppendLine("## ยอดส่งธนาคาร (AC-06)").AppendLine();
        md.AppendLine("| รายการ | Access: จำนวน / ยอดรวม / ค่าธรรมเนียม | ระบบใหม่: จำนวน / ยอดรวม / ค่าธรรมเนียม |");
        md.AppendLine("|---|---|---|");
        static string Text(BankTransferSummary? s) => s is null ? "–" : $"{s.Items:N0} / {s.Total:N2} / {s.Fee:N2}";
        foreach (var bank in r.NewBank.Select(b => b.Bank).Union(r.LegacyBank.Select(b => b.Bank)).Order(StringComparer.Ordinal))
            md.AppendLine($"| {bank} | {Text(r.LegacyBank.FirstOrDefault(b => b.Bank == bank))} | {Text(r.NewBank.FirstOrDefault(b => b.Bank == bank))} |");
        md.AppendLine();
        foreach (var c in r.Criteria)
        {
            md.AppendLine($"## {c.Id} {c.Title}").AppendLine();
            if (c.Note is not null) md.AppendLine($"- หมายเหตุ: {c.Note}");
            if (c.HasBaseline && c.Mismatches.Count == 0) md.AppendLine("- ตรงทุกคนทุกช่อง");
            if (c.Mismatches.Count > 0)
            {
                md.AppendLine($"- ไม่ตรง {c.Mismatches.Count:N0} ช่อง (ทั้งหมดใน period-compare-details.csv) — 50 รายการแรก:").AppendLine();
                md.AppendLine("| รหัส | ช่อง | Access | ระบบใหม่ |");
                md.AppendLine("|---|---|---|---|");
                foreach (var m in c.Mismatches.Take(50)) md.AppendLine($"| {m.Code} | {m.Field} | {m.Legacy} | {m.New} |");
            }
            md.AppendLine();
        }
        File.WriteAllText(Path.Combine(folder, "period-compare-summary.md"), md.ToString(), new UTF8Encoding(true));

        var everyone = r.Criteria.SelectMany(c => c.People).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal);
        Csv.Write(Path.Combine(folder, "period-compare-people.csv"), ["รหัส", .. r.Criteria.Select(c => $"{c.Id} {c.Title}")],
            everyone.Select(code => (object?[])[code, .. r.Criteria.Select(c => c.Verdict(code))]));
        Csv.Write(Path.Combine(folder, "period-compare-details.csv"), ["เกณฑ์", "รหัส", "ช่อง", "Access", "ระบบใหม่"],
            r.Criteria.SelectMany(c => c.Mismatches.Select(m => (object?[])[c.Id, m.Code, m.Field, m.Legacy, m.New])));
    }
}

/// <summary>เวลาทำงานรวมต่อคน (AC-02) against คิวรี คำนวณเวลาทำงาน13: W, L, O1, S, O2, H, O3 to 0.1 as printed, HOLI and SUN whole.</summary>
public static class TimeBaseline
{
    public static IReadOnlyList<PayMismatch> Compare(DateOnly from, IEnumerable<IReadOnlyDictionary<string, string>> legacy, IEnumerable<EmployeeTimeTotal> totals)
    {
        var old = legacy.ToDictionary(r => r["ID"].ToUpperInvariant());
        var now = totals.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        var mismatches = new List<PayMismatch>();
        static decimal Hours(double v) => Math.Round((decimal)v, 1, MidpointRounding.AwayFromZero);
        static string T(decimal v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        foreach (var code in old.Keys.Union(now.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
        {
            if (!now.TryGetValue(code, out var t)) { mismatches.Add(new(from, code, "แถว", "มี", "ไม่มี")); continue; }
            if (!old.TryGetValue(code, out var row)) { mismatches.Add(new(from, code, "แถว", "ไม่มี", "มี")); continue; }
            void Check(string field, double newValue)
            {
                if (!row.TryGetValue(field, out var raw)) return;
                var legacyValue = raw.Length == 0 ? 0 : double.Parse(raw, CultureInfo.InvariantCulture);
                if (Hours(legacyValue) != Hours(newValue)) mismatches.Add(new(from, code, field, T(Hours(legacyValue)), T(Hours(newValue))));
            }
            Check("W", t.Normal); Check("L", t.PaidLeave); Check("O1", t.Overtime); Check("S", t.Sunday); Check("O2", t.SundayOvertime);
            Check("H", t.Holiday); Check("O3", t.HolidayOvertime); Check("HOLI", t.PublicHolidays); Check("SUN", t.WeeklyHolidays);
        }
        return mismatches;
    }
}

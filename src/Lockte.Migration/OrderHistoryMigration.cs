using Lockte.Prod;
using Lockte.Sales;

namespace Lockte.Migration;

/// <summary>
/// ย้าย ORDER ค้างส่ง (<c>ODD</c>), ประวัติการรับ ORDER (<c>ORDER</c>) และประวัติการส่งของ (<c>ODDส่งออก</c>) ของ
/// <c>ผลผลิตและORDER.accdb</c> (#112). Run it <b>after</b> <see cref="OrderMasterDataMigration"/> — it needs the customers, territories
/// and products that one loaded, and the master-data run deletes the orders first because they refer to those by id.
/// <list type="bullet">
/// <item>ORDER = เลขที่ + ลูกค้า. The lines of both tables with the same เลขที่ + ลูกค้า + ลำดับ become one line with two sets of
/// values (รับ from ORDER, ค้างส่ง from ODD); a ค้างส่ง line that is not in the history keeps only its ค้างส่ง values, a history line
/// with no ค้างส่ง was delivered in full (#28).</item>
/// <item>A number that several customers used (91 in the 2026 copy): the first order keeps it, the others get <c>-2</c>, <c>-3</c> and
/// keep the old number as เลขเดิม; each is in the DQ report. A number one customer used on several dates is one order. A delivery is
/// tied to the order of its เลขที่ + ลูกค้า.</item>
/// <item>ประวัติ ORDER and การส่งของ are moved from 1 Jan 2025 (<see cref="LegacyDatabase.OrderHistoryFrom"/>); ODD all. A row or value it
/// cannot take goes to the DQ report instead of stopping the run. Amounts are moved as stored (Q-39) and the rows whose amount is not
/// จำนวน × ราคา are counted.</item>
/// </list>
/// The rows are deleted and loaded again, through <see cref="SalesOrderImport"/> — no row-by-row change history (#39).
/// </summary>
public sealed class OrderHistoryMigration(
    SalesOrderImport import,
    CustomerDirectory customerDirectory,
    SalesTerritoryDirectory territoryDirectory,
    ProductDirectory productDirectory)
{
    private const string Other = "NF-06";
    private const string Duplicate = "DQ-2";
    private const string ZeroDate = "DQ-4";

    private const string HistoryTable = "ORDER";
    private const string BackorderTable = "ODD";
    private const string DeliveryTable = "ODDส่งออก";

    private LegacyDatabase legacy = null!;
    private CodeMap map = null!;
    private MigrationReport report = null!;
    private Dictionary<string, CustomerDto> customers = null!;
    private Dictionary<string, SalesTerritoryDto> territories = null!;
    private Dictionary<string, ProductListItem> products = null!;
    private readonly Dictionary<string, Tally> tallies = [];
    private readonly Dictionary<(string No, string Customer), string> finalNumbers = [];

    public async Task RunAsync(LegacyDatabase legacyDatabase, CodeMap codeMap, MigrationReport migrationReport)
    {
        (legacy, map, report) = (legacyDatabase, codeMap, migrationReport);
        customers = (await customerDirectory.GetAllAsync()).ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        territories = (await territoryDirectory.GetAllAsync()).ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        products = (await productDirectory.SearchAsync(null, null, int.MaxValue)).ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var (table, newTable) in new[] { (BackorderTable, "sales.SalesOrderLines (ค้างส่ง)"), (HistoryTable, "sales.SalesOrderLines (รับ)"), (DeliveryTable, "sales.Deliveries") })
            tallies[table] = new Tally(table, newTable) { Source = legacy.SourceRows(table), Excluded = legacy.SourceRows(table) - legacy[table].Count };

        var sources = ReadOrderRows();
        var orders = BuildOrders(sources);
        AssignNumbers(orders);
        var orderRows = orders.Select(ToImport).ToList();
        await import.AddOrdersAsync(orderRows);

        var deliveries = BuildDeliveries();
        await import.AddDeliveriesAsync(deliveries);

        Notes();
        await CountBackAsync(sources, orderRows, deliveries);
    }

    // ---------- ORDER and ODD ----------

    private sealed class Src(string table, LegacyRow row, string no, string customerLegacy, string customer, int line, ProductListItem product)
    {
        public string Table { get; } = table;
        public LegacyRow Row { get; } = row;
        public string No { get; } = no;
        public string CustomerLegacy { get; } = customerLegacy;
        public string Customer { get; } = customer;
        public int Line { get; } = line;
        public ProductListItem Product { get; } = product;
        public bool Done { get; set; }
    }

    private List<Src> ReadOrderRows()
    {
        var rows = new List<Src>();
        foreach (var table in new[] { HistoryTable, BackorderTable })
        {
            var tally = tallies[table];
            var position = 0;
            foreach (var row in legacy[table])
            {
                position++;
                var where = $"แถวที่ {position}";
                if (row.Text("REF_NO") is not { } no) { Reject(tally, table, $"(REF_NO ว่าง) {where}", Other, "ไม่มีเลขที่ ORDER"); continue; }
                var key = $"{no} {where}";
                if (Int(row, "SUF_NO") is not { } line) { Reject(tally, table, key, Other, "ไม่มีลำดับบรรทัด (SUF_NO)"); continue; }
                if (row.Text("CUS_ID") is not { } legacyCustomer) { Reject(tally, table, key, Other, "ไม่มีรหัสลูกค้า (CUS_ID)"); continue; }
                var customer = (map.Find(CodeKinds.Customer, legacyCustomer)?.Code ?? legacyCustomer).ToUpperInvariant();
                if (!customers.ContainsKey(customer)) { Reject(tally, table, key, Other, $"ลูกค้า '{legacyCustomer}' ไม่มีในทะเบียนลูกค้า"); continue; }
                if (row.Text("PD_C") is not { } productCode || !products.TryGetValue(productCode, out var product))
                {
                    Reject(tally, table, key, Other, $"สินค้า '{row.Text("PD_C")}' ไม่มีในทะเบียนสินค้า");
                    continue;
                }
                CountAmount(table, row);
                rows.Add(new Src(table, row, no, legacyCustomer, customer, line, product));
            }
        }
        return rows;
    }

    private sealed class OrderBuild(string no, string customer)
    {
        public string No { get; } = no;
        public string Customer { get; } = customer;
        public string FinalNo { get; set; } = no;
        public DateOnly Date { get; set; }
        public string CustomerName { get; set; } = "";
        public int? TerritoryId { get; set; }
        public string TerritoryCode { get; set; } = "";
        public DateOnly? DeliveryDate { get; set; }
        public string? PaymentTerms { get; set; }
        public string? Remark { get; set; }
        public bool InHistory { get; set; }
        public List<(Src? History, Src? Backorder, decimal? Outstanding)> Lines { get; } = [];
    }

    private List<OrderBuild> BuildOrders(List<Src> sources)
    {
        var builds = new List<OrderBuild>();
        var severalDates = 0;
        foreach (var group in sources.GroupBy(s => (s.No.Trim().ToUpperInvariant(), s.Customer)))
        {
            var rows = group.ToList();
            var build = new OrderBuild(rows[0].No.Trim(), group.Key.Customer);
            foreach (var lineGroup in rows.GroupBy(r => r.Line).OrderBy(g => g.Key))
            {
                var history = One(lineGroup.Where(r => r.Table == HistoryTable).ToList());
                var backorder = One(lineGroup.Where(r => r.Table == BackorderTable).ToList());
                if (history is not null && Decimal(history.Row, "QTY") is null)
                {
                    Reject(history, Other, "จำนวนว่าง — ไม่ย้ายชุดค่าที่รับ");
                    history = null;
                }
                decimal? outstanding = backorder is null ? null : Decimal(backorder.Row, "QTY") ?? Decimal(backorder.Row, "UND_QTY");
                if (backorder is not null && outstanding is null or 0)
                {
                    Reject(backorder, Other, "ยอดค้างส่งว่างหรือเป็น 0 — ไม่ย้ายชุดค่าค้างส่ง");
                    backorder = null;
                    outstanding = null;
                }
                if (history is null && backorder is null) continue;
                if (history is not null && backorder is not null && history.Product.Id != backorder.Product.Id)
                    report.Add(backorder.Table, $"{backorder.No} #{backorder.Line}", Duplicate,
                        "สินค้าของค่าค้างส่งไม่ตรงกับในประวัติ ORDER (เลขที่+ลูกค้า+ลำดับเดียวกัน) — รวมเป็นบรรทัดเดียว ใช้สินค้าตามประวัติ", MigrationReport.LoadedBlank);
                build.Lines.Add((history, backorder, outstanding));
            }
            var live = build.Lines.SelectMany(l => new[] { l.History, l.Backorder }).OfType<Src>().ToList();
            if (live.Count == 0) continue;

            var dates = live.Select(r => CleanDate(r.Row, "OD_DATE", r.Table, $"{r.No} #{r.Line}")).OfType<DateOnly>().ToList();
            if (dates.Count == 0)
            {
                foreach (var row in live) Reject(row, ZeroDate, "ไม่มีวันที่ ORDER ที่ใช้ได้ — ไม่ย้ายใบนี้");
                continue;
            }
            if (dates.Distinct().Count() > 1) severalDates++;
            build.Date = dates.Min();

            var head = live.OrderBy(r => r.Table == HistoryTable ? 0 : 1).ThenBy(r => r.Line).First().Row;
            build.CustomerName = live.Select(r => r.Row.Text("CUS_N")).FirstOrDefault(n => n is not null) ?? customers[build.Customer].Name;
            build.DeliveryDate = live.Select(r => CleanDate(r.Row, "DATE_DL", r.Table, $"{r.No} #{r.Line}")).FirstOrDefault(d => d is not null);
            build.PaymentTerms = live.Select(r => r.Row.Text("DUERE_D")).FirstOrDefault(t => t is not null);
            build.Remark = head.Text("REMARK") ?? live.Select(r => r.Row.Text("REMARK")).FirstOrDefault(t => t is not null);
            build.InHistory = build.Lines.Any(l => l.History is not null);
            if (live.Select(r => r.Row.Text("SALE_ID")).FirstOrDefault(t => t is not null) is { } saleId)
            {
                if (territories.TryGetValue(saleId, out var territory)) (build.TerritoryId, build.TerritoryCode) = (territory.Id, territory.Code);
                else report.Add(live[0].Table, $"{build.No} {saleId}", Other, $"เขตการขาย (SALE_ID) '{saleId}' ไม่มีในทะเบียนเขตการขาย", MigrationReport.LoadedBlank);
            }
            builds.Add(build);
        }
        if (severalDates > 0)
            report.Notes.Add($"ORDER {severalDates} ใบที่แถวมีวันที่หลายวัน (เลขที่+ลูกค้าเดียวกัน) — รวมเป็นใบเดียว ใช้วันที่น้อยสุด");
        return builds;
    }

    /// <summary>The first of the rows of one table for one line; the others are the same key twice (DQ-2).</summary>
    private Src? One(List<Src> rows)
    {
        foreach (var extra in rows.Skip(1)) Reject(extra, Duplicate, $"เลขที่+ลูกค้า+ลำดับ ซ้ำ {rows.Count} แถวในตารางเดียวกัน — ใช้แถวแรก");
        return rows.FirstOrDefault();
    }

    /// <summary>
    /// A number that several customers used: the first order (by date, then customer code) keeps it, the others are numbered
    /// <c>-2</c>, <c>-3</c> … (not a number any order already has) and the old number is kept (#28).
    /// </summary>
    private void AssignNumbers(List<OrderBuild> builds)
    {
        var used = new HashSet<string>(builds.Select(b => b.No), StringComparer.OrdinalIgnoreCase);
        foreach (var same in builds.GroupBy(b => b.No, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = same.OrderBy(b => b.Date).ThenBy(b => b.Customer, StringComparer.Ordinal).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var build = ordered[i];
                if (i > 0)
                {
                    var suffix = i + 1;
                    string candidate;
                    while (!used.Add(candidate = $"{build.No}-{suffix}")) suffix++;
                    build.FinalNo = candidate;
                    report.Add(HistoryTable, $"{build.No} {build.Customer}", Duplicate,
                        $"เลขที่ ORDER ใช้กับลูกค้า {ordered.Count} ราย — เปลี่ยนเป็น {candidate} เก็บเลขเดิมไว้ในช่องเลขเดิม", "โหลดโดยเปลี่ยนเลขที่");
                }
                finalNumbers[(build.No.ToUpperInvariant(), build.Customer)] = build.FinalNo;
            }
        }
    }

    private OrderImportRow ToImport(OrderBuild b)
    {
        var lines = new List<OrderImportLine>();
        foreach (var (history, backorder, outstanding) in b.Lines)
        {
            var main = history ?? backorder!;
            var row = main.Row;
            var product = (history ?? backorder!).Product;
            decimal? receivedQuantity = history is null ? null : Decimal(history.Row, "QTY");
            decimal? receivedAmount = history is null ? null : Decimal(history.Row, "AMOUNT");
            decimal? outstandingAmount = backorder is null ? null : Decimal(backorder.Row, "AMOUNT");
            var b2 = backorder?.Row;
            lines.Add(new OrderImportLine(main.Line, product.Id, row.Text("PD_N") ?? product.Name, row.Text("U_NAME") ?? product.Unit, Decimal(row, "U_PRICE"),
                receivedQuantity, receivedAmount, outstanding ?? 0, outstandingAmount,
                b2 is null ? null : CleanDate(b2, "DL_DATE", BackorderTable, $"{main.No} #{main.Line}"), b2?.Text("INV_NO"), b2 is null ? null : Int(b2, "INV_SUF"),
                b2 is null ? null : Decimal(b2, "DL_QTY"), b2 is null ? null : Decimal(b2, "DL_NET"), b2?.Text("LOT"), b2 is null ? null : Decimal(b2, "DISC_PS")));
            if (history is not null) { tallies[HistoryTable].Loaded++; history.Done = true; }
            if (backorder is not null) { tallies[BackorderTable].Loaded++; backorder.Done = true; }
        }
        return new OrderImportRow(b.FinalNo, b.FinalNo == b.No ? null : b.No, b.Date, customers[b.Customer].Id, b.CustomerName, b.TerritoryId, b.DeliveryDate,
            b.PaymentTerms, b.Remark, b.InHistory, lines);
    }

    // ---------- ODDส่งออก ----------

    private List<DeliveryImportRow> BuildDeliveries()
    {
        var tally = tallies[DeliveryTable];
        var rows = new List<DeliveryImportRow>();
        var position = 0;
        var tiedToLoadedOrder = 0;
        var unmarked = 0;
        foreach (var row in legacy[DeliveryTable])
        {
            position++;
            if (row.Text("REF_NO") is not { } no) { Reject(tally, DeliveryTable, $"(REF_NO ว่าง) แถวที่ {position}", Other, "ไม่มีเลขที่ ORDER"); continue; }
            var key = $"{no} แถวที่ {position}";
            // A delivery without a line number (about 2% of the real file) is still a delivery: its reports do not need the line, so it is loaded with line 0.
            var lineNumber = Int(row, "SUF_NO");
            if (lineNumber is null) report.Add(DeliveryTable, key, Other, "ไม่มีลำดับบรรทัด (SUF_NO) — โหลดโดยใช้ลำดับ 0", MigrationReport.LoadedBlank);
            var line = lineNumber ?? 0;
            if (row.Text("CUS_ID") is not { } legacyCustomer) { Reject(tally, DeliveryTable, key, Other, "ไม่มีรหัสลูกค้า (CUS_ID)"); continue; }
            var customerCode = (map.Find(CodeKinds.Customer, legacyCustomer)?.Code ?? legacyCustomer).ToUpperInvariant();
            if (!customers.TryGetValue(customerCode, out var customer)) { Reject(tally, DeliveryTable, key, Other, $"ลูกค้า '{legacyCustomer}' ไม่มีในทะเบียนลูกค้า"); continue; }
            if (row.Text("PD_C") is not { } productCode || !products.TryGetValue(productCode, out var product))
            {
                Reject(tally, DeliveryTable, key, Other, $"สินค้า '{row.Text("PD_C")}' ไม่มีในทะเบียนสินค้า");
                continue;
            }
            if (Decimal(row, "DL_QTY") is not { } quantity) { Reject(tally, DeliveryTable, key, Other, "จำนวนส่ง (DL_QTY) ว่าง"); continue; }
            CountAmount(DeliveryTable, row);

            string orderNo;
            if (finalNumbers.TryGetValue((no.Trim().ToUpperInvariant(), customerCode), out var final)) { orderNo = final; tiedToLoadedOrder++; }
            else orderNo = no.Trim();
            var mark = row.Text("MARK");
            if (mark is not ("a" or "b")) unmarked++;
            territories.TryGetValue(row.Text("SALE_ID") ?? "", out var territory);
            rows.Add(new DeliveryImportRow(orderNo, CleanDate(row, "OD_DATE", DeliveryTable, key), line, customer.Id, row.Text("CUS_N") ?? customer.Name, territory?.Id,
                product.Id, row.Text("PD_N") ?? product.Name, row.Text("U_NAME") ?? product.Unit, Decimal(row, "U_PRICE"), CleanDate(row, "DATE_DL", DeliveryTable, key),
                CleanDate(row, "DL_DATE", DeliveryTable, key), row.Text("INV_NO"), Int(row, "INV_SUF"), row.Text("LOT"), quantity, Decimal(row, "DL_NET"),
                Decimal(row, "DISC_PS"), string.Equals(mark, "a", StringComparison.Ordinal)));
            tally.Loaded++;
        }
        report.Notes.Add($"การส่งของ {tiedToLoadedOrder} รายการผูกกับ ORDER ที่ย้ายมา (เลขที่+ลูกค้า) — ที่เหลืออ้าง ORDER ก่อนปี 2568 เก็บเลขที่เดิม");
        if (unmarked > 0)
            report.Notes.Add($"การส่งของ {unmarked} รายการไม่มีเครื่องหมาย a/b (บันทึกด้วยวิธีเดิม BR-013) — ตั้งเป็น \"ส่งไม่ครบ\" เพราะไม่ทราบว่าส่งครบหรือไม่");
        return rows;
    }

    // ---------- notes and รายงานตรวจยอด ----------

    private readonly Dictionary<string, (int Mismatch, int Blank)> amounts = [];

    private void CountAmount(string table, LegacyRow row)
    {
        var (mismatch, blank) = amounts.GetValueOrDefault(table);
        var quantity = table == DeliveryTable ? row.Number("DL_QTY") : row.Number("QTY");
        var amount = table == DeliveryTable ? row.Number("DL_NET") : row.Number("AMOUNT");
        var price = row.Number("U_PRICE");
        if (amount is null) blank++;
        else if (quantity is { } q && price is { } p && Math.Abs(amount.Value - q * p) > 0.005) mismatch++;
        amounts[table] = (mismatch, blank);
    }

    private void Notes()
    {
        foreach (var (table, (mismatch, blank)) in amounts)
            if (mismatch > 0 || blank > 0)
                report.Notes.Add($"{table}: มูลค่าที่เก็บไว้ไม่ตรงจำนวน×ราคา {mismatch} แถว และว่าง {blank} แถว — ย้ายตามค่าเดิม ไม่แก้ (Q-39)");
    }

    private async Task CountBackAsync(List<Src> sources, List<OrderImportRow> orders, List<DeliveryImportRow> deliveries)
    {
        var inDatabase = new Dictionary<string, int>
        {
            [BackorderTable] = await import.CountOutstandingLinesAsync(),
            [HistoryTable] = await import.CountReceivedLinesAsync(),
            [DeliveryTable] = await import.CountDeliveriesAsync(),
        };
        report.Tables.AddRange(tallies.Values.Select(t => new TableCount(t.LegacyTable, t.NewTable, t.Source, t.Excluded, t.Rejected, t.Loaded, inDatabase[t.LegacyTable])));

        // ORDER by month and territory (AC-17), ค้างส่ง by month (AC-19), การส่งของ by month (AC-16): as read, as it should load, as it is.
        var territoryOf = territories.Values.ToDictionary(t => t.Id, t => t.Code);
        var months = await import.GetOrderMonthTotalsAsync();
        var deliveryMonths = await import.GetDeliveryMonthTotalsAsync();

        var sourceHistory = sources.Where(s => s.Table == HistoryTable).GroupBy(s => (s.No.Trim().ToUpperInvariant(), s.Customer)).ToList();
        AddTotals(HistoryTable, "จำนวนใบ",
            sourceHistory.GroupBy(g => (RawMonth(g.First().Row, "OD_DATE"), g.First().Row.Text("SALE_ID") ?? "")).ToDictionary(g => g.Key, g => (double)g.Count()),
            orders.Where(o => o.InHistory).GroupBy(o => (Month(o.OrderDate), TerritoryCode(o.SalesTerritoryId))).ToDictionary(g => g.Key, g => (double)g.Count()),
            months.ToDictionary(m => (m.Month, m.TerritoryCode), m => (double)m.Orders));
        AddTotals(HistoryTable, "มูลค่าที่รับ",
            sources.Where(s => s.Table == HistoryTable).GroupBy(s => (RawMonth(s.Row, "OD_DATE"), s.Row.Text("SALE_ID") ?? "")).ToDictionary(g => g.Key, g => g.Sum(s => s.Row.Number("AMOUNT") ?? 0)),
            orders.GroupBy(o => (Month(o.OrderDate), TerritoryCode(o.SalesTerritoryId)))
                .ToDictionary(g => g.Key, g => (double)g.Sum(o => o.Lines.Sum(l => l.ReceivedAmount ?? 0))),
            months.ToDictionary(m => (m.Month, m.TerritoryCode), m => (double)m.ReceivedAmount));

        var back = sources.Where(s => s.Table == BackorderTable).ToList();
        AddTotals(BackorderTable, "ค้างส่ง จำนวนบรรทัด",
            back.GroupBy(s => (RawMonth(s.Row, "OD_DATE"), "")).ToDictionary(g => g.Key, g => (double)g.Count()),
            orders.GroupBy(o => (Month(o.OrderDate), "")).ToDictionary(g => g.Key, g => (double)g.Sum(o => o.Lines.Count(l => l.OutstandingQuantity != 0))),
            months.GroupBy(m => (m.Month, "")).ToDictionary(g => g.Key, g => (double)g.Sum(m => m.OutstandingLines)));
        AddTotals(BackorderTable, "ค้างส่ง จำนวน",
            back.GroupBy(s => (RawMonth(s.Row, "OD_DATE"), "")).ToDictionary(g => g.Key, g => g.Sum(s => s.Row.Number("QTY") ?? s.Row.Number("UND_QTY") ?? 0)),
            orders.GroupBy(o => (Month(o.OrderDate), "")).ToDictionary(g => g.Key, g => (double)g.Sum(o => o.Lines.Sum(l => l.OutstandingQuantity))),
            months.GroupBy(m => (m.Month, "")).ToDictionary(g => g.Key, g => (double)g.Sum(m => m.OutstandingQuantity)));
        AddTotals(BackorderTable, "ค้างส่ง มูลค่า",
            back.GroupBy(s => (RawMonth(s.Row, "OD_DATE"), "")).ToDictionary(g => g.Key, g => g.Sum(s => s.Row.Number("AMOUNT") ?? 0)),
            orders.GroupBy(o => (Month(o.OrderDate), "")).ToDictionary(g => g.Key, g => (double)g.Sum(o => o.Lines.Sum(l => l.OutstandingAmount ?? 0))),
            months.GroupBy(m => (m.Month, "")).ToDictionary(g => g.Key, g => (double)g.Sum(m => m.OutstandingAmount)));

        var raw = legacy[DeliveryTable];
        AddTotals(DeliveryTable, "จำนวนรายการส่ง",
            raw.GroupBy(r => (RawMonth(r, "DL_DATE"), "")).ToDictionary(g => g.Key, g => (double)g.Count()),
            deliveries.GroupBy(d => (d.DeliveryDate is { } date ? Month(date) : "", "")).ToDictionary(g => g.Key, g => (double)g.Count()),
            deliveryMonths.ToDictionary(m => (m.Month, ""), m => (double)m.Rows));
        AddTotals(DeliveryTable, "Σ จำนวนส่ง",
            raw.GroupBy(r => (RawMonth(r, "DL_DATE"), "")).ToDictionary(g => g.Key, g => g.Sum(r => r.Number("DL_QTY") ?? 0)),
            deliveries.GroupBy(d => (d.DeliveryDate is { } date ? Month(date) : "", "")).ToDictionary(g => g.Key, g => (double)g.Sum(d => d.Quantity)),
            deliveryMonths.ToDictionary(m => (m.Month, ""), m => (double)m.Quantity));
        AddTotals(DeliveryTable, "Σ มูลค่าส่ง",
            raw.GroupBy(r => (RawMonth(r, "DL_DATE"), "")).ToDictionary(g => g.Key, g => g.Sum(r => r.Number("DL_NET") ?? 0)),
            deliveries.GroupBy(d => (d.DeliveryDate is { } date ? Month(date) : "", "")).ToDictionary(g => g.Key, g => (double)g.Sum(d => d.Amount ?? 0)),
            deliveryMonths.ToDictionary(m => (m.Month, ""), m => (double)m.Amount));
        AddTotals(DeliveryTable, "Σ กล่อง",
            raw.GroupBy(r => (RawMonth(r, "DL_DATE"), "")).ToDictionary(g => g.Key, g => g.Sum(r => r.Number("DISC_PS") ?? 0)),
            deliveries.GroupBy(d => (d.DeliveryDate is { } date ? Month(date) : "", "")).ToDictionary(g => g.Key, g => (double)g.Sum(d => d.Boxes ?? 0)),
            deliveryMonths.ToDictionary(m => (m.Month, ""), m => (double)m.Boxes));

        string TerritoryCode(int? id) => id is { } i ? territoryOf.GetValueOrDefault(i, "") : "";
    }

    private void AddTotals(string table, string measure, Dictionary<(string Month, string Key), double> source,
        Dictionary<(string Month, string Key), double> expected, Dictionary<(string Month, string Key), double> inDatabase)
    {
        foreach (var key in source.Keys.Concat(expected.Keys).Concat(inDatabase.Keys).Distinct().OrderBy(k => k.Month, StringComparer.Ordinal).ThenBy(k => k.Key, StringComparer.Ordinal))
            report.MonthlyTotals.Add(new MonthlyTotal(table, key.Month, key.Key.Length > 0 ? $"{measure} เขต {key.Key}" : measure,
                source.GetValueOrDefault(key), expected.GetValueOrDefault(key), inDatabase.GetValueOrDefault(key)));
    }

    // ---------- helpers ----------

    /// <summary>The month of a legacy date as read (even a wrong year), so what the DQ report takes out shows as the difference.</summary>
    private static string RawMonth(LegacyRow row, string column) => row.Date(column) is { } date ? $"{date.Year:0000}-{date.Month:00}" : "";

    private static string Month(DateOnly date) => $"{date.Year:0000}-{date.Month:00}";

    /// <summary>A legacy date; the Access zero date (1899) is no date (DQ-4); a year no one could mean is left out and reported.</summary>
    private DateOnly? CleanDate(LegacyRow row, string column, string table, string key)
    {
        if (row.Date(column) is not { } date) return null;
        if (date.Year < 1900) return null;
        if (date.Year is < 1990 or > 2100)
        {
            report.Add(table, key, ZeroDate, $"{column} = {date:yyyy-MM-dd} เป็นปีที่ไม่น่าเป็นไปได้", MigrationReport.LoadedBlank);
            return null;
        }
        return DateOnly.FromDateTime(date);
    }

    private static int? Int(LegacyRow row, string column) => row.Number(column) is { } n ? (int)Math.Round(n) : null;

    private static decimal? Decimal(LegacyRow row, string column) => row.Number(column) is { } n ? Math.Round((decimal)n, 4) : null;

    private void Reject(Tally tally, string table, string key, string rule, string problem)
    {
        tally.Rejected++;
        report.Add(table, key, rule, problem, MigrationReport.NotLoaded);
    }

    /// <summary>Rejects a row already read: it is counted once, however many reasons it has.</summary>
    private void Reject(Src row, string rule, string problem)
    {
        if (row.Done) return;
        row.Done = true;
        Reject(tallies[row.Table], row.Table, $"{row.No} #{row.Line}", rule, problem);
    }

    private sealed class Tally(string legacyTable, string newTable)
    {
        public string LegacyTable { get; } = legacyTable;
        public string NewTable { get; } = newTable;
        public int Source { get; init; }
        public int Excluded { get; init; }
        public int Rejected { get; set; }
        public int Loaded { get; set; }
    }
}

using System.Globalization;
using Lockte.Org;
using Lockte.Purch;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Migration;

/// <summary>
/// ย้ายเอกสารจัดซื้อของ <c>วัตถุดิบ.accdb</c> (#266): the old tables hold one flat row per line with the head of the request and of the order repeated on every row — PO_RECEI4 the lines still
/// waiting for goods, PO_RECEI5 every delivery. They are taken apart into ใบขอซื้อ (head + lines), ใบสั่งซื้อ (head + lines) and การรับของ. Run after the master data (#217) on a database whose
/// documents are empty; matched to the suppliers, the materials and the แผนก by code (a code that is not found goes to the DQ report, and the line with it, because a line cannot stand without
/// them). The names the documents carry (DQ-04) are kept as they are. The ordered quantity of a line is what was received (not failed) plus what is still waiting; with nothing waiting it is the
/// quantity the first delivery found outstanding. Loaded through <see cref="PurchHistoryImport"/>, outside the change history (#39).
/// </summary>
public sealed class PurchDocumentMigration(AuditScope audit, PurchHistoryImport import, SupplierDirectory suppliers, MaterialDirectory materials, OrgDirectory org, PurchSettingsDirectory settings)
{
    private const string Other = "NF-06";
    private const string DuplicateKey = "DQ-18";
    private const string Orphan = "DQ-05";
    private const string StaleOrder = "DQ-06";
    private const string SendMismatch = "DQ-11";
    private const string MixedOrder = "DQ-19";
    private const string Department = "DQ-5";
    private const string Missing = "DQ-20";

    private record struct LineKey(string OrderNo, int Suffix);

    private sealed class Line
    {
        public required LineKey Key { get; init; }
        public LegacyRow? Waiting { get; init; }
        public List<LegacyRow> Receipts { get; } = [];
        public LegacyRow Head => Waiting ?? Receipts[0];
    }

    private LegacyDatabase legacy = null!;
    private CodeMap map = null!;
    private MigrationReport report = null!;

    public async Task RunAsync(LegacyDatabase legacyDatabase, CodeMap codeMap, MigrationReport into)
    {
        (legacy, map, report) = (legacyDatabase, codeMap, into);
        using (audit.SuppressRowChanges())
            await LoadAsync();
    }

    private async Task LoadAsync()
    {
        const string waitingTable = "PO_RECEI4", historyTable = "PO_RECEI5";
        var waitingRows = legacy[waitingTable];
        var historyRows = legacy[historyTable];
        var materialByCode = (await materials.GetAllAsync()).ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);
        var supplierByCode = (await suppliers.GetAllAsync()).ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        var departments = await org.GetDepartmentsAsync();
        var vatRate = (await settings.GetAsync()).VatRate;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var waitingTally = new Tally();
        var historyTally = new Tally();

        // ---- the lines: PO_NO + PO_SUF ties a waiting row to the deliveries made of it ----
        var lines = new Dictionary<LineKey, Line>();
        foreach (var (row, i) in waitingRows.Select((r, i) => (r, i)))
        {
            if (!TryKey(row, out var key)) { Reject(waitingTally, waitingTable, $"แถวที่ {i + 1}", Other, "ไม่มีเลขที่ใบสั่งซื้อ"); continue; }
            if (lines.ContainsKey(key)) { Reject(waitingTally, waitingTable, key.OrderNo, DuplicateKey, $"ใบสั่งซื้อ {key.OrderNo} ลำดับ {key.Suffix} ซ้ำในรายการค้างรับ — โหลดแถวแรก"); continue; }
            lines[key] = new Line { Key = key, Waiting = row };
            waitingTally.Loaded++; // counted again below if a line cannot be loaded
        }
        var received = new List<(LineKey Key, LegacyRow Row)>();
        foreach (var (row, i) in historyRows.Select((r, i) => (r, i)))
        {
            if (row.Date("RE_DATE") is null || row.Text("MTR_NAME") is null)
            {
                Reject(historyTally, historyTable, row.Text("PO_NO") ?? $"แถวที่ {i + 1}", Orphan, "แถวเศษ ไม่มีวันที่รับหรือชื่อวัตถุดิบ — ไม่ย้าย (ข้อเสนอ Q-12)");
                continue;
            }
            if (!TryKey(row, out var key)) { Reject(historyTally, historyTable, $"แถวที่ {i + 1}", Other, "ไม่มีเลขที่ใบสั่งซื้อ"); continue; }
            received.Add((key, row));
        }
        foreach (var group in received.GroupBy(r => r.Key))
        {
            var rows = group.Select(g => g.Row).OrderBy(r => r.Date("RE_DATE")).ToList();
            if (!lines.TryGetValue(group.Key, out var line)) lines[group.Key] = line = new Line { Key = group.Key };
            line.Receipts.AddRange(rows);
        }

        // ---- worked into documents ----
        var requestsByNo = new Dictionary<string, (LegacyRow Head, Dictionary<string, RequestLine> Lines)>(StringComparer.OrdinalIgnoreCase);
        var orderLines = new List<(string Head, OrderLineBuilt Built, Line Source)>();
        var failedLines = new HashSet<LineKey>();
        foreach (var line in lines.Values.OrderBy(l => l.Key.OrderNo, StringComparer.Ordinal).ThenBy(l => l.Key.Suffix))
        {
            var head = line.Head;
            var code = head.Text("MTR_CODE");
            var problem = code is null || !materialByCode.ContainsKey(code) ? $"รหัสวัตถุดิบ '{code}' ไม่อยู่ในทะเบียน"
                : head.Text("SUP_CODE") is not { } sc || !supplierByCode.ContainsKey(sc) ? $"รหัสผู้ขาย '{head.Text("SUP_CODE")}' ไม่อยู่ในทะเบียน"
                : head.Text("PR_NO") is null ? "ไม่มีเลขที่ใบขอซื้อ"
                : head.Date("PO_DATE") is null ? "ไม่มีวันที่สั่งซื้อ"
                : ResolveDepartment(departments, head.Text("EMP_ID")) is null ? $"ไม่พบแผนก '{head.Text("EMP_ID")}' ในทะเบียนแผนก — ใส่ {CodeKinds.Department},<ชื่อแผนก>,<รหัสแผนก> ในตารางจับคู่"
                : null;
            if (problem is not null)
            {
                failedLines.Add(line.Key);
                var rule = problem.Contains("แผนก") ? Department : Missing;
                if (line.Waiting is not null) { waitingTally.Loaded--; Reject(waitingTally, waitingTable, line.Key.OrderNo, rule, problem); }
                foreach (var _ in line.Receipts) Reject(historyTally, historyTable, line.Key.OrderNo, rule, problem);
                continue;
            }
            var material = materialByCode[code!];
            var good = line.Receipts.Where(r => !IsFailed(r)).ToList();
            var waiting = (decimal)(line.Waiting?.Number("PO_QTY") ?? 0);
            var ordered = line.Waiting is not null ? good.Sum(r => Qty(r, "RE_QTY")) + waiting : Qty(line.Receipts[0], "PO_QTY");
            var requested = new[] { line.Waiting }.Concat(line.Receipts).OfType<LegacyRow>().Select(r => r.Number("PR_QTY")).FirstOrDefault(q => q is > 0) is { } pr ? (decimal)pr : ordered;
            var price = Round4(head.Number("U_PRICE") ?? 0);
            var requestNo = head.Text("PR_NO")!;
            var departmentId = ResolveDepartment(departments, head.Text("EMP_ID"))!.Id;
            if (!requestsByNo.TryGetValue(requestNo, out var request)) requestsByNo[requestNo] = request = (head, []);
            var requestKey = KeyText(Int(head, "PR_SUF"), code!);
            if (!request.Lines.TryGetValue(requestKey, out var requestLine)) request.Lines[requestKey] = requestLine = new RequestLine(Int(head, "PR_SUF"), material, head.Text("MTR_NAME") ?? material.Name, head.Text("MTR_UNIT") ?? material.Unit, requested, price);
            else requestLine.Quantity = Math.Max(requestLine.Quantity, requested);

            var supplier = supplierByCode[head.Text("SUP_CODE")!];
            var receipts = line.Receipts.Select(r => new ImportedReceipt(
                DateOnly.FromDateTime(r.Date("RE_DATE")!.Value), Qty(r, "RE_QTY"), r.Text("INVOICE_NO"), r.Text("INV_SUF"), Round4(r.Number("U_PRICE") ?? 0), (decimal)(r.Number("NET_AMT") ?? 0),
                supplierByCode.TryGetValue(r.Text("SUP_CODE") ?? "", out var rs) ? rs.Id : supplier.Id, null,
                materialByCode.TryGetValue(r.Text("MTR_CODE") ?? "", out var rm) ? rm.Id : material.Id, r.Text("MTR_NAME") ?? material.Name, r.Text("MTR_UNIT") ?? material.Unit, IsFailed(r),
                r.Text("LOT"), r.Text("NOPASS") switch { "มี" => true, "ไม่มี" => false, _ => null }, r.Text("RESULT"))).ToList();
            foreach (var r in line.Receipts)
            {
                var sent = r.Text("SEND");
                var onTime = GoodsReceiptMath.OnTime(Date(head, "DELI_D"), DateOnly.FromDateTime(r.Date("RE_DATE")!.Value));
                if (sent is "ทัน" or "ไม่ทัน" && (sent == "ทัน") != onTime)
                    report.Add(historyTable, line.Key.OrderNo, SendMismatch, $"ลำดับ {line.Key.Suffix}: ผลส่งเดิม '{sent}' ไม่ตรงกับที่คำนวณจากวันที่ (ระบบใหม่คำนวณใหม่)", "คำนวณใหม่");
            }
            var net = (decimal)((double)price * (double)ordered);
            orderLines.Add((requestNo, new OrderLineBuilt(line.Key, requestNo, requestKey, material, head.Text("MTR_NAME") ?? material.Name, head.Text("MTR_UNIT") ?? material.Unit, requested, ordered, price, net,
                Round2(head.Number("TAX") ?? (double)net), Date(head, "DELI_D"), departmentId, receipts, head), line));
            if (line.Waiting is not null && DateOnly.FromDateTime(head.Date("PO_DATE")!.Value).AddYears(1) < today)
                report.Add(waitingTable, line.Key.OrderNo, StaleOrder, $"ใบสั่งซื้อค้างรับเกิน 1 ปี (ลำดับ {line.Key.Suffix}) — ย้ายตามเดิม รอคำตัดสินปิดหรือคงไว้ (ข้อเสนอ Q-12)", "โหลดตามเดิม");
            historyTally.Loaded += receipts.Count;
        }

        // ---- the documents ----
        var requests = requestsByNo.Select(r => new ImportedRequest(r.Key, DateOnly.FromDateTime(r.Value.Head.Date("PR_DATE") ?? r.Value.Head.Date("PO_DATE")!.Value),
            ResolveDepartment(departments, r.Value.Head.Text("EMP_ID"))!.Id, Date(r.Value.Head, "DELI_D"), r.Value.Head.Text("PUR"), r.Value.Head.Text("REMARK"), r.Value.Head.Text("MEM"),
            r.Value.Lines.Values.OrderBy(l => l.LineNo).Select(l => new ImportedRequestLine(l.Key, l.LineNo, l.Material.Id, l.Name, l.Unit, l.Quantity, l.Price, (decimal)((double)l.Price * (double)l.Quantity))).ToList()))
            .OrderBy(r => r.RequestNo, StringComparer.Ordinal).ToList();

        // an order number whose lines name more than one supplier or VAT is more than one order: the extra ones get a suffix and are told
        var orders = new List<ImportedOrder>();
        foreach (var byOrder in orderLines.GroupBy(o => o.Built.Key.OrderNo).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var groups = byOrder.GroupBy(o => (o.Built.Head.Text("SUP_CODE")?.ToUpperInvariant(), VatOf(o.Built.Head))).ToList();
            if (groups.Count > 1)
                report.Add(waitingTable, byOrder.Key, MixedOrder, $"ใบสั่งซื้อ {byOrder.Key} มีผู้ขายหรือ VAT ไม่ตรงกัน {groups.Count} แบบ — แยกเป็น {groups.Count} ใบ ({string.Join(", ", Enumerable.Range(1, groups.Count).Select(n => n == 1 ? byOrder.Key : $"{byOrder.Key}-{n}"))})", "แยกใบ");
            var n = 0;
            foreach (var group in groups)
            {
                n++;
                var head = group.First().Built.Head;
                var supplier = supplierByCode[head.Text("SUP_CODE")!];
                var chargesVat = VatOf(head);
                var built = group.Select(g => g.Built).OrderBy(l => l.Key.Suffix).ToList();
                var calc = PurchaseOrderMath.Calculate(built.Select(b => b.Net).ToList(), chargesVat, vatRate);
                var total = head.Number("TOTAL") is { } t ? Round2(t) : calc.Total;
                orders.Add(new ImportedOrder(n == 1 ? byOrder.Key : $"{byOrder.Key}-{n}", DateOnly.FromDateTime(head.Date("PO_DATE")!.Value), supplier.Id, head.Text("SUP_NAME") ?? supplier.Name,
                    head.Text("SUP_ADD1"), head.Text("SUP_ADD2"), head.Text("SUP_TEL"), head.Text("SUP_FAX"), Credit(head.Text("CREDIT_D")), chargesVat, head.Text("PUR"), head.Text("REMARK"), head.Text("MEM"),
                    calc.Subtotal, calc.Tax, total,
                    built.Select(b => new ImportedOrderLine(b.RequestNo, b.RequestKey, b.Key.Suffix, b.Material.Id, b.Name, b.Unit, b.Requested, b.Quantity, b.Price, b.Net, b.TaxIncluded, b.Desired,
                        b.DepartmentId, b.Receipts)).ToList()));
            }
        }
        await import.DeleteDocumentsAsync();
        await import.AddDocumentsAsync(requests, orders);

        var totals = await import.GetDocumentTotalsAsync();
        var expectedReceived = orders.SelectMany(o => o.Lines).SelectMany(l => l.Receipts).ToList();
        waitingTally.Report(report, waitingTable, "purch.PurchaseOrderLines (ค้างรับ)", waitingRows.Count, 0, totals.OrderLines - lines.Count(l => l.Value.Waiting is null && !failedLines.Contains(l.Key)));
        historyTally.Report(report, historyTable, "purch.GoodsReceipts", historyRows.Count, 0, totals.Receipts);
        report.MonthlyTotals.Add(new MonthlyTotal("เอกสาร", "ทั้งหมด", "Σ จำนวนสั่ง", (double)orders.SelectMany(o => o.Lines).Sum(l => l.Quantity), (double)orders.SelectMany(o => o.Lines).Sum(l => l.Quantity), (double)totals.OrderedQuantity));
        report.MonthlyTotals.Add(new MonthlyTotal("เอกสาร", "ทั้งหมด", "Σ จำนวนรับ", (double)expectedReceived.Sum(g => g.Quantity), (double)expectedReceived.Sum(g => g.Quantity), (double)totals.ReceivedQuantity));
        report.MonthlyTotals.Add(new MonthlyTotal("เอกสาร", "ทั้งหมด", "Σ จำนวนเงินที่รับ", (double)expectedReceived.Sum(g => g.NetAmount), (double)expectedReceived.Sum(g => g.NetAmount), (double)totals.ReceivedAmount));
        report.Notes.Add($"เอกสารจัดซื้อ: ใบขอซื้อ {totals.Requests} ใบ ({totals.RequestLines} รายการ), ใบสั่งซื้อ {totals.Orders} ใบ ({totals.OrderLines} รายการ), การรับของ {totals.Receipts} ครั้ง; รายการค้างรับ {lines.Values.Count(l => l.Waiting is not null && !failedLines.Contains(l.Key))}");
    }

    // ---------- helpers ----------

    private sealed class RequestLine(int lineNo, MaterialDto material, string name, string? unit, decimal quantity, decimal price)
    {
        public int LineNo { get; } = lineNo;
        public MaterialDto Material { get; } = material;
        public string Name { get; } = name;
        public string? Unit { get; } = unit;
        public decimal Quantity { get; set; } = quantity;
        public decimal Price { get; } = price;
        public string Key => KeyText(LineNo, Material.Code);
    }

    private sealed record OrderLineBuilt(
        LineKey Key, string RequestNo, string RequestKey, MaterialDto Material, string Name, string? Unit, decimal Requested, decimal Quantity, decimal Price, decimal Net, decimal TaxIncluded,
        DateOnly? Desired, int DepartmentId, IReadOnlyList<ImportedReceipt> Receipts, LegacyRow Head);

    private sealed class Tally
    {
        public int Rejected { get; set; }
        public int Loaded { get; set; }

        public void Report(MigrationReport report, string legacyTable, string newTable, int source, int excluded, int inDatabase)
            => report.Tables.Add(new TableCount(legacyTable, newTable, source, excluded, Rejected, Loaded, inDatabase));
    }

    private static string KeyText(int lineNo, string code) => $"{lineNo}|{code.ToUpperInvariant()}";

    private static bool TryKey(LegacyRow row, out LineKey key)
    {
        key = new LineKey(row.Text("PO_NO") ?? "", Int(row, "PO_SUF"));
        return key.OrderNo.Length > 0;
    }

    private static bool IsFailed(LegacyRow row) => row.Text("PASS") == "ไม่ผ่าน";

    private static decimal Qty(LegacyRow row, string column) => (decimal)(row.Number(column) ?? 0);

    private static int Int(LegacyRow row, string column) => (int)Math.Round(row.Number(column) ?? 0);

    private static DateOnly? Date(LegacyRow row, string column) => row.Date(column) is { } d && d.Year > 1900 ? DateOnly.FromDateTime(d) : null;

    private static decimal Round4(double value) => Math.Round((decimal)value, 4);

    private static decimal Round2(double value) => Math.Round((decimal)value, 2, MidpointRounding.ToEven);

    private static bool VatOf(LegacyRow row) => PurchaseOrderMath.ChargesVat(row.Text("VAT"));

    private static int? Credit(string? text) => text is null ? null : text == "เงินสด" ? 0 : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var days) ? days : null;

    private DepartmentDto? ResolveDepartment(IReadOnlyList<DepartmentDto> departments, string? name)
    {
        if (name is null) return null;
        var entry = map.Find(CodeKinds.Department, name);
        return entry?.Code is { } code
            ? departments.FirstOrDefault(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase))
            : departments.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));
    }

    private void Reject(Tally tally, string table, string key, string rule, string problem)
    {
        tally.Rejected++;
        report.Add(table, key, rule, problem, MigrationReport.NotLoaded);
    }
}

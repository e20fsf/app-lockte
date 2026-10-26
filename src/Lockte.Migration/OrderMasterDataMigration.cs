using System.Globalization;
using System.Text.RegularExpressions;
using Lockte.Hr;
using Lockte.Org;
using Lockte.Prod;
using Lockte.Sales;
using Lockte.SharedKernel;
using Lockte.SharedKernel.Persistence;

namespace Lockte.Migration;

/// <summary>
/// ย้ายข้อมูลหลักของ <c>ผลผลิตและORDER.accdb</c> (#107): เขตการขาย, ลูกค้า, สินค้า with its lists (ประเภท, ชนิดเบรก, รุ่น, กลุ่มสินค้า),
/// เครื่องจักร and แม่พิมพ์ — through each module's public registry, so a loaded row passes the same checks as one keyed on
/// screen, and nothing it does is written to the change history (#39). Run it <b>after</b> the employee program's migration
/// (#47): แผนก and the employee register are the ones that migration loaded, and ลูกค้า it loaded (งานรับจ้างผลิต) are matched
/// by code and updated, not created twice. It deletes and loads again สินค้า, its lists, แม่พิมพ์ and เครื่องจักร; ลูกค้า and
/// เขตการขาย are filled in place (a customer that is no longer in the file is left and reported), because the employee
/// program shares that register. A row or value it cannot take goes to the DQ report instead of stopping the run — to be
/// decided in the code map and run again (#17). พนักงานผลิต (T-EMPO) and พนักงานขาย get no register of their own: they are
/// matched to the HR register by code (<see cref="CodeKinds.ProductionEmployee"/>, <see cref="CodeKinds.SalesRepresentative"/>).
/// </summary>
public sealed partial class OrderMasterDataMigration(
    AuditScope audit,
    ProdDataReset prodReset,
    OrgMachineReset machineReset,
    SalesTerritoryRegistry territories,
    SalesTerritoryDirectory territoryDirectory,
    CustomerRegistry customers,
    CustomerDirectory customerDirectory,
    ProductRegistry products,
    ProductListRegistry lists,
    ProductDirectory productDirectory,
    MoldRegistry molds,
    MoldDirectory moldDirectory,
    OrgRegistry org,
    OrgDirectory orgDirectory,
    EmployeeDirectory employeeDirectory)
{
    private const string Other = "NF-06";
    private const string Duplicate = "DQ-2";
    private const string DepartmentByName = "DQ-5";
    private const string ZeroDate = "DQ-4";
    private const string Kept = "คงไว้ในฐานข้อมูลเหมือนเดิม";

    private LegacyDatabase legacy = null!;
    private CodeMap map = null!;
    private MigrationReport report = null!;
    private readonly List<Tally> tallies = [];

    private readonly Dictionary<string, int> territoryIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> customerIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> moldIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> typeIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> modelIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> brakeTypeIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int? Id, string? Problem)> departments = new(StringComparer.Ordinal);
    /// <summary>Values the products use that the lists did not have, added to the lists: counted off the lists' own tables.</summary>
    private readonly Dictionary<string, int> addedToLists = [];
    private int customersMatched;

    /// <summary>Once per instance (it is scoped): the run keeps what it loaded in fields.</summary>
    public async Task<MigrationReport> RunAsync(LegacyDatabase legacyDatabase, CodeMap codeMap)
    {
        if (report is not null) throw new InvalidOperationException("OrderMasterDataMigration runs once per scope.");
        (legacy, map, report) = (legacyDatabase, codeMap, new MigrationReport { Source = legacyDatabase.Source });
        using (audit.SuppressRowChanges())
        {
            await prodReset.DeleteAllAsync();
            await machineReset.DeleteAllAsync();
            var existingCustomers = (await customerDirectory.GetAllAsync()).ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
            await CheckDepartmentsAsync();
            await LoadTerritoriesAsync();
            await LoadCustomersAsync(existingCustomers);
            await LoadListsAsync();
            await LoadMoldsAsync();
            await LoadMachinesAsync();
            await LoadProductsAsync();
            await MatchProductionEmployeesAsync();
        }
        foreach (var (list, count) in report.ListAdditions.GroupBy(a => a.List).Select(g => (g.Key, g.Count())))
            report.Notes.Add($"รายการเลือก{list}: เพิ่ม {count} ค่าที่สินค้าใช้แต่ไม่อยู่ในตารางรายการเดิม (ฟอร์มเดิมเลือกจากค่าที่ใช้อยู่) — ดู {MigrationReport.ListAdditionsFile}");
        await CountBackAsync();
        return report;
    }

    // ---------- แผนก (T-แผนก): matched to the register, not loaded ----------

    private async Task CheckDepartmentsAsync()
    {
        const string table = "แผนก";
        var tally = Count(table, "org.Departments (จับคู่)", legacy[table].Count);
        foreach (var row in legacy[table])
        {
            var name = row.Text("DEP");
            if (name is null) { Reject(tally, table, "(DEP ว่าง)", Other, "ไม่มีชื่อแผนก"); continue; }
            var (id, problem) = await ResolveDepartmentAsync(name);
            if (id is null) Reject(tally, table, name, DepartmentByName, problem!);
            else tally.Excluded++; // by design: the แผนก register is the employee program's (#16)
        }
    }

    // ---------- เขตการขาย (T-เซล) ----------

    private async Task LoadTerritoriesAsync()
    {
        const string table = "เซล";
        var tally = Count(table, "sales.SalesTerritories", legacy[table].Count);
        var working = (await employeeDirectory.SearchAsync()).ToList();
        var byName = working.ToLookup(e => NameKey(e.Name));
        var candidates = new List<(string Code, string? Name)>();
        foreach (var row in legacy[table])
        {
            if (row.Text("ID") is not { } code) { Reject(tally, table, $"(ID ว่าง) {row.Text("NAME")}", Other, "ไม่มีรหัสเขตการขาย"); continue; }
            candidates.Add((code, row.Text("NAME")));
        }
        foreach (var group in candidates.GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase))
        {
            if (!OneValue(tally, table, group.Key, "รหัสเขตการขาย", group.ToList(), c => c.Name)) continue;
            var (code, name) = group.First();
            var (employeeId, note) = await SalesRepresentativeAsync(code, name, byName);
            var existing = await territoryDirectory.FindByCodeAsync(code);
            var saved = await territories.SaveAsync(new SalesTerritoryInput(existing?.Id, code, name ?? "", employeeId, existing?.RowVersion));
            if (!saved.Succeeded) { Reject(tally, table, code, Other, Errors(saved)); continue; }
            tally.Loaded++;
            territoryIds[code] = saved.Id!.Value;
            if (note is not null) report.Add(table, code, Other, note, MigrationReport.LoadedBlank);
        }
    }

    /// <summary>
    /// The พนักงานขาย of a territory: the code map's decision (<see cref="CodeKinds.SalesRepresentative"/>), else the one
    /// employee of the HR register with the same name; none → the territory is loaded without (and reported).
    /// </summary>
    private async Task<(int? EmployeeId, string? Note)> SalesRepresentativeAsync(string code, string? name, ILookup<string, EmployeeListItem> byName)
    {
        var entry = map.Find(CodeKinds.SalesRepresentative, code);
        if (entry?.Code == CodeKinds.Nobody)
        {
            UseCode(CodeKinds.SalesRepresentative, code, CodeKinds.Nobody, null, null, entry);
            report.EmployeeMatches.Add(new EmployeeMatch("sales", code, null, "ตัดสินแล้ว: ไม่มีพนักงานขาย"));
            return (null, null);
        }
        if (entry?.Code is { } mapped)
        {
            var employee = await employeeDirectory.FindByCodeAsync(mapped);
            UseCode(CodeKinds.SalesRepresentative, code, mapped, null, null, entry);
            report.EmployeeMatches.Add(new EmployeeMatch("sales", code, mapped, employee is null ? "ไม่พบรหัสในทะเบียน HR" : "จับคู่ตามตารางจับคู่"));
            return employee is null
                ? (null, $"รหัสพนักงานขาย '{mapped}' ในตารางจับคู่ไม่มีในทะเบียนพนักงาน HR")
                : (employee.Id, null);
        }
        var matches = name is null ? [] : byName[NameKey(name)].ToList();
        if (matches.Count > 1 && matches.Count(m => m.IsWorking) == 1) matches = matches.Where(m => m.IsWorking).ToList();
        if (matches.Count == 1)
        {
            UseCode(CodeKinds.SalesRepresentative, code, matches[0].Code, null, null, null);
            report.EmployeeMatches.Add(new EmployeeMatch("sales", code, matches[0].Code, "จับคู่ด้วยชื่อ"));
            return (matches[0].Id, null);
        }
        report.EmployeeMatches.Add(new EmployeeMatch("sales", code, null, matches.Count == 0 ? "ไม่พบในทะเบียน HR" : $"ชื่อตรงกับพนักงาน {matches.Count} คน"));
        return (null, (matches.Count == 0 ? "ชื่อพนักงานขายไม่พบในทะเบียนพนักงาน HR" : $"ชื่อพนักงานขายตรงกับพนักงาน HR {matches.Count} คน")
            + $" — ใส่ {CodeKinds.SalesRepresentative},{code},<รหัสพนักงาน หรือ {CodeKinds.Nobody}> ในตารางจับคู่");
    }

    // ---------- ลูกค้า (T-P_CUS) ----------

    private async Task LoadCustomersAsync(Dictionary<string, CustomerDto> existing)
    {
        const string table = "P_CUS";
        var tally = Count(table, "sales.Customers", legacy[table].Count);
        var candidates = new List<CustomerCandidate>();
        foreach (var row in legacy[table])
        {
            var legacyCode = row.Text("CODE");
            if (legacyCode is null) { Reject(tally, table, $"(CODE ว่าง) {row.Text("NAME")}", Other, "ไม่มีรหัสลูกค้า"); continue; }
            var entry = map.Find(CodeKinds.Customer, legacyCode);
            candidates.Add(new CustomerCandidate(legacyCode, (entry?.Code ?? legacyCode).ToUpperInvariant(), entry, row));
        }
        foreach (var group in candidates.GroupBy(c => c.Code))
        {
            var copies = group.ToList();
            var pick = ChooseCopy(tally, table, group.Key, "รหัสลูกค้า", copies, c => c.Row.Text("NAME"),
                c => string.Join("|", CustomerColumns.Select(c2 => c.Row.Text(c2))), copies[0].Entry?.Name,
                $"ใส่ {CodeKinds.Customer},{group.Key},,#<ลำดับแถวที่ถูก 1–{copies.Count}> (หรือชื่อแถวที่ถูก) ในตารางจับคู่ หรือแก้ในไฟล์");
            if (pick < 0) continue;
            var chosen = copies[pick];
            var row = chosen.Row;
            var name = NameOverride(chosen.Entry) ?? row.Text("NAME");
            UseCode(CodeKinds.Customer, chosen.Legacy, chosen.Code, name, null, chosen.Entry);

            var credit = CreditTerm.Parse(row.Text("CREDIT"));
            var problems = new List<string>();
            if (credit is null && row.Text("CREDIT") is { } creditText)
                problems.Add($"เครดิต '{creditText}' แปลงเป็นจำนวนวันไม่ได้");
            int? territoryId = null;
            if (row.Text("SALE_ID") is { } saleId)
            {
                if (territoryIds.TryGetValue(saleId, out var id)) territoryId = id;
                else problems.Add($"เขตการขาย (SALE_ID) '{saleId}' ไม่มีในทะเบียนเขตการขาย");
            }
            var matched = existing.GetValueOrDefault(chosen.Code);
            var saved = await customers.SaveAsync(new CustomerInput(matched?.Id, chosen.Code, name ?? "", matched?.RowVersion, credit,
                row.Text("ADDRESS"), row.Text("ADDRESS1"), row.Text("PROVINCES"), row.Text("P_CODE"), row.Text("ADD1"), row.Text("ADDDE"),
                row.Text("ADDDE1"), row.Text("TEL_1"), row.Text("TEL_2"), territoryId, row.Text("REMARK")));
            if (!saved.Succeeded) { Reject(tally, table, chosen.Legacy, Other, Errors(saved)); continue; }
            tally.Loaded++;
            customerIds[chosen.Code] = saved.Id!.Value;
            if (matched is not null) customersMatched++;
            foreach (var problem in problems) report.Add(table, chosen.Legacy, Other, problem, MigrationReport.LoadedBlank);
        }
        if (customersMatched > 0)
            report.Notes.Add($"ลูกค้า {customersMatched} รหัสมีในทะเบียนอยู่แล้ว (โปรแกรมพนักงานย้ายมา) — จับคู่ด้วยรหัสเดิมและอัปเดตค่า ไม่สร้างซ้ำ");
        foreach (var stale in existing.Values.Where(c => !customerIds.ContainsKey(c.Code)))
            report.Add(table, stale.Code, Other, "ลูกค้าในทะเบียนที่ไม่มีใน P_CUS ของไฟล์นี้ (ย้ายมาจากโปรแกรมพนักงาน?)", Kept);
    }

    private static readonly string[] CustomerColumns =
        ["NAME", "CREDIT", "ADDRESS", "ADDRESS1", "PROVINCES", "P_CODE", "ADD1", "ADDDE", "ADDDE1", "TEL_1", "TEL_2", "SALE_ID", "REMARK"];

    private sealed record CustomerCandidate(string Legacy, string Code, CodeMapEntry? Entry, LegacyRow Row);

    // ---------- รายการเลือกของสินค้า ----------

    private async Task LoadListsAsync()
    {
        await LoadListAsync("ประเภท", "prod.ProductTypes", "MEM", typeIds, name => lists.SaveProductTypeAsync(null, name, null));
        await LoadListAsync("MODEL", "prod.ProductModels", "MODEL", modelIds, name => lists.SaveModelAsync(null, name, null));

        const string brake = "TYPE";
        var tally = Count(brake, "prod.BrakeTypes", legacy[brake].Count);
        var candidates = new List<(string Code, string? Name)>();
        foreach (var row in legacy[brake])
        {
            if (row.Text("TYPE") is not { } code) { Reject(tally, brake, $"(TYPE ว่าง) {row.Text("NANE")}", Other, "ไม่มีรหัสชนิดเบรก"); continue; }
            candidates.Add((code, row.Text("NANE")));
        }
        foreach (var group in candidates.GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase))
        {
            if (!OneValue(tally, brake, group.Key, "รหัสชนิดเบรก", group.ToList(), c => c.Name)) continue;
            var (code, name) = group.First();
            var saved = await lists.SaveBrakeTypeAsync(null, code, name ?? "", null);
            if (saved.Succeeded) { tally.Loaded++; brakeTypeIds[code] = saved.Id!.Value; }
            else Reject(tally, brake, code, Other, Errors(saved));
        }

        const string groups = "รหัสสินค้าย่อ";
        tally = Count(groups, "prod.ProductGroups", legacy[groups].Count);
        var groupCandidates = new List<(string Pattern, string? Name)>();
        foreach (var row in legacy[groups])
        {
            if (row.Text("C") is not { } pattern) { Reject(tally, groups, $"(C ว่าง) {row.Text("N")}", Other, "ไม่มีรหัสกลุ่ม"); continue; }
            groupCandidates.Add((pattern, row.Text("N")));
        }
        foreach (var group in groupCandidates.GroupBy(c => c.Pattern, StringComparer.OrdinalIgnoreCase))
        {
            if (!OneValue(tally, groups, group.Key, "รหัสกลุ่มสินค้า", group.ToList(), c => c.Name)) continue;
            var (pattern, name) = group.First();
            var saved = await lists.SaveGroupAsync(null, pattern, name ?? "", null);
            if (saved.Succeeded) tally.Loaded++;
            else Reject(tally, groups, pattern, Other, Errors(saved));
        }
    }

    private async Task LoadListAsync(string table, string newTable, string column, Dictionary<string, int> ids, Func<string, Task<ServiceResult>> save)
    {
        var tally = Count(table, newTable, legacy[table].Count);
        var candidates = new List<string>();
        foreach (var row in legacy[table])
        {
            if (row.Text(column) is not { } name) { Reject(tally, table, $"({column} ว่าง)", Other, "ไม่มีค่า"); continue; }
            candidates.Add(name);
        }
        foreach (var group in candidates.GroupBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            if (!OneValue(tally, table, group.Key, column, group.ToList(), n => n)) continue;
            var saved = await save(group.First());
            if (saved.Succeeded) { tally.Loaded++; ids[group.First()] = saved.Id!.Value; }
            else Reject(tally, table, group.Key, Other, Errors(saved));
        }
    }

    // ---------- แม่พิมพ์ (T-รหัสพิมพ์) ----------

    private async Task LoadMoldsAsync()
    {
        const string table = "รหัสพิมพ์";
        var tally = Count(table, "prod.Molds", legacy[table].Count);
        var candidates = new List<(string Code, LegacyRow Row)>();
        foreach (var row in legacy[table])
        {
            if (row.Text("ID") is not { } code) { Reject(tally, table, $"(ID ว่าง) {row.Text("NAME")}", Other, "ไม่มีรหัสแม่พิมพ์"); continue; }
            candidates.Add((code, row));
        }
        foreach (var group in candidates.GroupBy(c => c.Code.ToUpperInvariant()))
        {
            var copies = group.ToList();
            var pick = ChooseCopy(tally, table, group.Key, "รหัสแม่พิมพ์", copies, c => c.Row.Text("NAME"),
                c => string.Join("|", MoldColumns.Select(c2 => c.Row.Text(c2))), null);
            if (pick < 0) continue;
            var chosen = copies[pick];
            var row = chosen.Row;
            var (departmentId, problem) = await ResolveDepartmentAsync(row.Text("DEP"));
            var start = CleanDate(row, "DATE", table, chosen.Code);
            var end = CleanDate(row, "OUT_DATE", table, chosen.Code);
            var saved = await molds.SaveAsync(new MoldInput(null, chosen.Code, row.Text("NAME") ?? "", null,
                Int(row, "QTYS"), Int(row, "SHOT"), Int(row, "PCS"), Int(row, "YEAR"), departmentId, row.Text("MARKER"),
                start, end, (decimal)(row.Number("COST") ?? 0), row.Text("OWNER"), Decimal(row, "AL WEIGHT")));
            if (!saved.Succeeded) { Reject(tally, table, chosen.Code, Other, Errors(saved)); continue; }
            tally.Loaded++;
            moldIds[chosen.Code] = saved.Id!.Value;
            if (problem is not null) report.Add(table, chosen.Code, DepartmentByName, problem, MigrationReport.LoadedBlank);
        }
    }

    private static readonly string[] MoldColumns =
        ["NAME", "QTYS", "OWNER", "SHOT", "PCS", "YEAR", "DEP", "MARKER", "DATE", "OUT_DATE", "COST", "AL WEIGHT"];

    // ---------- เครื่องจักร (T-MC_N) ----------

    private async Task LoadMachinesAsync()
    {
        const string table = "MC_N";
        var tally = Count(table, "org.Machines", legacy[table].Count);
        var candidates = new List<(string Code, LegacyRow Row)>();
        foreach (var row in legacy[table])
        {
            if (row.Text("CODE") is not { } code) { Reject(tally, table, $"(CODE ว่าง) {row.Text("NAME")}", Other, "ไม่มีรหัสเครื่องจักร"); continue; }
            candidates.Add((code, row));
        }
        foreach (var group in candidates.GroupBy(c => c.Code.ToUpperInvariant()))
        {
            var copies = group.ToList();
            var pick = ChooseCopy(tally, table, group.Key, "รหัสเครื่องจักร", copies, c => c.Row.Text("NAME"),
                c => $"{c.Row.Text("NAME")}|{c.Row.Text("DEP")}", null);
            if (pick < 0) continue;
            var chosen = copies[pick];
            var (departmentId, problem) = await ResolveDepartmentAsync(chosen.Row.Text("DEP"));
            if (departmentId is null)
            {
                Reject(tally, table, chosen.Code, DepartmentByName, problem ?? "เครื่องจักรไม่มีแผนก");
                continue;
            }
            var saved = await org.SaveMachineAsync(new MachineInput(null, chosen.Code, chosen.Row.Text("NAME") ?? "", departmentId, null));
            if (saved.Succeeded) tally.Loaded++;
            else Reject(tally, table, chosen.Code, Other, Errors(saved));
        }
    }

    // ---------- สินค้า (T-P_NAME) ----------

    private async Task LoadProductsAsync()
    {
        const string table = "P_NAME";
        var tally = Count(table, "prod.Products", legacy[table].Count);
        var candidates = new List<(string Code, LegacyRow Row)>();
        foreach (var row in legacy[table])
        {
            if (row.Text("PD_C") is not { } code) { Reject(tally, table, $"(PD_C ว่าง) {row.Text("PD_N")}", Other, "ไม่มีรหัสสินค้า"); continue; }
            candidates.Add((code, row));
        }
        foreach (var group in candidates.GroupBy(c => c.Code.ToUpperInvariant()))
        {
            var copies = group.ToList();
            var pick = ChooseCopy(tally, table, group.Key, "รหัสสินค้า", copies, c => c.Row.Text("PD_N"),
                c => string.Join("|", ProductColumns.Select(c2 => c.Row.Text(c2))), null);
            if (pick < 0) continue;
            var chosen = copies[pick];
            var row = chosen.Row;
            var problems = new List<string>();

            var typeId = await ListValueAsync(row.Text("MEM"), typeIds, "ประเภท", "prod.ProductTypes", name => lists.SaveProductTypeAsync(null, name, null), table, chosen.Code);
            var modelId = await ListValueAsync(row.Text("MODEL"), modelIds, "รุ่น", "prod.ProductModels", name => lists.SaveModelAsync(null, name, null), table, chosen.Code);
            int? brakeId = null;
            if (row.Text("TYPE") is { } brake)
            {
                if (brakeTypeIds.TryGetValue(brake, out var id)) brakeId = id;
                else problems.Add($"ชนิดเบรก (TYPE) '{brake}' ไม่มีในรายการชนิดเบรก");
            }
            int? moldId = null;
            if (row.Text("MOULD") is { } mold)
            {
                if (moldIds.TryGetValue(mold, out var id)) moldId = id;
                else problems.Add($"แม่พิมพ์ (MOULD) '{mold}' ไม่มีในทะเบียนแม่พิมพ์");
            }
            int? customerId = null;
            if (row.Text("CUS_ID") is { } customer)
            {
                var key = (map.Find(CodeKinds.Customer, customer)?.Code ?? customer).ToUpperInvariant();
                if (customerIds.TryGetValue(key, out var id)) customerId = id;
                else problems.Add($"ลูกค้าเจ้าของ (CUS_ID) '{customer}' ไม่มีในทะเบียนลูกค้า");
            }

            var saved = await products.SaveAsync(new ProductInput(null, chosen.Code, row.Text("PD_N") ?? "", null, row.Text("U_NAME"), row.Text("U_PACK"),
                Decimal(row, "WEIGHT"), Decimal(row, "WEIGHT1"), Decimal(row, "WEIGHT2"), Decimal(row, "WEIGHT3"), Decimal(row, "WEIGHT4"),
                Decimal(row, "U_PRICE"), Decimal(row, "FML"), typeId, moldId, customerId, CleanDate(row, "YEAR", table, chosen.Code), modelId,
                row.Text("BARCODE"), row.Text("มอก"), brakeId, row.Text("SIZE"), row.Text("FORMULA"), row.Text("MODEL1")));
            if (!saved.Succeeded) { Reject(tally, table, chosen.Code, Other, Errors(saved)); continue; }
            tally.Loaded++;
            foreach (var problem in problems) report.Add(table, chosen.Code, Other, problem, MigrationReport.LoadedBlank);
        }
    }

    private static readonly string[] ProductColumns =
        ["PD_N", "U_NAME", "U_PACK", "WEIGHT", "WEIGHT1", "WEIGHT2", "WEIGHT3", "WEIGHT4", "U_PRICE", "FML", "MEM", "MOULD", "CUS_ID", "YEAR",
         "MODEL", "BARCODE", "มอก", "TYPE", "SIZE", "FORMULA", "MODEL1"];

    /// <summary>
    /// The id of a value of a list a product chooses from. The legacy combos listed the values in use (SELECT DISTINCT), so a
    /// value the list table lacks is added to the list — one possible answer, noted in the DQ report.
    /// </summary>
    private async Task<int?> ListValueAsync(string? value, Dictionary<string, int> ids, string what, string newTable,
        Func<string, Task<ServiceResult>> save, string table, string key)
    {
        if (value is null) return null;
        if (ids.TryGetValue(value, out var id)) return id;
        var saved = await save(value);
        if (!saved.Succeeded)
        {
            report.Add(table, key, Other, $"{what} '{value}' เพิ่มเข้ารายการเลือกไม่ได้: {Errors(saved)}", MigrationReport.LoadedBlank);
            return null;
        }
        ids[value] = saved.Id!.Value;
        addedToLists[newTable] = addedToLists.GetValueOrDefault(newTable) + 1;
        report.ListAdditions.Add((what, value));
        return saved.Id;
    }

    // ---------- พนักงานผลิต (T-EMPO) ----------

    private async Task MatchProductionEmployeesAsync()
    {
        const string table = "EMPO";
        var tally = Count(table, "hr.Employees (จับคู่)", legacy[table].Count);
        var candidates = new List<(string Code, string? Name)>();
        foreach (var row in legacy[table])
        {
            if (row.Text("ID") is not { } code) { Reject(tally, table, $"(ID ว่าง) {row.Text("NAME")}", Other, "ไม่มีรหัสพนักงาน"); continue; }
            candidates.Add((code, row.Text("NAME")));
        }
        foreach (var group in candidates.GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase))
        {
            var entry = map.Find(CodeKinds.ProductionEmployee, group.Key);
            var rows = group.ToList();
            if (rows.Count > 1)
            {
                // The same ID twice (B179, Q-08): kept row = the one whose name the map names; else all wait for the decision.
                var keep = entry?.Name is { } wanted
                    ? (Pick(wanted, rows.Count) is { } index ? new List<(string Code, string? Name)> { rows[index] } : rows.Where(r => string.Equals(r.Name, wanted, StringComparison.OrdinalIgnoreCase)).ToList())
                    : [];
                if (rows.Select(r => r.Name).Distinct().Count() == 1) keep = rows.Take(1).ToList();
                if (keep.Count != 1)
                {
                    foreach (var _ in rows)
                        Reject(tally, table, group.Key, Duplicate, $"รหัสพนักงานซ้ำ {rows.Count} แถว ชื่อไม่ตรงกัน — ใส่ {CodeKinds.ProductionEmployee},{group.Key},<รหัสพนักงาน>,#<ลำดับแถวที่ถูก 1–{rows.Count}> ในตารางจับคู่");
                    report.EmployeeMatches.Add(new EmployeeMatch("production", group.Key, null, "รหัสซ้ำ รอตัดสิน"));
                    continue;
                }
                foreach (var _ in rows.Skip(1)) Reject(tally, table, group.Key, Duplicate, $"รหัสพนักงานซ้ำ {rows.Count} แถว — ใช้แถวที่เลือกแล้ว");
            }
            var hrCode = (entry?.Code ?? group.Key).ToUpperInvariant();
            UseCode(CodeKinds.ProductionEmployee, group.Key, entry?.Code == CodeKinds.Nobody ? CodeKinds.Nobody : hrCode, null, null, entry);
            if (entry?.Code == CodeKinds.Nobody)
            {
                tally.Excluded++;
                report.EmployeeMatches.Add(new EmployeeMatch("production", group.Key, null, "ตัดสินแล้ว: ไม่จับคู่"));
            }
            else if (await employeeDirectory.FindByCodeAsync(hrCode) is null)
            {
                Reject(tally, table, group.Key, Other,
                    $"ไม่มีรหัส '{hrCode}' ในทะเบียนพนักงาน HR — ใส่ {CodeKinds.ProductionEmployee},{group.Key},<รหัสพนักงาน หรือ {CodeKinds.Nobody}> ในตารางจับคู่");
                report.EmployeeMatches.Add(new EmployeeMatch("production", group.Key, null, "ไม่พบในทะเบียน HR"));
            }
            else
            {
                tally.Excluded++; // by design: no register of its own (#16) — matched to the HR register
                report.EmployeeMatches.Add(new EmployeeMatch("production", group.Key, hrCode, entry is null ? "จับคู่ด้วยรหัสเดียวกัน" : "จับคู่ตามตารางจับคู่"));
            }
        }
    }

    // ---------- รายงานตรวจยอด ----------

    private async Task CountBackAsync()
    {
        var inDatabase = new Dictionary<string, int>
        {
            ["sales.SalesTerritories"] = (await territoryDirectory.GetAllAsync()).Count(t => territoryIds.ContainsKey(t.Code)),
            ["sales.Customers"] = (await customerDirectory.GetAllAsync()).Count(c => customerIds.ContainsKey(c.Code)),
            ["prod.Products"] = (await productDirectory.SearchAsync(null, null, int.MaxValue)).Count,
            ["prod.ProductTypes"] = (await productDirectory.GetProductTypesAsync()).Count - addedToLists.GetValueOrDefault("prod.ProductTypes"),
            ["prod.BrakeTypes"] = (await productDirectory.GetBrakeTypesAsync()).Count,
            ["prod.ProductModels"] = (await productDirectory.GetModelsAsync()).Count - addedToLists.GetValueOrDefault("prod.ProductModels"),
            ["prod.ProductGroups"] = (await productDirectory.GetGroupsAsync()).Count,
            ["prod.Molds"] = (await moldDirectory.GetAllAsync()).Count,
            ["org.Machines"] = (await orgDirectory.GetMachinesAsync()).Count,
            ["org.Departments (จับคู่)"] = 0,
            ["hr.Employees (จับคู่)"] = 0,
        };
        report.Tables.AddRange(tallies.Select(t =>
            new TableCount(t.LegacyTable, t.NewTable, t.Source, t.Excluded, t.Rejected, t.Loaded, inDatabase[t.NewTable])));
    }

    // ---------- helpers ----------

    /// <summary>
    /// A department of the register by the legacy name — the code map's decision (<see cref="CodeKinds.Department"/>) or the
    /// name itself (DQ-5). Returns the problem text when there is none.
    /// </summary>
    private async Task<(int? Id, string? Problem)> ResolveDepartmentAsync(string? legacyName)
    {
        if (legacyName is null) return (null, null);
        if (departments.TryGetValue(legacyName, out var known)) return known;
        var entry = map.Find(CodeKinds.Department, legacyName);
        var department = entry?.Code is { } code ? await orgDirectory.FindDepartmentByCodeAsync(code) : await orgDirectory.FindDepartmentByNameAsync(legacyName);
        var result = department is null
            ? ((int?)null, $"แผนก '{legacyName}' ไม่มีในทะเบียนแผนก — ใส่ {CodeKinds.Department},{legacyName},<รหัสแผนก> ในตารางจับคู่")
            : (department.Id, (string?)null);
        if (department is not null) UseCode(CodeKinds.Department, legacyName, department.Code, department.Name, department.EmployeeIdPrefix, entry);
        departments[legacyName] = result;
        return result;
    }

    /// <summary>
    /// Rows of one key: identical copies load once (the others go to the report), copies that differ are all left out (DQ-2)
    /// unless the code map names the right one by its NAME (<paramref name="decision"/>). Returns the index of the row to
    /// load, or -1 when there is none.
    /// </summary>
    private int ChooseCopy<T>(Tally tally, string table, string key, string what, List<T> rows, Func<T, string?> name, Func<T, string> signature,
        string? decision, string? hint = null)
    {
        if (rows.Count == 1) return 0;
        var same = rows.Select(signature).Distinct().Count() == 1;
        var chosen = -1;
        if (same) chosen = 0;
        else if (decision is not null)
            chosen = Pick(decision, rows.Count) is { } index ? index
                : rows.FindIndex(r => string.Equals(name(r), decision, StringComparison.OrdinalIgnoreCase));
        for (var i = 0; i < rows.Count; i++)
        {
            if (i == chosen) continue;
            Reject(tally, table, key, Duplicate, same ? $"{what}ซ้ำ {rows.Count} แถว ค่าเหมือนกัน — โหลดแถวเดียว"
                : chosen >= 0 ? $"{what}ซ้ำ {rows.Count} แถว — โหลดแถวที่ตารางจับคู่เลือก"
                : $"{what}ซ้ำ {rows.Count} แถว ค่าไม่ตรงกัน — {hint ?? "แก้ในไฟล์ต้นทาง"}");
        }
        return chosen;
    }

    /// <summary>
    /// <c>#2</c> in the code map's name column = the second of the rows with the same key, in the file's order — a decision that
    /// holds no name, so a map kept in the repo stays free of names. Null when <paramref name="decision"/> is not of that form
    /// or the number is out of range.
    /// </summary>
    private static int? Pick(string decision, int count)
        => decision.StartsWith('#') && int.TryParse(decision.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= count
            ? n - 1 : null;

    private static bool IsPick(string? decision) => decision is not null && decision.StartsWith('#');

    /// <summary>The name a code-map line gives a customer (not a <c>#n</c> pick).</summary>
    private static string? NameOverride(CodeMapEntry? entry) => IsPick(entry?.Name) ? null : entry?.Name;

    /// <summary>The same rule when the rows are just values (<see cref="ChooseCopy"/> would need a row type).</summary>
    private bool OneValue<T, TValue>(Tally tally, string table, string key, string what, List<T> rows, Func<T, TValue> value)
    {
        if (rows.Count == 1) return true;
        var same = rows.Select(value).Distinct().Count() == 1;
        foreach (var _ in rows.Skip(same ? 1 : 0))
            Reject(tally, table, key, Duplicate, same ? $"{what}ซ้ำ {rows.Count} แถว ค่าเหมือนกัน — โหลดแถวเดียว"
                : $"{what}ซ้ำ {rows.Count} แถว ค่าไม่ตรงกัน");
        return same;
    }

    /// <summary>A legacy date; the Access zero date (1899) is no date (DQ-4); a year no one could mean is left out and reported.</summary>
    private DateOnly? CleanDate(LegacyRow row, string column, string table, string key)
    {
        if (row.Date(column) is not { } date) return null;
        if (date.Year < 1900) return null; // OD_DATE = 1899-12-30: Access's zero
        if (date.Year is < 1990 or > 2100)
        {
            report.Add(table, key, ZeroDate, $"{column} = {date:yyyy-MM-dd} เป็นปีที่ไม่น่าเป็นไปได้", MigrationReport.LoadedBlank);
            return null;
        }
        return DateOnly.FromDateTime(date);
    }

    private static int? Int(LegacyRow row, string column) => row.Number(column) is { } n ? (int)Math.Round(n) : null;

    private static decimal? Decimal(LegacyRow row, string column) => row.Number(column) is { } n ? Math.Round((decimal)n, 4) : null;

    private Tally Count(string legacyTable, string newTable, int source)
    {
        var tally = new Tally(legacyTable, newTable) { Source = source };
        tallies.Add(tally);
        return tally;
    }

    private void Reject(Tally tally, string table, string key, string rule, string problem)
    {
        tally.Rejected++;
        report.Add(table, key, rule, problem, MigrationReport.NotLoaded);
    }

    private void UseCode(string kind, string legacyValue, string code, string? name, string? prefix, CodeMapEntry? entry)
    {
        if (report.CodeMapUsed.Any(u => u.Kind == kind && string.Equals(u.Legacy.Trim(), legacyValue.Trim(), StringComparison.OrdinalIgnoreCase))) return;
        report.CodeMapUsed.Add(new CodeMapUse(kind, legacyValue, code, name, prefix, entry is not null));
    }

    private static string Errors(ServiceResult result) => string.Join(" · ", result.Errors);

    /// <summary>A Thai name without its title and with the spaces collapsed, to compare T-เซล.NAME with the HR register.</summary>
    private static string NameKey(string name)
    {
        var collapsed = Spaces().Replace(name.Trim(), " ");
        foreach (var title in (string[])["นางสาว", "น.ส.", "นาง", "นาย", "Miss", "Mrs.", "Mr."])
            if (collapsed.StartsWith(title, StringComparison.Ordinal)) return collapsed[title.Length..].TrimStart();
        return collapsed;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    private sealed class Tally(string legacyTable, string newTable)
    {
        public string LegacyTable { get; } = legacyTable;
        public string NewTable { get; } = newTable;
        public int Source { get; init; }
        public int Excluded { get; set; }
        public int Rejected { get; set; }
        public int Loaded { get; set; }
    }
}

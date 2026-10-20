using System.Data.OleDb;
using System.Runtime.Versioning;

namespace Lockte.Web.Tests;

/// <summary>
/// A made-up <c>พนักงาน.accdb</c> with the master-data and history tables and their legacy column types (data-model.md), written
/// through ADOX + the ACE provider of this PC. Every name, ID and number in it is fictional — the repo is public.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class FakeLegacyFile
{
    private static readonly Dictionary<string, (string Column, string Type)[]> Schema = new()
    {
        ["EMPLO"] =
        [
            ("ID", "TEXT(4)"), ("NAME", "TEXT(255)"), ("SEX", "TEXT(3)"), ("BORN", "DATETIME"), ("ID_CODE", "TEXT(15)"),
            ("PER_ADD", "TEXT(255)"), ("ID_PLACE", "TEXT(255)"), ("ID_DATE", "DATETIME"), ("ADDRESS", "TEXT(255)"),
            ("START", "DATETIME"), ("NO", "TEXT(4)"), ("POINT", "TEXT(12)"), ("DEPARTMENT", "TEXT(50)"),
            ("POSITION", "TEXT(18)"), ("CLAS", "TEXT(3)"), ("SALARY", "DOUBLE"), ("SA_DAY", "DOUBLE"), ("ID_S", "TEXT(15)"),
            ("ACC", "TEXT(10)"), ("SHIFT", "TEXT(3)"), ("HOT1", "TEXT(1)"), ("RESIGN", "DATETIME"), ("SECTION", "TEXT(50)"),
            ("HOSPITAL", "TEXT(255)"), ("WORK", "TEXT(255)"), ("DATE_1", "DATETIME"), ("DATE_2", "DATETIME"),
            ("POINT_1", "LONG"), ("POINT_2", "LONG"), ("DATE_3", "DATETIME"), ("GRED_1", "TEXT(50)"), ("GRED_2", "TEXT(50)"),
            ("MEM", "TEXT(255)"), ("NATION", "TEXT(50)"), ("ACC2", "TEXT(255)"), ("NAME TITLE", "TEXT(255)"),
            ("NAME ENGLISH", "TEXT(255)"), ("TELEPHONE", "TEXT(255)"),
        ],
        ["แผนก"] =
        [
            ("SUF", "LONG"), ("DEPART", "TEXT(50)"), ("CODE", "TEXT(50)"), ("SECTION", "TEXT(255)"), ("SUPER", "TEXT(50)"),
            ("MANAGER", "TEXT(50)"), ("SHIFT", "TEXT(50)"), ("FACTORY MANAGER", "TEXT(255)"),
        ],
        ["แผนก2"] = [("SUF", "LONG"), ("DEPART", "TEXT(50)"), ("QTY", "LONG")],
        ["ฝ่าย"] = [("TYPE", "TEXT(255)")],
        ["SHIFT"] =
        [
            ("SHIFT", "TEXT(3)"), ("W_IN", "DATETIME"), ("W_OUT", "DATETIME"), ("STOP", "DATETIME"), ("START", "DATETIME"),
            ("O_IN", "DATETIME"), ("O_OUT", "DATETIME"), ("REST_T", "LONG"),
        ],
        ["ชื่อลูกค้า"] = [("CODE", "TEXT(50)"), ("NAME", "TEXT(255)")],
        ["SUNDAY"] = [("DATE", "DATETIME"), ("ITEM", "TEXT(10)")],
        ["HOLIDAY"] = [("DATE", "DATETIME"), ("ITEM", "TEXT(46)")],
        ["E_WORK"] = WorkTimeColumns("DATETIME"),
        ["ADD_EW"] = WorkTimeColumns("TEXT(2)"), // DQ-7: LATE is Text(2) in the history
        ["LEAVE"] = LeaveColumns,
        ["LEAVE_ADD"] = LeaveColumns,
        ["ตั้งวันที่ตัดWEEK"] =
            [("FROM_DA", "DATETIME"), ("TO_DA", "DATETIME"), ("PAY_D", "DATETIME"), ("CUS", "TEXT(50)"), ("NAMES", "TEXT(255)"), ("DAY", "LONG")],
        ["NO_WORK"] =
        [
            ("DATE1", "DATETIME"), ("ID1", "TEXT(4)"), ("W_IN", "DATETIME"), ("W_OUT", "DATETIME"), ("WORK_T", "DOUBLE"), ("TYPE", "TEXT(2)"),
            ("LATE", "TEXT(2)"), ("REMARK", "TEXT(255)"),
        ],
        ["ใบเตือน"] = WarningColumns,
        ["ใบเตือนADD"] = WarningColumns,
        ["ค่าแรง"] =
        [
            ("ID", "TEXT(4)"), ("NAME", "TEXT(50)"), ("SA", "DOUBLE"), ("W", "DOUBLE"), ("TT_OT", "DOUBLE"), ("PAY_W", "LONG"),
            ("PAY_OT", "LONG"), ("FROM_DATE", "DATETIME"), ("TO_DATE", "DATETIME"), ("SUNDAY", "DOUBLE"), ("HOLIDAY", "DOUBLE"),
            ("PAY_D", "DATETIME"), ("HOT", "DOUBLE"), ("L", "DOUBLE"), ("OT", "DOUBLE"),
        ],
        ["สำเนาของ ค่าแรง"] =
        [
            ("ID", "TEXT(4)"), ("NAME", "TEXT(50)"), ("W", "LONG"), ("TT_OT", "LONG"), ("PAY_W", "LONG"), ("PAY_OT", "LONG"),
            ("FROM_DATE", "DATETIME"), ("TO_DATE", "DATETIME"), ("SUNDAY", "LONG"), ("HOLIDAY", "LONG"), ("PAY_D", "DATETIME"),
            ("MARK", "TEXT(1)"), ("HOT", "DOUBLE"), ("OT", "LONG"),
        ],
    };

    private static (string, string)[] WarningColumns =>
    [
        ("DATE", "DATETIME"), ("ID", "TEXT(50)"), ("NAME", "TEXT(255)"), ("DEPART", "TEXT(255)"), ("CODE", "TEXT(50)"),
        ("CODE_NAME", "TEXT(255)"), ("REMARK", "TEXT(255)"),
    ];

    private static (string, string)[] WorkTimeColumns(string lateType) =>
    [
        ("DATE", "DATETIME"), ("ID", "TEXT(4)"), ("W_IN", "DATETIME"), ("W_OUT", "DATETIME"), ("WORK_T", "DOUBLE"), ("TYPE", "TEXT(2)"),
        ("LATE", lateType), ("REST_T", "LONG"), ("MARK", "TEXT(2)"), ("CODE", "TEXT(50)"),
    ];

    private static (string, string)[] LeaveColumns =>
    [
        ("DATE", "DATETIME"), ("ID", "TEXT(4)"), ("FROM_D", "DATETIME"), ("TO_D", "DATETIME"), ("FROM_T", "DATETIME"), ("TO_T", "DATETIME"),
        ("SUM_D", "SINGLE"), ("SR", "TEXT(50)"), ("SAL", "TEXT(2)"), ("REMARK", "TEXT(28)"), ("MARK", "TEXT(50)"), ("SALA_DAY", "SINGLE"),
        ("PAY", "SINGLE"), ("CODE_A", "TEXT(50)"),
    ];

    public Dictionary<string, List<Dictionary<string, object?>>> Tables { get; } =
        Schema.Keys.ToDictionary(t => t, _ => new List<Dictionary<string, object?>>());

    public FakeLegacyFile Row(string table, params (string Column, object? Value)[] values)
    {
        Tables[table].Add(values.ToDictionary(v => v.Column, v => v.Value));
        return this;
    }

    /// <summary>A legacy time of day: 1899-12-30 + time, the next day = 1899-12-31.</summary>
    public static DateTime Time(int hour, int minute, bool nextDay = false) => new DateTime(1899, 12, nextDay ? 31 : 30).AddHours(hour).AddMinutes(minute);

    /// <summary>
    /// A small company: three ฝ่าย, departments one row per shift as in T-แผนก (ไดคาสท์ in กะ A and B, a ฝ่าย that is not in
    /// T-ฝ่าย), กะ A/B/D plus a preset-time row without a code, two งานรับจ้าง customers, four employees, a few days off and the
    /// open pay period 12–26 ต.ค. 2569 (ตั้งวันที่ตัดWEEK always has one row).
    /// </summary>
    public static FakeLegacyFile Standard() => new FakeLegacyFile()
        .Row("ฝ่าย", ("TYPE", "ผลิต")).Row("ฝ่าย", ("TYPE", "บุคคลและธุรการ")).Row("ฝ่าย", ("TYPE", "ทั่วไป"))
        .Row("แผนก", ("SUF", 1), ("DEPART", "บุคคลและธุรการ"), ("CODE", "O*"), ("SECTION", "บุคคลและธุรการ"),
            ("SUPER", "นาง  สมมุติ  ไม่มีตัวตน"), ("MANAGER", "ผู้จัดการสมมุติ"), ("SHIFT", "D"), ("FACTORY MANAGER", "ผจก.โรงงานสมมุติ"))
        .Row("แผนก", ("SUF", 2), ("DEPART", "ไดคาสท์"), ("CODE", "D*"), ("SECTION", "ผลิต"), ("SUPER", "นาย ทดสอบ หัวหน้ากะ"), ("SHIFT", "A"))
        .Row("แผนก", ("SUF", 3), ("DEPART", "ไดคาสท์"), ("CODE", "D*"), ("SECTION", "ผลิต"), ("SUPER", "นาย ทดสอบ หัวหน้ากะ"), ("SHIFT", "B"))
        .Row("แผนก", ("SUF", 4), ("DEPART", "จัดซื้อและการเงิน"), ("CODE", "F*"), ("SECTION", "จัดซื้อและการเงิน"), ("SHIFT", "D"))
        .Row("แผนก2", ("SUF", 1), ("DEPART", "ไดคาสท์"), ("QTY", 14)).Row("แผนก2", ("SUF", 2), ("DEPART", "บุคคลและธุรการ"), ("QTY", 4))
        .Row("SHIFT", ("SHIFT", "A"), ("W_IN", Time(8, 0)), ("W_OUT", Time(17, 0)), ("STOP", Time(12, 0)), ("START", Time(13, 0)),
            ("O_IN", Time(17, 20)), ("O_OUT", Time(20, 10)), ("REST_T", 60))
        .Row("SHIFT", ("SHIFT", "B"), ("W_IN", Time(20, 10)), ("W_OUT", Time(5, 10, nextDay: true)), ("STOP", Time(0, 10, nextDay: true)),
            ("START", Time(1, 10, nextDay: true)), ("O_IN", Time(5, 30, nextDay: true)), ("O_OUT", Time(8, 10, nextDay: true)), ("REST_T", 60))
        .Row("SHIFT", ("SHIFT", "D"), ("W_IN", Time(8, 0)), ("W_OUT", Time(17, 0)), ("STOP", Time(12, 0)), ("START", Time(13, 0)),
            ("O_IN", Time(17, 20)), ("O_OUT", Time(20, 10)), ("REST_T", 60))
        .Row("SHIFT", ("W_IN", Time(8, 0)), ("W_OUT", Time(12, 0)), ("REST_T", 0))
        .Row("ชื่อลูกค้า", ("CODE", "Y"), ("NAME", "ลูกค้าสมมุติหนึ่ง")).Row("ชื่อลูกค้า", ("CODE", "S"), ("NAME", "ลูกค้าสมมุติสอง"))
        .Row("EMPLO", ("ID", "o001"), ("NAME", "นาง  ทดสอบ  ธุรการ"), ("SEX", "F"), ("BORN", new DateTime(1990, 2, 3)),
            ("ID_CODE", "0000000000001"), ("ID_S", "0000000000001"), ("START", new DateTime(2020, 6, 1)), ("DEPARTMENT", "บุคคลและธุรการ"),
            ("SECTION", "บุคคลและธุรการ"), ("POSITION", "พนักงาน"), ("CLAS", "1"), ("SALARY", 15600.0), ("SA_DAY", 520.0),
            ("ACC", "0000000001"), ("ACC2", "0000000002"), ("SHIFT", "D"), ("HOT1", "N"), ("NATION", "ไทย"),
            ("NAME TITLE", "Mrs"), ("NAME ENGLISH", "Test Thurakan"), ("POINT", "ปวส."), ("WORK", "งานเอกสาร"), ("NO", "1"))
        .Row("EMPLO", ("ID", "D001"), ("NAME", "นาย  ทดสอบ  หัวหน้ากะ"), ("SEX", "M"), ("START", new DateTime(2015, 1, 5)),
            ("DEPARTMENT", "ไดคาสท์"), ("SECTION", "ผลิต"), ("POSITION", "หัวหน้า"), ("CLAS", "2"), ("SA_DAY", 400.0),
            ("SHIFT", "A"), ("HOT1", "Y"), ("POINT_1", 80), ("GRED_2", "NO"))
        .Row("EMPLO", ("ID", "D002"), ("NAME", "นาย ทดสอบ ลาออกแล้ว"), ("SEX", "M"), ("START", new DateTime(2010, 3, 1)),
            ("RESIGN", new DateTime(2019, 12, 31)), ("DEPARTMENT", "ไดคาสท์"), ("SECTION", "ผลิต"), ("CLAS", "2"), ("SHIFT", "B"),
            ("MEM", "ย้ายภูมิลำเนา"))
        .Row("EMPLO", ("ID", "F001"), ("NAME", "นางสาว ทดสอบ การเงิน"), ("SEX", "F"), ("START", new DateTime(2024, 9, 16)),
            ("DEPARTMENT", "จัดซื้อและการเงิน"), ("SECTION", "จัดซื้อและการเงิน"), ("CLAS", "1"), ("SALARY", 21000.0), ("SHIFT", "D"))
        .Row("SUNDAY", ("DATE", new DateTime(2025, 1, 5)), ("ITEM", "วันอาทิตย์"))
        .Row("SUNDAY", ("DATE", new DateTime(2025, 1, 12)), ("ITEM", "วันอาทิตย์"))
        .Row("HOLIDAY", ("DATE", new DateTime(2025, 1, 1)), ("ITEM", "วันขึ้นปีใหม่"))
        .Row("ตั้งวันที่ตัดWEEK", ("FROM_DA", new DateTime(2026, 10, 12)), ("TO_DA", new DateTime(2026, 10, 26)),
            ("PAY_D", new DateTime(2026, 11, 1)), ("DAY", 0));

    /// <summary>Writes the file as <paramref name="path"/> (a new .accdb).</summary>
    public string Save(string path)
    {
        var connectionString = $"Provider=Microsoft.ACE.OLEDB.16.0;Data Source={path};";
        dynamic catalog = Activator.CreateInstance(Type.GetTypeFromProgID("ADOX.Catalog")
            ?? throw new InvalidOperationException("ADOX is not registered on this PC"))!;
        catalog.Create(connectionString);
        catalog.ActiveConnection.Close();

        using (var connection = new OleDbConnection(connectionString))
        {
            connection.Open();
            foreach (var (table, columns) in Schema)
            {
                using (var create = new OleDbCommand(
                    $"CREATE TABLE [{table}] ({string.Join(", ", columns.Select(c => $"[{c.Column}] {c.Type}"))})", connection))
                    create.ExecuteNonQuery();
                foreach (var row in Tables[table])
                {
                    var names = row.Keys.ToList();
                    using var insert = new OleDbCommand(
                        $"INSERT INTO [{table}] ({string.Join(", ", names.Select(n => $"[{n}]"))}) VALUES ({string.Join(", ", names.Select(_ => "?"))})",
                        connection);
                    foreach (var name in names)
                        insert.Parameters.Add(Parameter(row[name]));
                    insert.ExecuteNonQuery();
                }
            }
        }
        OleDbConnection.ReleaseObjectPool();
        return path;
    }

    private static OleDbParameter Parameter(object? value) => value switch
    {
        null => new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = DBNull.Value },
        DateTime d => new OleDbParameter { OleDbType = OleDbType.Date, Value = d },
        double n => new OleDbParameter { OleDbType = OleDbType.Double, Value = n },
        float n => new OleDbParameter { OleDbType = OleDbType.Single, Value = n },
        int n => new OleDbParameter { OleDbType = OleDbType.Integer, Value = n },
        string s => new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = s },
        _ => throw new ArgumentException($"Unsupported legacy value {value.GetType()}"),
    };
}

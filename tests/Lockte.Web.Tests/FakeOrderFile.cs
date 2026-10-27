using System.Runtime.Versioning;

namespace Lockte.Web.Tests;

/// <summary>
/// The master-data tables of a made-up <c>ผลผลิตและORDER.accdb</c> with their legacy column types (data-model.md). Every name,
/// code and number in it is fictional — the repo is public. Written by <see cref="FakeLegacyFile"/>.
/// </summary>
public static class FakeOrderFile
{
    public static Dictionary<string, (string Column, string Type)[]> Schema { get; } = new()
    {
        ["เซล"] = [("ID", "TEXT(4)"), ("NAME", "TEXT(255)")],
        ["P_CUS"] =
        [
            ("CODE", "TEXT(255)"), ("NAME", "TEXT(255)"), ("CREDIT", "TEXT(255)"), ("ADDRESS", "TEXT(255)"), ("ADDRESS1", "TEXT(255)"),
            ("PROVINCES", "TEXT(255)"), ("P_CODE", "TEXT(50)"), ("ADD1", "TEXT(255)"), ("ADDDE", "TEXT(255)"), ("ADDDE1", "TEXT(255)"),
            ("TEL_1", "TEXT(255)"), ("TEL_2", "TEXT(255)"), ("SALE_ID", "TEXT(255)"), ("REMARK", "TEXT(255)"),
        ],
        ["P_NAME"] =
        [
            ("PD_C", "TEXT(255)"), ("PD_N", "TEXT(255)"), ("U_NAME", "TEXT(255)"), ("U_PACK", "TEXT(255)"), ("WEIGHT", "DOUBLE"),
            ("WEIGHT1", "DOUBLE"), ("WEIGHT2", "DOUBLE"), ("WEIGHT3", "DOUBLE"), ("WEIGHT4", "DOUBLE"), ("U_PRICE", "DOUBLE"),
            ("FML", "DOUBLE"), ("MEM", "TEXT(255)"), ("MOULD", "TEXT(255)"), ("CUS_ID", "TEXT(50)"), ("CUS", "TEXT(255)"),
            ("YEAR", "DATETIME"), ("MODEL", "TEXT(255)"), ("BARCODE", "TEXT(50)"), ("มอก", "TEXT(255)"), ("TYPE", "TEXT(255)"),
            ("SIZE", "TEXT(255)"), ("FORMULA", "TEXT(255)"), ("MODEL1", "TEXT(255)"),
        ],
        ["MC_N"] = [("CODE", "TEXT(6)"), ("NAME", "TEXT(30)"), ("DEP", "TEXT(255)")],
        ["รหัสพิมพ์"] =
        [
            ("ID", "TEXT(50)"), ("NAME", "TEXT(100)"), ("QTYS", "LONG"), ("OWNER", "TEXT(100)"), ("SHOT", "LONG"), ("PCS", "LONG"),
            ("YEAR", "LONG"), ("DEP", "TEXT(50)"), ("MARKER", "TEXT(100)"), ("DATE", "DATETIME"), ("OUT_DATE", "DATETIME"),
            ("COST", "LONG"), ("AL WEIGHT", "SINGLE"),
        ],
        ["แผนก"] = [("DEP", "TEXT(50)"), ("DEPA", "TEXT(50)")],
        ["ประเภท"] = [("MEM", "TEXT(255)")],
        ["TYPE"] = [("NANE", "TEXT(255)"), ("TYPE", "TEXT(255)")],
        ["MODEL"] = [("MODEL", "TEXT(255)")],
        ["รหัสสินค้าย่อ"] = [("C", "TEXT(50)"), ("N", "TEXT(50)")],
        ["EMPO"] = [("ID", "TEXT(5)"), ("NAME", "TEXT(30)")],
        ["ODD"] = OrderLine(refNoSize: 50),
        ["ORDER"] = OrderLine(refNoSize: 50),
        ["ODDส่งออก"] = OrderLine(refNoSize: 13),
        ["P_REPไดคาสท์"] = ReportLine(),
        ["P_REPตกแต่ง"] = ReportLine(),
        ["P_REPอัดผ้าเบรก"] = ReportLine(),
        ["P_REPฝนผ้าเบรก"] = ReportLine(),
        ["P_REPผ้าคลัทช์และดิสเบรค"] = ReportLine(),
        ["P_REPประกอบ"] = ReportLine(),
        ["P_REPบรรจุ"] = ReportLine(),
        ["P_REP"] = ReportLine(),
        ["P_ST"] = StockLine(),
        ["P_ADDST"] = StockLine(),
        ["ต้นงวดP_ST"] = StockLine(),
        ["การใช้งานของพิมพ์0"] = MoldLine(),
        ["ตันงวดการใช้งานของพิมพ์0"] = MoldLine(),
    };

    /// <summary>The columns of the stock tables (data-model.md): one structure for P_ST, P_ADDST and ต้นงวดP_ST (REF_NO and SUF_NO are Text there).</summary>
    private static (string Column, string Type)[] StockLine() =>
    [
        ("DATE", "DATETIME"), ("REF_NO", "TEXT(10)"), ("SUF_NO", "TEXT(2)"), ("PD_C", "TEXT(6)"), ("PD_N", "TEXT(100)"), ("QTY", "DOUBLE"), ("UNIT", "TEXT(50)"),
        ("ID", "TEXT(50)"), ("MARK", "TEXT(2)"), ("REM", "TEXT(50)"),
    ];

    /// <summary>The columns of the mold usage tables.</summary>
    private static (string Column, string Type)[] MoldLine() =>
    [
        ("DAY", "DATETIME"), ("MOULD_ID", "TEXT(50)"), ("PD_QTY", "LONG"), ("MARK", "TEXT(50)"),
    ];

    /// <summary>The columns of ODD, ORDER and ODDส่งออก (they share one structure, data-model.md); REF_NO is Text(13) in ODDส่งออก.</summary>
    private static (string Column, string Type)[] OrderLine(int refNoSize) =>
    [
        ("OD_DATE", "DATETIME"), ("CUS_ID", "TEXT(50)"), ("CUS_N", "TEXT(50)"), ("REF_NO", $"TEXT({refNoSize})"), ("SUF_NO", "LONG"),
        ("PD_C", "TEXT(6)"), ("PD_N", "TEXT(50)"), ("QTY", "DOUBLE"), ("U_NAME", "TEXT(50)"), ("U_PRICE", "DOUBLE"), ("AMOUNT", "DOUBLE"),
        ("DISC_PS", "DOUBLE"), ("NET", "DOUBLE"), ("MARK", "TEXT(50)"), ("SALE_ID", "TEXT(4)"), ("DL_DATE", "DATETIME"), ("INV_NO", "TEXT(11)"),
        ("INV_SUF", "LONG"), ("DL_QTY", "DOUBLE"), ("DL_NET", "DOUBLE"), ("UND_QTY", "DOUBLE"), ("DUERE_D", "TEXT(50)"), ("DATE_DL", "DATETIME"),
        ("LOT", "TEXT(255)"), ("REMARK", "TEXT(255)"),
    ];

    /// <summary>The columns of the P_REP tables (data-model.md): one structure for the seven departments and the transferred copy.</summary>
    private static (string Column, string Type)[] ReportLine() =>
    [
        ("DATE", "DATETIME"), ("REF_NO", "TEXT(8)"), ("SUF_NO", "LONG"), ("MC_NO", "TEXT(10)"), ("SHIFT", "TEXT(50)"), ("TT_T", "DOUBLE"), ("LOT_MTR1", "TEXT(50)"),
        ("LOT_MTR2", "TEXT(50)"), ("PD_C", "TEXT(5)"), ("PD_N", "TEXT(50)"), ("U_NAME", "TEXT(50)"), ("QTY", "DOUBLE"), ("NG", "DOUBLE"), ("EMP_ID", "TEXT(40)"),
        ("TOTAL", "DOUBLE"), ("MARK1", "TEXT(50)"), ("MOULD", "TEXT(100)"), ("LOT", "TEXT(50)"),
    ];

    [SupportedOSPlatform("windows")]
    public static FakeLegacyFile Empty() => new(Schema);
}

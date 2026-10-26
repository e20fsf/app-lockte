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
    };

    [SupportedOSPlatform("windows")]
    public static FakeLegacyFile Empty() => new(Schema);
}

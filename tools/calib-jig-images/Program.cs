using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

// ส่งรูป JIG ของแบบตรวจ (RPT-05/06) ออกจากไฟล์ Access เดิม (#200). อ่านนิยามรายงานที่ส่งออกเป็นข้อความ (SaveAsText: source/_extract/สอบเทียบ/reports/*.txt)
// หารูป JIG = กรอบ OLE (Word.Document) ที่ใหญ่ที่สุดของรายงาน แล้วดึง metafile (EMF) ที่ฝังอยู่ออกมาวาดเป็น PNG
// ใช้: CalibJigImages <โฟลเดอร์ reports/*.txt> <โฟลเดอร์ปลายทาง>
// ปลายทาง: images/<sha256 8 ตัว>.png (หนึ่งไฟล์ต่อรูปที่ไม่ซ้ำ), mapping.csv (ชนิดแบบ,รุ่น,รายงานเดิม,ไฟล์รูป), summary.txt (จำนวน — ไม่มีข้อมูลบุคคล)
// รูปไม่ถูก commit เข้า repo (repo เป็น public, Q-28 ข้อ 1): โฟลเดอร์ปลายทางต้องอยู่นอก repo
if (args.Length != 2)
{
    Console.Error.WriteLine("ใช้: CalibJigImages <โฟลเดอร์ reports> <โฟลเดอร์ปลายทาง>");
    return 2;
}
Console.OutputEncoding = Encoding.UTF8;
var (reportsFolder, outputFolder) = (args[0], args[1]);
Directory.CreateDirectory(Path.Combine(outputFolder, "images"));

var rows = new List<string> { "type,model,report,image" };
var summary = new List<string>();
var unique = new HashSet<string>();
int exported = 0, noImage = 0;
foreach (var file in Directory.EnumerateFiles(reportsFolder, "*.txt").Order())
{
    var name = Path.GetFileNameWithoutExtension(file);
    if (SheetOf(name) is not { } sheet) continue;

    var frames = ReadFrames(File.ReadAllLines(file, Encoding.UTF8));
    var main = frames.Where(f => f.Blob.Length > 0).OrderByDescending(f => (long)f.Width * f.Height).FirstOrDefault();
    var png = main is null ? null : TryRender(main.Blob);
    if (png is null)
    {
        noImage++;
        summary.Add($"ขาดรูป: {sheet.Type} {sheet.Model} ({name})");
        rows.Add($"{sheet.Type},{Csv(sheet.Model)},{Csv(name)},");
        continue;
    }
    var hash = Convert.ToHexString(SHA256.HashData(png))[..8].ToLowerInvariant();
    var path = Path.Combine(outputFolder, "images", hash + ".png");
    if (unique.Add(hash)) File.WriteAllBytes(path, png);
    exported++;
    rows.Add($"{sheet.Type},{Csv(sheet.Model)},{Csv(name)},{hash}.png");
}

File.WriteAllLines(Path.Combine(outputFolder, "mapping.csv"), rows, new UTF8Encoding(true));
summary.Insert(0, $"รายงานที่มีรูป {exported} · ไม่มีรูป/อ่านไม่ได้ {noImage} · รูปที่ไม่ซ้ำ {unique.Count}");
File.WriteAllLines(Path.Combine(outputFolder, "summary.txt"), summary, new UTF8Encoding(true));
summary.ForEach(Console.WriteLine);
return noImage == 0 ? 0 : 1;

static (string Type, string Model)? SheetOf(string report)
{
    // RPT-06: "JIG กรอ <รุ่น>" · RPT-05: the report's own name; the names the Switchboard buttons shorten are listed here
    if (report.StartsWith("JIG กรอ ", StringComparison.Ordinal)) return ("Grinding", report["JIG กรอ ".Length..]);
    return report switch
    {
        "052,063" or "061" or "068" or "143" or "168" or "233" or "ATTILA" or "CLICK" or "JOG" or "K125" or "NOUVO" or "PIN GAUGE" or "RX100" or "TACT"
            or "TAPER GAUGE" or "TS100" or "V100" or "WING" => ("ZeroCheck", report),
        "A100,GTO R,KAZE" => ("ZeroCheck", "A100"),
        "C100,WAVE110,043,XL078" => ("ZeroCheck", "C100"),
        "NH50,SS50,098,029,048" => ("ZeroCheck", "NH50"),
        "NOUVO,DT100,105,SPACY" => ("ZeroCheck", "DT100"),
        "GTO F,AR125" => ("ZeroCheck", "GTO F"),
        _ => null,
    };
}

static string Csv(string text) => text.Contains(',') || text.Contains('"') ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;

static List<Frame> ReadFrames(string[] lines)
{
    var frames = new List<Frame>();
    for (var i = 0; i < lines.Length; i++)
    {
        if (lines[i].Trim() != "Begin UnboundObjectFrame") continue;
        int width = 0, height = 0;
        var blob = new List<byte>();
        for (i++; i < lines.Length && lines[i].Trim() != "End"; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("Width =", StringComparison.Ordinal)) width = int.Parse(line[7..], CultureInfo.InvariantCulture);
            else if (line.StartsWith("Height =", StringComparison.Ordinal)) height = int.Parse(line[8..], CultureInfo.InvariantCulture);
            else if (line.StartsWith("OleData = Begin", StringComparison.Ordinal))
            {
                for (i++; i < lines.Length && lines[i].Trim() != "End"; i++)
                {
                    var hex = lines[i].Trim().TrimEnd(',', ' ');
                    if (hex.StartsWith("0x", StringComparison.Ordinal)) blob.AddRange(Convert.FromHexString(hex[2..]));
                }
            }
        }
        frames.Add(new Frame(width, height, blob.ToArray()));
    }
    return frames;
}

/// <summary>The EMF inside an OLE object: from its header (record 1, " EMF" at offset 40) for the size its header states.</summary>
static byte[]? ExtractEmf(byte[] blob)
{
    ReadOnlySpan<byte> signature = [0x20, 0x45, 0x4D, 0x46];
    for (var at = 40; at + 4 <= blob.Length; at++)
    {
        if (!blob.AsSpan(at, 4).SequenceEqual(signature)) continue;
        var start = at - 40;
        if (BitConverter.ToUInt32(blob, start) != 1) continue;
        var size = (int)BitConverter.ToUInt32(blob, start + 48);
        if (size <= 88 || start + size > blob.Length) size = blob.Length - start;
        return blob.AsSpan(start, size).ToArray();
    }
    return null;
}

static byte[]? TryRender(byte[] blob)
{
    try
    {
        if (ExtractEmf(blob) is not { } emf) return null;
        using var metafile = new Metafile(new MemoryStream(emf));
        var scale = 1200f / Math.Max(1, metafile.Width);
        using var bitmap = new Bitmap((int)(metafile.Width * scale), (int)(metafile.Height * scale));
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            g.DrawImage(metafile, 0, 0, bitmap.Width, bitmap.Height);
        }
        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return output.ToArray();
    }
    catch (Exception ex) when (ex is ExternalException or ArgumentException or OutOfMemoryException or InvalidOperationException)
    {
        return null;
    }
}

internal sealed record Frame(int Width, int Height, byte[] Blob);

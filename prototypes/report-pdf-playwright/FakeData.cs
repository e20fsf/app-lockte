// PROTOTYPE — throwaway. Fake employees only; this repo is public, so no real names/IDs/company data.
using System.Globalization;

namespace ReportPdfProto;

public record Employee(
    string IdCode, string Name, string Nation, DateTime Born, DateTime Start, string Position,
    decimal Salary, decimal SaDay, DateTime? Resign,
    string Father, string Mother, string Address, string Department, string Job)
{
    // ทะเบียนผู้ประกันตน Expr1: IIf([salary]=0,[sa_day],[salary])
    public decimal Wage => Salary == 0 ? SaDay : Salary;
}

public static class FakeData
{
    public static readonly CultureInfo Th = CreateThai();

    static CultureInfo CreateThai()
    {
        var c = (CultureInfo)CultureInfo.GetCultureInfo("th-TH").Clone();
        c.DateTimeFormat.Calendar = new ThaiBuddhistCalendar(); // legacy PCs run Thai regional settings (พ.ศ.)
        return c;
    }

    // Tricky Thai on purpose: stacked tone marks (ปิ่น ปุ๊ก ฟ้า), tall consonants, sara am (น้ำ), ฐ ญ ฎ ฏ with descenders.
    static readonly string[] First = ["ปิ่นแก้ว", "ฟ้าใส", "น้ำฝน", "ปุ๊ก", "จุฑามาศ", "ญาณิศา", "ฐิติพร", "ฎีกา", "ปัญญ์", "ฝ้ายคำ", "สมศักดิ์", "ศุภฤกษ์", "ฤทัย", "กิ่งก้อย", "บุญเลิศ", "ปู่จ๋าย"];
    static readonly string[] Last = ["ศรีสุขทดสอบ", "ทองคำปั้นตัวอย่าง", "พุ่มพวงสมมติ", "ปัญญาฟู", "ฝั้นเฟื่องฟ้า", "เจริญผลจำลอง", "ยิ่งยงค์", "ฤทธิ์ณรงค์ปลอม", "บุญญฤทธิ์"];
    static readonly string[] Titles = ["นาย", "นาง", "นางสาว"];
    static readonly string[] Positions = ["พนักงานฝ่ายผลิต", "หัวหน้ากะแผนกอัดผ้าเบรก", "พนักงานตรวจสอบคุณภาพ (QA)", "ช่างซ่อมบำรุงแม่พิมพ์ฉีดอลูมิเนียมและเครื่องจักรกลหนัก", "ธุรการ", "พนักงานบรรจุ"];
    static readonly string[] Departments = ["ไดคาสท์", "ตกแต่ง", "อัดผ้าเบรก", "ฝนผ้าเบรก", "ผ้าคลัทช์และดิสเบรก", "ประกอบ", "บรรจุ"];
    static readonly string[] Nations = ["ไทย", "ไทย", "ไทย", "ไทย", "เมียนมา", "กัมพูชา"];

    public static List<Employee> Employees(int count, int seed = 42)
    {
        var r = new Random(seed);
        var list = new List<Employee>(count);
        for (var i = 0; i < count; i++)
        {
            var monthly = r.Next(5) == 0;
            var born = new DateTime(1970, 1, 1).AddDays(r.Next(0, 13000));
            var start = new DateTime(2005, 1, 1).AddDays(r.Next(0, 7500));
            list.Add(new Employee(
                // Prefix 0 + no checksum: cannot collide with a real Thai citizen ID.
                IdCode: $"0{r.NextInt64(100000000000, 999999999999)}",
                Name: $"{Titles[r.Next(Titles.Length)]}{First[r.Next(First.Length)]} {Last[r.Next(Last.Length)]}",
                Nation: Nations[r.Next(Nations.Length)],
                Born: born,
                Start: start,
                Position: Positions[r.Next(Positions.Length)],
                Salary: monthly ? r.Next(15, 45) * 1000m : 0m,
                SaDay: monthly ? 0m : 372m + r.Next(0, 80),
                Resign: r.Next(4) == 0 ? start.AddDays(r.Next(30, 3000)) : null,
                Father: $"{First[r.Next(First.Length)]} {Last[r.Next(Last.Length)]}",
                Mother: $"{First[r.Next(First.Length)]} {Last[r.Next(Last.Length)]}",
                Address: $"{r.Next(1, 999)}/{r.Next(1, 99)} หมู่ {r.Next(1, 12)} ต.ตัวอย่าง อ.ทดสอบ จ.สมมติ",
                Department: Departments[r.Next(Departments.Length)],
                Job: "ตรวจนับและบรรจุชิ้นงานผ้าเบรกลงกล่อง ติดฉลาก และเคลื่อนย้ายกล่องน้ำหนักไม่เกินที่กฎหมายกำหนด"));
        }
        // SELECT DISTINCT … ORDER BY ID_CODE, START
        return list.OrderBy(e => e.IdCode, StringComparer.Ordinal).ThenBy(e => e.Start).ToList();
    }
}

using System.Globalization;
using System.Text;

namespace Lockte.SharedKernel;

/// <summary>
/// จำนวนเงินเป็นตัวอักษรไทย — the legacy VBA module <c>ThaiNumber</c> (<c>GetThaiUnit</c>) line for line, including its
/// readings that differ from the standard one (specs/employee/reports.md "ฟังก์ชันช่วย: ThaiNumber", AC-12, kept as
/// the legacy by Q-17): a unit 1 reads "เอ็ด" only when the tens digit is not 0 (101 → หนึ่งร้อยหนึ่ง), and satang
/// are the first two decimals cut, not rounded.
/// </summary>
public static class ThaiNumber
{
    private static readonly string[] Digits = ["ศูนย์", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า"];

    /// <summary>The reading as the reports print it (<c>Trim(GetThaiUnit(n))</c>), e.g. 10400.5 → หนึ่งหมื่นสี่ร้อยบาทห้าสิบสตางค์.</summary>
    public static string Baht(double amount) => GetThaiUnit(amount).Trim();

    /// <summary><c>GetThaiUnit</c> itself: the result starts with a space, as in the legacy.</summary>
    public static string GetThaiUnit(double amount)
    {
        // Format$(Val(Format$(n))): VBA's general format, 15 significant digits.
        var input = amount.ToString("G15", CultureInfo.InvariantCulture);
        var output = new StringBuilder(" ");
        if (input.StartsWith('-'))
        {
            input = input[1..];
            output.Append("ลบ");
        }

        var dot = input.IndexOf('.');
        var front = dot < 0 ? input : input[..dot];
        var back = dot < 0 ? " " : input[(dot + 1)..];
        if (back.Length > 2) back = back[..2];

        if (front.Length != 0)
        {
            for (var i = 0; i < front.Length; i++)
            {
                var digit = front[i];
                var place = front.Length - 1 - i;
                switch (place % 6)
                {
                    case 0:
                        if (digit != '0')
                            output.Append(digit == '1' && i != 0 && front[i - 1] != '0' ? "เอ็ด" : Digits[digit - '0']);
                        if (place >= 6) output.Append("ล้าน");
                        break;
                    case 1:
                        if (digit != '0') output.Append(digit switch { '1' => "", '2' => "ยี่", _ => Digits[digit - '0'] }).Append("สิบ");
                        break;
                    default:
                        if (digit != '0') output.Append(Digits[digit - '0']).Append((place % 6) switch { 2 => "ร้อย", 3 => "พัน", 4 => "หมื่น", _ => "แสน" });
                        break;
                }
            }
            if (output.ToString() != " ") output.Append("บาท");
        }

        if (back == " " || int.Parse(back, CultureInfo.InvariantCulture) == 0)
            return output.ToString() != " " ? output.Append("ถ้วน").ToString() : "ศูนย์บาทถ้วน";

        if (back[0] != '0') output.Append(back[0] switch { '1' => "สิบ", '2' => "ยี่สิบ", _ => Digits[back[0] - '0'] + "สิบ" });
        if (back.Length == 2 && back[1] != '0')
            output.Append(back[0] != '0' && back[1] == '1' ? "เอ็ด" : Digits[back[1] - '0']);
        return output.Append("สตางค์").ToString();
    }
}

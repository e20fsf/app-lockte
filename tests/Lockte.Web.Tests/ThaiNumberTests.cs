using Lockte.SharedKernel;

namespace Lockte.Web.Tests;

/// <summary>AC-12 จำนวนเงินเป็นตัวอักษรไทย: the table of specs/employee/reports.md "ฟังก์ชันช่วย: ThaiNumber", kept as the legacy (Q-17).</summary>
public sealed class ThaiNumberTests
{
    [Theory]
    [InlineData(15600, "หนึ่งหมื่นห้าพันหกร้อยบาทถ้วน")]
    [InlineData(21, "ยี่สิบเอ็ดบาทถ้วน")]
    [InlineData(101, "หนึ่งร้อยหนึ่งบาทถ้วน")] // ⚠ legacy: เอ็ด only after a tens digit that is not 0
    [InlineData(1000000, "หนึ่งล้านบาทถ้วน")]
    [InlineData(10400.5, "หนึ่งหมื่นสี่ร้อยบาทห้าสิบสตางค์")]
    [InlineData(9672, "เก้าพันหกร้อยเจ็ดสิบสองบาทถ้วน")] // the sample หนังสือรับรองเงินเดือน
    [InlineData(230000, "สองแสนสามหมื่นบาทถ้วน")] // the sample ฟอร์มเกษียณ
    [InlineData(11, "สิบเอ็ดบาทถ้วน")]
    [InlineData(21000000, "ยี่สิบเอ็ดล้านบาทถ้วน")]
    [InlineData(0, "ศูนย์บาทถ้วน")]
    [InlineData(0.25, "ยี่สิบห้าสตางค์")]
    [InlineData(12.219, "สิบสองบาทยี่สิบเอ็ดสตางค์")] // satang cut to two digits, not rounded
    [InlineData(-5, "ลบห้าบาทถ้วน")]
    public void Amounts_read_as_the_legacy_GetThaiUnit(double amount, string words)
        => Assert.Equal(words, ThaiNumber.Baht(amount));

    [Fact] // the reports Trim() it: GetThaiUnit itself starts with a space
    public void GetThaiUnit_starts_with_a_space_as_in_VBA()
        => Assert.Equal(" ยี่สิบเอ็ดบาทถ้วน", ThaiNumber.GetThaiUnit(21));
}

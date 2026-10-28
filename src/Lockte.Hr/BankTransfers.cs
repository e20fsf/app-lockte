namespace Lockte.Hr;

/// <summary>What the transfer lists of one employee in a period read: the accounts (EMPLO.ACC / ACC2) and the pay (BR-033).</summary>
public sealed record BankPayInput(string Code, string? AccountTtb, string? AccountKbank, decimal PayNormal, decimal PayOvertime, decimal Hot);

/// <summary>One line of a transfer list: <see cref="Bank"/> is one of <see cref="BankTransfers"/>' constants.</summary>
public sealed record BankTransferRow(string Bank, string Account, decimal Amount, string Code);

/// <summary>Items, total and fee of one list (<see cref="BankTransfers.Summary"/>).</summary>
public sealed record BankTransferSummary(string Bank, int Items, decimal Total, decimal Fee);

/// <summary>
/// BR-033 ยอดส่งธนาคาร, from the same ค่าแรง as the slips: ทหารไทย = PAY_W of those with ACC (PAY_W &gt; 0), กสิกร = PAY_OT of
/// those with ACC2 (PAY_OT &gt; 0), หนังสือนำส่ง = PAY_W + PAY_OT + HOT of those with ACC (&gt; 0), fee 10 baht an item.
/// Pure, so the comparison with Access (#57) and the bank file (#54) read one source. ⚠ Not net of เงินสงเคราะห์ (Q-54).
/// </summary>
public static class BankTransfers
{
    public const string Ttb = "TTB";
    public const string Kbank = "KBANK";
    /// <summary>The list behind หนังสือนำส่งข้อมูล (คิวรี ค่าแรงส่งแบงค์10).</summary>
    public const string Letter = "LETTER";
    public const decimal FeePerItem = 10;

    public static IReadOnlyList<BankTransferRow> Build(IEnumerable<BankPayInput> pay)
    {
        var rows = new List<BankTransferRow>();
        foreach (var p in pay)
        {
            if (Account(p.AccountTtb) is { } ttb)
            {
                if (p.PayNormal > 0) rows.Add(new(Ttb, ttb, p.PayNormal, p.Code));
                if (p.PayNormal + p.PayOvertime + p.Hot > 0) rows.Add(new(Letter, ttb, p.PayNormal + p.PayOvertime + p.Hot, p.Code));
            }
            if (Account(p.AccountKbank) is { } kbank && p.PayOvertime > 0) rows.Add(new(Kbank, kbank, p.PayOvertime, p.Code));
        }
        return rows.OrderBy(r => r.Bank, StringComparer.Ordinal).ThenBy(r => r.Account, StringComparer.Ordinal).ToList();
    }

    public static IReadOnlyList<BankTransferSummary> Summary(IEnumerable<BankTransferRow> rows)
        => rows.GroupBy(r => r.Bank).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new BankTransferSummary(g.Key, g.Count(), g.Sum(r => r.Amount), g.Count() * FeePerItem)).ToList();

    /// <summary>The account as digits only (Access stored a number or text with dashes); null when blank.</summary>
    public static string? Account(string? value)
    {
        var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }
}

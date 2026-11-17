using Lockte.Sales;
using Lockte.Web.Components;
using Lockte.Web.Reports;

namespace Lockte.Web.Tests;

/// <summary>UAT-33 (#414): the รหัสลูกค้า pick-box — a code search is a prefix, the list is complete and sorted, each row shows the seven columns.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class CustomerSearchTests(LocalDbFixture db)
{
    private static string Letters(int n) => new(Enumerable.Range(0, n).Select(_ => (char)('a' + Random.Shared.Next(26))).ToArray());

    private async Task AddAsync(string code, string name, string? address = null)
    {
        await using var scope = db.Scope("sales1");
        var result = await scope.Get<CustomerRegistry>().SaveAsync(new CustomerInput(null, code, name, null, CreditDays: 30, Address: address, Province: "จังหวัดทดสอบ"));
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }

    private async Task<IReadOnlyList<CustomerDto>> SearchAsync(string? text)
    {
        await using var scope = db.Scope("sales1");
        return await scope.Get<CustomerDirectory>().SearchAsync(text);
    }

    [Fact] // digits = the code STARTS with them: the same digits in the middle of another code do not match
    public async Task A_code_search_is_a_prefix_not_a_contains()
    {
        var p = Random.Shared.Next(10_000_000, 99_999_999).ToString();
        await AddAsync($"{p}-01-001", "ร้านทดสอบ " + Letters(8));
        await AddAsync($"01-{p}-005", "ร้านทดสอบ " + Letters(8));

        var found = (await SearchAsync(p)).Select(c => c.Code).ToList();

        Assert.Equal([$"{p}-01-001"], found);
    }

    [Fact] // letters search the name (anywhere in it), not the code
    public async Task A_text_search_finds_the_name()
    {
        var tag = Letters(12);
        await AddAsync("77-" + Random.Shared.Next(10_000_000, 99_999_999), $"บริษัท {tag} จำกัด");

        var found = await SearchAsync(tag);

        Assert.Single(found);
        Assert.Contains(tag, found[0].Name);
    }

    [Fact] // the old cap was 50 rows: 60 customers of one prefix all come back, by รหัส
    public async Task More_than_the_old_cap_of_50_are_all_listed_in_code_order()
    {
        var p = Random.Shared.Next(10_000_000, 99_999_999).ToString();
        for (var i = 60; i >= 1; i--) await AddAsync($"{p}-{i:000}", "ร้านทดสอบ " + Letters(6));

        var found = (await SearchAsync(p)).Select(c => c.Code).ToList();

        Assert.Equal(60, found.Count);
        Assert.Equal(found.OrderBy(c => c, StringComparer.Ordinal).ToList(), found);
    }
}

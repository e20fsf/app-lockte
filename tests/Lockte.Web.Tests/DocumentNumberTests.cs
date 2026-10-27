using Lockte.SharedKernel.Persistence;
using Lockte.Web.Audit;
using Lockte.Web.Reports;

namespace Lockte.Web.Tests;

/// <summary>เลขเอกสารควบคุม (#40, #52): master data with the legacy numbers copied character for character, editable.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class DocumentNumberTests(LocalDbFixture db)
{
    private async Task<T> AsUserAsync<T>(Func<DocumentNumberService, Task<T>> action)
    {
        await using var scope = db.Scope("doc1");
        return await action(scope.Get<DocumentNumberService>());
    }

    [Theory] // specs/employee/print-samples.md — spaces as printed, also where they differ between documents
    [InlineData(DocumentKeys.LeaveForm, "WI-PE-004-1/2547 ฉบับที่ 5")]
    [InlineData(DocumentKeys.DailyContract, "PC-PE001-9/2547ฉบับที่1")]
    [InlineData(DocumentKeys.MonthlyContract, "PD-PE001-9/2547ฉบับที่1")] // PD, not PC: as printed
    [InlineData(DocumentKeys.ProbationForm, "PC-PE001-5/2547 ฉบับที่2")]
    [InlineData(DocumentKeys.WorkCertificate, "WI-PE006-2/2547 ฉบับที่ 1")]
    [InlineData(DocumentKeys.ProbationEvaluation, "PC-PE001-6/2547  ฉบับที่2")]
    [InlineData(DocumentKeys.DocumentTransmittal, "PC-PE001-8/2547ฉบับที่3")]
    [InlineData(DocumentKeys.InsuredRegister, "สปส.6-07")]
    [InlineData(DocumentKeys.Kr2, "แบบ คร.2")]
    public async Task The_legacy_numbers_are_there_from_the_start(string key, string printed)
        => Assert.Equal(printed, await AsUserAsync(s => s.PrintedTextAsync(key)));

    [Fact]
    public async Task Every_numbered_legacy_document_is_listed()
    {
        var all = await AsUserAsync(s => s.ListAsync());

        Assert.Equal(21, all.Count);
        Assert.Equal(all.Select(d => d.Key).Order(), DocumentKeys.All.Order());
    }

    [Fact] // ผู้ควบคุมเอกสารออกฉบับใหม่ได้เองโดยไม่แก้โปรแกรม
    public async Task A_new_edition_is_printed_after_saving_and_kept_in_the_history()
    {
        var row = (await AsUserAsync(s => s.ListAsync())).Single(d => d.Key == DocumentKeys.YearlyEvaluationDaily);

        var result = await AsUserAsync(s => s.SaveAsync(new DocumentNumberInput(row.Id, " WI-PE013-3/2548 ", " ฉบับที่3", row.RowVersion)));

        Assert.True(result.Succeeded, string.Join(", ", result.Errors));
        Assert.Equal("WI-PE013-3/2548 ฉบับที่3", await AsUserAsync(s => s.PrintedTextAsync(DocumentKeys.YearlyEvaluationDaily)));
        await using var scope = db.Scope();
        var history = await scope.Get<HistoryService>().SearchAsync(new HistoryFilter(Table: DocumentNumberService.Table));
        Assert.Contains(history, h => h.Kind == HistoryKind.Change && h.UserName == "doc1" && h.NewValue == " ฉบับที่3");
    }

    [Fact]
    public async Task A_document_number_needs_a_code()
    {
        var row = (await AsUserAsync(s => s.ListAsync())).Single(d => d.Key == DocumentKeys.ResignationLetter);

        var result = await AsUserAsync(s => s.SaveAsync(new DocumentNumberInput(row.Id, " ", "", row.RowVersion)));

        Assert.Equal(["กรุณากรอกรหัสเอกสาร"], result.Errors);
    }

    [Fact]
    public async Task A_report_without_a_number_gets_none()
        => Assert.Null(await AsUserAsync(s => s.PrintedTextAsync("RPT-01")));
}

using Lockte.Calib;

namespace Lockte.Web.Tests;

/// <summary>รูป JIG ของแบบตรวจ (#200): stored as data of the app; the pictures here are fake (a PNG header and filler), never the real ones.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class JigImageTests(LocalDbFixture db)
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>A fake picture: the PNG signature followed by filler that differs per <paramref name="seed"/>.</summary>
    private static byte[] FakePng(string seed) => [.. PngHeader, .. System.Text.Encoding.UTF8.GetBytes("fake-jig-" + seed)];

    private async Task<T> AsQaAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("qa1");
        return await action(scope);
    }

    private Task<JigSheetDto> SheetAsync(JigSheetType type, string model) => AsQaAsync(async s => (await s.Get<JigSheetDirectory>().FindAsync(type, model))!);

    [Fact]
    public async Task A_picture_is_set_read_back_replaced_and_removed()
    {
        var sheet = await SheetAsync(JigSheetType.ZeroCheck, "V100");
        var first = FakePng(Guid.NewGuid().ToString("N"));
        Assert.True((await AsQaAsync(s => s.Get<JigImageRegistry>().SetAsync(sheet.Id, first))).Succeeded);

        var image = await AsQaAsync(s => s.Get<JigImageDirectory>().GetAsync(sheet.Id));
        Assert.Equal("image/png", image!.ContentType);
        Assert.Equal(first, image.Data); // byte arrays: by content
        Assert.True((await SheetAsync(JigSheetType.ZeroCheck, "V100")).HasImage);

        var second = FakePng(Guid.NewGuid().ToString("N"));
        Assert.True((await AsQaAsync(s => s.Get<JigImageRegistry>().SetAsync(sheet.Id, second))).Succeeded);
        Assert.Equal(second, (await AsQaAsync(s => s.Get<JigImageDirectory>().GetAsync(sheet.Id)))!.Data);

        Assert.True((await AsQaAsync(s => s.Get<JigImageRegistry>().ClearAsync(sheet.Id))).Succeeded);
        Assert.Null(await AsQaAsync(s => s.Get<JigImageDirectory>().GetAsync(sheet.Id)));
        Assert.Contains(await AsQaAsync(s => s.Get<JigImageDirectory>().GetSheetsWithoutImageAsync()), r => r.Id == sheet.Id);
    }

    [Fact]
    public async Task Only_png_or_jpeg_within_the_size_limit_is_accepted()
    {
        var sheet = await SheetAsync(JigSheetType.ZeroCheck, "JOG");
        Assert.Equal(["รูปต้องเป็นไฟล์ PNG หรือ JPEG"], (await AsQaAsync(s => s.Get<JigImageRegistry>().SetAsync(sheet.Id, "not a picture"u8.ToArray()))).Errors);
        var big = new byte[JigImageRegistry.MaxBytes + 1];
        PngHeader.CopyTo(big, 0);
        Assert.Contains("รูปใหญ่เกิน", (await AsQaAsync(s => s.Get<JigImageRegistry>().SetAsync(sheet.Id, big))).Errors.Single());
        Assert.Equal(["ไม่พบแบบตรวจนี้"], (await AsQaAsync(s => s.Get<JigImageRegistry>().SetAsync(int.MaxValue, FakePng("x")))).Errors);
        Assert.True((await AsQaAsync(s => s.Get<JigImageRegistry>().SetAsync(sheet.Id, [0xFF, 0xD8, 0xFF, 0xE0, 1, 2]))).Succeeded);
        Assert.Equal("image/jpeg", (await AsQaAsync(s => s.Get<JigImageDirectory>().GetAsync(sheet.Id)))!.ContentType);
    }

    [Fact] // the same picture serves every sheet that shares it
    public async Task Sheets_with_the_same_picture_share_one_stored_copy()
    {
        var a = await SheetAsync(JigSheetType.Grinding, "CLICK");
        var b = await SheetAsync(JigSheetType.Grinding, "JOG");
        var png = FakePng(Guid.NewGuid().ToString("N"));
        Assert.True((await AsQaAsync(s => s.Get<JigImageRegistry>().SetAsync(a.Id, png))).Succeeded);
        Assert.True((await AsQaAsync(s => s.Get<JigImageRegistry>().SetAsync(b.Id, png))).Succeeded);

        Assert.Equal(png, (await AsQaAsync(s => s.Get<JigImageDirectory>().GetAsync(b.Id)))!.Data);
        // removing it from one sheet keeps it for the other
        Assert.True((await AsQaAsync(s => s.Get<JigImageRegistry>().ClearAsync(a.Id))).Succeeded);
        Assert.Equal(png, (await AsQaAsync(s => s.Get<JigImageDirectory>().GetAsync(b.Id)))!.Data);
    }

    [Fact]
    public async Task A_folder_written_by_the_export_tool_is_loaded_and_the_gaps_are_reported()
    {
        var folder = Path.Combine(Path.GetTempPath(), "jigimg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "images"));
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(folder, "images", "aaaa1111.png"), FakePng("a" + Guid.NewGuid().ToString("N")));
            await File.WriteAllBytesAsync(Path.Combine(folder, "images", "bbbb2222.png"), FakePng("b" + Guid.NewGuid().ToString("N")));
            await File.WriteAllTextAsync(Path.Combine(folder, "images", "text.png"), "not a picture");
            await File.WriteAllLinesAsync(Path.Combine(folder, "mapping.csv"),
            [
                "type,model,report,image",
                "ZeroCheck,\"052,063\",\"052,063\",aaaa1111.png",
                "ZeroCheck,CLICK,CLICK,aaaa1111.png",
                "Grinding,K125,JIG กรอ K125,bbbb2222.png",
                "ZeroCheck,NOSUCH,NOSUCH,aaaa1111.png",
                "ZeroCheck,TACT,TACT,",
                "ZeroCheck,TS100,TS100,missing.png",
                "Grinding,WING,JIG กรอ WING,text.png",
            ]);

            var report = await AsQaAsync(s => s.Get<JigImageRegistry>().ImportFolderAsync(folder));

            Assert.Equal((7, 3, 2), (report.MappingRows, report.Loaded, report.DistinctImages));
            Assert.Equal(["ZeroCheck NOSUCH (NOSUCH)"], report.SheetNotFound);
            Assert.Equal(3, report.FileProblems.Count);
            Assert.False(report.Complete);
            Assert.DoesNotContain(report.StillWithoutImage, r => r is { Type: JigSheetType.ZeroCheck, ModelKey: "052,063" or "CLICK" });
            Assert.Contains(report.StillWithoutImage, r => r is { Type: JigSheetType.ZeroCheck, ModelKey: "TACT" });
            Assert.NotNull(await AsQaAsync(async s => await s.Get<JigImageDirectory>().GetAsync((await s.Get<JigSheetDirectory>().FindAsync(JigSheetType.Grinding, "K125"))!.Id)));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}

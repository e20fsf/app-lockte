using Lockte.Org;
using Lockte.Purch;

namespace Lockte.Web.Tests;

/// <summary>คะแนนประเมินผู้ขาย (BR-050, AC-22, AC-23) without a database.</summary>
public sealed class SupplierScoreTests
{
    private static readonly PurchSettingsDto Settings = new(0.07m, 5000m, null, ["sp"], 80m, 70m, 60m, [], null);

    private static SupplierCounts Counts(int failed = 0, int withoutCertificate = 0, int late = 0) => new(5, failed, 0, withoutCertificate, 5, late);

    [Fact] // BR-050 #1
    public void A_clean_supplier_with_one_late_delivery_scores_24_of_25_grade_A()
    {
        var score = SupplierScore.Calculate(Counts(late: 1), true, 5, 5, Settings);
        Assert.Equal((5, 5, 4, 5, 5, 24), (score.Quality, score.Certificate, score.Delivery, score.Service1, score.Service2, score.Total));
        Assert.Equal((96m, "A", "ผู้ขายชั้นดี"), (score.Percent, score.Grade, score.Result));
    }

    [Fact] // BR-050 #2, AC-22
    public void Two_failures_and_no_certificate_score_15_grade_C()
    {
        var score = SupplierScore.Calculate(Counts(failed: 2), false, 3, 3, Settings);
        Assert.Equal((3, 1, 5, 15), (score.Quality, score.Certificate, score.Delivery, score.Total));
        Assert.Equal((60m, "C", "ระมัดระวังในการสั่งซื้อ,สั่งซื้อเท่าที่จำเป็น"), (score.Percent, score.Grade, score.Result));
    }

    [Theory] // BR-050 #3, AC-23: the thresholds are met, not exceeded
    [InlineData(20, "A")]
    [InlineData(19, "B")]
    [InlineData(18, "B")]
    [InlineData(17, "C")]
    [InlineData(15, "C")]
    [InlineData(14, "D")]
    public void The_grade_follows_the_settings(int total, string grade)
        => Assert.Equal(grade, Settings.GradeOf(total * 100m / SupplierScore.Maximum));

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 4)]
    [InlineData(2, 3)]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    [InlineData(29, 1)]
    public void A_count_becomes_points(int count, int points) => Assert.Equal(points, SupplierScore.Points(count));

    [Fact] // BR-050 #5: 28 receipts without a certificate still give 1, and the percentage is not rounded
    public void The_real_example_scores_21_and_84_percent()
    {
        var score = SupplierScore.Calculate(new SupplierCounts(29, 0, 1, 28, 29, 0), true, 5, 5, Settings);
        Assert.Equal((5, 1, 5, 21, 84m, "A"), (score.Quality, score.Certificate, score.Delivery, score.Total, score.Percent, score.Grade));
    }

    [Fact]
    public void Each_grade_has_its_wording()
    {
        Assert.Equal("ควรสอบถามข้อมูลให้แน่ชัดก่อนสั่งซื้อ", SupplierScore.ResultText("B"));
        Assert.Equal("ห้ามสั่งซื้อ นอกจากได้รับคำสั่งจาก ผจก.จัดซื้อ", SupplierScore.ResultText("D"));
    }
}

/// <summary>ประเมินผู้ขายรายไตรมาส (#253, BR-050): what is counted, replaced and kept.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class SupplierEvaluationTests(LocalDbFixture db)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<T> AsAsync<T>(Func<TestScope, Task<T>> action)
    {
        await using var scope = db.Scope("buyer1");
        return await action(scope);
    }

    private async Task<int> SupplierAsync(bool certificate = true)
        => (await AsAsync(s => s.Get<SupplierRegistry>().SaveAsync(new SupplierInput(null, Unique("ev").ToUpperInvariant(), "ผู้ขายประเมิน", null, Certificate: certificate, UsedInProduction: true)))).Id!.Value;

    /// <summary>Receives one delivery of a new material from <paramref name="supplier"/> and, with <paramref name="lot"/>, lets QA mark it.</summary>
    private async Task ReceiveAsync(int supplier, DateOnly date, DateOnly? wanted, string? lot, bool failed = false, bool? certificate = null)
    {
        var material = (await AsAsync(s => s.Get<MaterialRegistry>().SaveAsync(new MaterialInput(null, Unique("em").ToUpperInvariant(), "วัตถุดิบประเมิน", null, "กก.", IsMain: false)))).Id!.Value;
        int department;
        await using (var scope = db.Scope("hr1"))
        {
            var org = scope.Get<OrgRegistry>();
            var section = (await org.SaveSectionAsync(new SectionInput(null, Unique("ฝ่าย"), 1, null))).Id!.Value;
            department = (await org.SaveDepartmentAsync(new DepartmentInput(null, Unique("D"), Unique("แผนก"), "d", 3, section, null, [], null))).Id!.Value;
        }
        var request = await AsAsync(s => s.Get<PurchaseRequestRegistry>().SaveAsync(new PurchaseRequestInput(Unique("V") + "69-0001", date, department, wanted, "F", null, null, [new PurchaseRequestLineInput(material, 5)])));
        var requestLine = (await AsAsync(s => s.Get<PurchaseRequestDirectory>().GetAsync(request.Id!.Value)))!.Lines.Single();
        var no = "69" + Random.Shared.Next(1000, 9999) + Guid.NewGuid().ToString("N")[..3];
        var order = await AsAsync(s => s.Get<PurchaseOrderRegistry>().IssueAsync(new PurchaseOrderInput(no, date, supplier, "ผู้ขายประเมิน", null, null, null, null, 30, true, "F", null, null, [new PurchaseOrderLineInput(requestLine.Id, 5m, 1m)])));
        var orderLine = (await AsAsync(s => s.Get<PurchaseOrderDirectory>().GetAsync(order.Id!.Value)))!.Lines.Single();
        await AsAsync(s => s.Get<GoodsReceiptRegistry>().ReceiveAsync([new GoodsReceiptInput(orderLine.Id, date, 5m)]));
        var receipt = (await AsAsync(s => s.Get<GoodsReceiptDirectory>().GetReceiptsAsync(orderLine.Id))).Single();
        await AsAsync(s => s.Get<GoodsReceiptRegistry>().SaveQaAsync(new GoodsReceiptQaUpdate(receipt.Id, receipt.RowVersion, lot, failed, certificate, null)));
    }

    private Task<SupplierEvaluationResult> EvaluateAsync(int supplier, DateOnly from, DateOnly to, int p1 = 5, int p2 = 5)
        => AsAsync(s => s.Get<SupplierEvaluationRegistry>().EvaluateAsync(new SupplierEvaluationInput(supplier, from, to, 2, p1, p2)));

    private static readonly DateOnly From = new(2029, 4, 1);
    private static readonly DateOnly To = new(2029, 6, 30);

    [Fact]
    public async Task Only_receipts_with_a_lot_are_counted_and_the_score_follows_BR_050()
    {
        var supplier = await SupplierAsync();
        await ReceiveAsync(supplier, new DateOnly(2029, 4, 10), new DateOnly(2029, 4, 12), "L1", certificate: true); // on time
        await ReceiveAsync(supplier, new DateOnly(2029, 5, 10), new DateOnly(2029, 5, 9), "L2", failed: true, certificate: false); // late, failed, no certificate
        await ReceiveAsync(supplier, new DateOnly(2029, 5, 20), new DateOnly(2029, 5, 25), null); // QA has not looked: not counted
        await ReceiveAsync(supplier, new DateOnly(2029, 7, 2), new DateOnly(2029, 7, 5), "L4", certificate: true); // outside the period

        var result = await EvaluateAsync(supplier, From, To, 4, 3);

        Assert.True(result.Succeeded);
        Assert.Equal(new SupplierCounts(1, 1, 1, 1, 1, 1), result.Counts);
        var score = result.Evaluation!.Score;
        Assert.Equal((4, 4, 4, 4, 3, 19), (score.Quality, score.Certificate, score.Delivery, score.Service1, score.Service2, score.Total));
        Assert.Equal((76m, "B"), (score.Percent, score.Grade));
    }

    [Fact]
    public async Task Evaluating_again_replaces_the_row_of_the_same_period_and_supplier()
    {
        var supplier = await SupplierAsync();
        await ReceiveAsync(supplier, new DateOnly(2029, 4, 10), new DateOnly(2029, 4, 12), "L1", certificate: true);
        await EvaluateAsync(supplier, From, To, 3, 3);
        await EvaluateAsync(supplier, From, To, 5, 5);

        var kept = await AsAsync(s => s.Get<SupplierEvaluationDirectory>().ListAsync(From, To, 2));
        var mine = Assert.Single(kept, e => e.SupplierId == supplier);
        Assert.Equal((5, 5), (mine.Point1, mine.Point2));
        Assert.Equal(mine.Id, (await AsAsync(s => s.Get<SupplierEvaluationDirectory>().GetAsync(supplier, From, To)))!.Id);
    }

    [Fact]
    public async Task A_supplier_with_nothing_counted_keeps_no_row_BR_050_4()
    {
        var supplier = await SupplierAsync();
        await ReceiveAsync(supplier, new DateOnly(2029, 4, 10), new DateOnly(2029, 4, 12), null); // received but QA has no lot yet
        var result = await EvaluateAsync(supplier, From, To);
        Assert.True(result.Succeeded);
        Assert.Null(result.Evaluation);
        Assert.True(result.Counts.IsEmpty);
        Assert.Null(await AsAsync(s => s.Get<SupplierEvaluationDirectory>().GetAsync(supplier, From, To)));
    }

    [Fact]
    public async Task A_supplier_without_a_certificate_gets_1_for_it_and_bad_input_is_refused()
    {
        var supplier = await SupplierAsync(certificate: false);
        await ReceiveAsync(supplier, new DateOnly(2029, 4, 10), new DateOnly(2029, 4, 12), "L1");
        var result = await EvaluateAsync(supplier, From, To);
        Assert.Equal(1, result.Evaluation!.Score.Certificate);

        Assert.Contains("ตั้งแต่วันที่ต้องไม่หลังถึงวันที่", (await EvaluateAsync(supplier, To, From)).Errors);
        Assert.Contains("คะแนนบริการต้องอยู่ระหว่าง 1 ถึง 5", (await EvaluateAsync(supplier, From, To, 6, 1)).Errors);
        Assert.Contains("ไม่พบผู้ขายที่เลือก", (await EvaluateAsync(int.MaxValue, From, To)).Errors);
    }
}

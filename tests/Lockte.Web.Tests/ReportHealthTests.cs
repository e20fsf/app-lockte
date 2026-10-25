using System.Net;
using System.Text;
using Lockte.Web.Reports;
using Microsoft.AspNetCore.Http;

namespace Lockte.Web.Tests;

/// <summary>The installer's print check (<see cref="ReportHealth"/>, #21): a real one-page PDF, only for the server itself.</summary>
[Collection(ChromiumCollection.Name)]
public sealed class ReportHealthTests(ChromiumFixture chromium)
{
    [Fact]
    public async Task The_check_page_prints_as_one_A4_page_with_the_Thai_font_embedded()
    {
        var pdf = Encoding.Latin1.GetString(await chromium.Renderer.RenderAsync(ReportHealth.Html(), Paper.A4Portrait));

        Assert.StartsWith("%PDF-", pdf);
        Assert.Contains("THSarabunNew", pdf);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(pdf, @"/Type /Page\b"));
    }

    [Theory] // no login on it, so the network must not reach it
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData(null, true)]
    [InlineData("172.31.255.20", false)]
    [InlineData("10.0.0.5", false)]
    public void Only_a_request_from_the_server_itself_is_served(string? remote, bool served)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = remote is null ? null : IPAddress.Parse(remote);

        Assert.Equal(served, ReportHealth.IsLocal(http));
    }
}

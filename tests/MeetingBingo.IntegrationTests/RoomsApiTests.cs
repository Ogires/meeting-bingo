using System.Net;

namespace MeetingBingo.IntegrationTests;

public sealed class RoomsApiTests(BingoApiFactory factory) : IClassFixture<BingoApiFactory>
{
    [Fact]
    public async Task The_liveness_probe_reports_healthy()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(ct));
    }
}

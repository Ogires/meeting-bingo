namespace MeetingBingo.IntegrationTests;

public sealed class SecurityHeadersTests(BingoApiFactory factory) : IClassFixture<BingoApiFactory>
{
    [Theory]
    [InlineData("/healthz")]
    [InlineData("/api/stats")]
    [InlineData("/api/nope")]
    public async Task The_security_headers_are_set_correctly(string uri)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        var response = await client.GetAsync(uri, ct);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("camera=(), microphone=(), geolocation=()", response.Headers.GetValues("Permissions-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.False(response.Headers.Contains("X-Robots-Tag"));
    }

    [Fact]
    public async Task Room_links_are_kept_from_indexing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/s/ABCDE", ct);
        Assert.Equal("noindex", response.Headers.GetValues("X-Robots-Tag").Single());
    }
}
using System.Net;
using System.Net.Http.Json;

using MeetingBingo.Api.Rooms;
using MeetingBingo.Core.Games;
using MeetingBingo.Core.Rooms;
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

    [Fact]
    public async Task Creating_a_room_returns_its_code_and_link()
    {

        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/rooms", null, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdRoom = await response.Content.ReadFromJsonAsync<CreatedRoom>(ct);
        Assert.True(RoomCode.TryNormalize(createdRoom?.Code, out _));
        Assert.Equal($"/s/{createdRoom?.Code}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Stats_are_empty_before_any_game()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/stats", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync<WeeklyStats>(ct);
        Assert.NotNull(stats);
        Assert.Empty(stats.Top);
    }

    [Fact]
    public async Task A_room_is_refused_when_the_limit_is_reached()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("Limits:MaxRooms", "1"));
        var ct = TestContext.Current.CancellationToken;
        using var client = limited.CreateClient();

        var response = await client.PostAsync("/api/rooms", null, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);


        var lastResponse = await client.PostAsync("/api/rooms", null, ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, lastResponse.StatusCode);
        Assert.Equal("application/problem+json", lastResponse.Content.Headers.ContentType?.MediaType);
    }
}
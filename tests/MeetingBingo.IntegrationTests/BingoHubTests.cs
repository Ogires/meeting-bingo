using MeetingBingo.Core.Rooms;

using Microsoft.AspNetCore.SignalR;

using Microsoft.AspNetCore.SignalR.Client;

namespace MeetingBingo.IntegrationTests;

public sealed class BingoHubTests(BingoApiFactory factory) : IClassFixture<BingoApiFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task The_second_player_is_announced_to_the_first()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = await factory.CreateRoomAsync(ct);
        await using var first = await factory.ConnectAsync(ct);
        await using var second = await factory.ConnectAsync(ct);

        // The event arrives on another thread: the test waits for this task, with a time limit
        var joined = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.On<string>("PlayerJoined", alias => joined.TrySetResult(alias));

        await first.InvokeAsync<PlayerSnapshot>("JoinRoom", code, null, ct);
        var secondPlayer = await second.InvokeAsync<PlayerSnapshot>("JoinRoom", code, null, ct);

        Assert.Equal(secondPlayer.Alias, await joined.Task.WaitAsync(EventTimeout, ct));
    }

    [Theory]
    [InlineData("ZZZZZ", "room_not_found")]
    [InlineData("abc", "room_not_found")]
    [InlineData("", "room_not_found")]
    public async Task Joining_a_nonexistent_room_fails(string code, string expectedError)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = await factory.ConnectAsync(ct);
        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync<PlayerSnapshot>("JoinRoom", code, null, ct));
        string actualError = ex.Message[^expectedError.Length..];
        Assert.Equal(expectedError, actualError);
    }

    [Fact]
    public async Task When_a_player_leaves_the_room_the_others_are_notified()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = await factory.CreateRoomAsync(ct);
        await using var first = await factory.ConnectAsync(ct);
        await using var second = await factory.ConnectAsync(ct);

        // The event arrives on another thread: the test waits for this task, with a time limit
        var left = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.On<string>("PlayerLeft", alias => left.TrySetResult(alias));

        await first.InvokeAsync<PlayerSnapshot>("JoinRoom", code, null, ct);
        var secondPlayer = await second.InvokeAsync<PlayerSnapshot>("JoinRoom", code, null, ct);
        await second.StopAsync(ct);

        Assert.Equal(secondPlayer.Alias, await left.Task.WaitAsync(EventTimeout, ct));
    }

    [Fact]
    public async Task A_player_can_rejoin_after_disconnecting()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = await factory.CreateRoomAsync(ct);

        await using var first = await factory.ConnectAsync(ct);
        var before = await first.InvokeAsync<PlayerSnapshot>("JoinRoom", code, null, ct);
        string oldToken = before.Token;

        await first.StopAsync(ct);
        await using var second = await factory.ConnectAsync(ct);
        var after = await second.InvokeAsync<PlayerSnapshot>("JoinRoom", code, oldToken, ct);
        Assert.Equivalent(before, after);
    }
}
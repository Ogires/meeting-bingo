using MeetingBingo.Core.Rooms;
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
}

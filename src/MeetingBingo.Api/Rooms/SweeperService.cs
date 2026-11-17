using MeetingBingo.Api.Hubs;
using MeetingBingo.Core.Games;
using MeetingBingo.Core.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace MeetingBingo.Api.Rooms;

/// <summary>Closes expired rooms every 30 seconds, and every open room on shutdown.</summary>
public sealed class SweeperService(
    RoomRegistry rooms, IGameRecorder recorder, IHubContext<BingoHub, IBingoClient> hub,
    TimeProvider time, ILogger<SweeperService> log) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // An exception escaping a BackgroundService stops the whole app: log it and try again on the next tick
            try
            {
                foreach (var closed in rooms.SweepExpired())
                    await CloseAsync(closed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Room sweep failed");
            }
        }
    }

    // On shutdown, close every room so its summary reaches the Recorder's last flush.
    // The Recorder (S3) is registered BEFORE this service, so the host stops it AFTER this one.
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        var closed = rooms.CloseAll();
        foreach (var room in closed)
            await CloseAsync(room);
        log.LogInformation("Closed {Rooms} open rooms on shutdown", closed.Count);
    }

    private async Task CloseAsync(ClosedRoom closed)
    {
        try { await hub.Clients.Group(closed.Room.Code).RoomClosed(); }
        catch (Exception ex) { log.LogWarning(ex, "Could not notify room {Code} that it closed", closed.Room.Code); }

        if (closed.Summary is { } summary && !recorder.TryEnqueue(summary))
            log.LogWarning("Game {GameId} could not be queued for recording", summary.GameId);
    }
}

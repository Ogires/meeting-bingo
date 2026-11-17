using MeetingBingo.Core.Games;

namespace MeetingBingo.Api.Recording;

/// <summary>Stand-in until the real Recorder (S3): it only logs each finished game.</summary>
public sealed class LoggingGameRecorder(ILogger<LoggingGameRecorder> log) : IGameRecorder
{
    public bool TryEnqueue(GameSummary summary)
    {
        log.LogInformation("Game {GameId} ended with {Players} players and {Bingos} lines (not recorded yet)",
            summary.GameId, summary.Players, summary.Bingos);
        return true;
    }
}

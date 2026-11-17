namespace MeetingBingo.Core.Games;

public interface IGameRecorder
{
    /// <summary>Queues a finished game for the next write window. Never blocks or touches the database.</summary>
    bool TryEnqueue(GameSummary summary);
}

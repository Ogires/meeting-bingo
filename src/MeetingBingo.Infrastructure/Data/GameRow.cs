namespace MeetingBingo.Infrastructure.Data;

/// <summary>One finished game. The id comes from the domain (<c>GameSummary.GameId</c>) and is the idempotency key.</summary>
public sealed class GameRow
{
    public Guid Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public int Players { get; set; }
    public int Bingos { get; set; }
    public List<PhraseMarkRow> Phrases { get; set; } = [];
}

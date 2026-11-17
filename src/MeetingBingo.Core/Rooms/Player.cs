namespace MeetingBingo.Core.Rooms;

internal sealed class Player(string token, string alias, Card card)
{
    public string Token { get; } = token;
    public string Alias { get; } = alias;
    public Card Card { get; } = card;
    public HashSet<int> Marked { get; } = [];
    public HashSet<string> AnnouncedLines { get; } = [];

    /// <summary>A reconnection can arrive before the old connection drops, so this is a count, not a flag.</summary>
    public int Connections { get; set; }
}

internal sealed class PhraseState
{
    /// <summary>Who has the phrase marked now, and since when.</summary>
    public Dictionary<string, DateTimeOffset> CurrentMarks { get; } = new(StringComparer.Ordinal);
    public HashSet<string> EverMarkedBy { get; } = new(StringComparer.Ordinal);
    public bool Confirmed { get; set; }
}

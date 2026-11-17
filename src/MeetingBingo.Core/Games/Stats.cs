namespace MeetingBingo.Core.Games;

public sealed record PhraseCount(int PhraseId, string Text, int Marks, int Confirmations);

public sealed record WeeklyStats(DateTimeOffset? ComputedAt, IReadOnlyList<PhraseCount> Top)
{
    public static WeeklyStats Empty { get; } = new(null, []);
}

/// <summary>Weekly stats pushed by the Recorder; reading it never touches the database.</summary>
public sealed class StatsSnapshot
{
    private WeeklyStats _current = WeeklyStats.Empty;

    // The whole object is swapped in one write, so readers see either the old stats or the new ones, never a mix
    public WeeklyStats Current => Volatile.Read(ref _current);

    public void Update(WeeklyStats stats) => Volatile.Write(ref _current, stats);
}

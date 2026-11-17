namespace MeetingBingo.Core.Games;

/// <summary>How many players marked a phrase during a game, and whether it got confirmed.</summary>
public sealed record PhraseTally(int PhraseId, int Marks, bool Confirmed);

/// <summary>The aggregate kept when a room closes: no aliases, tokens or IPs.</summary>
public sealed record GameSummary(
    Guid GameId, DateTimeOffset StartedAt, DateTimeOffset EndedAt,
    int Players, int Bingos, IReadOnlyList<PhraseTally> Phrases);

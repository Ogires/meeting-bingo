namespace MeetingBingo.Core.Rooms;

public enum JoinStatus { Joined, Rejoined, Full, Closed }

public sealed record JoinOutcome(JoinStatus Status, PlayerSnapshot? Player = null);

public enum ToggleStatus { Marked, Unmarked, InvalidCell, NotInRoom, Closed }

/// <summary>How many players have a phrase marked right now, and whether it has been confirmed.</summary>
public sealed record PhraseUpdate(int PhraseId, int Marks, bool Confirmed);

public sealed record ToggleOutcome(ToggleStatus Status, PhraseUpdate? Phrase, IReadOnlyList<string> NewLines, string? Alias)
{
    public static ToggleOutcome Rejected(ToggleStatus status) => new(status, null, [], null);
}

public sealed record CellView(int Index, int? PhraseId, string? Text, bool Marked);

/// <summary>Everything a player needs to draw their card and the room after joining.</summary>
public sealed record PlayerSnapshot(
    string Token, string Alias, IReadOnlyList<CellView> Cells,
    IReadOnlyList<string> Players, IReadOnlyList<PhraseUpdate> Phrases, IReadOnlyList<string> Lines);

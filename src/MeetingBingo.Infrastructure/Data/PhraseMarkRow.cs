namespace MeetingBingo.Infrastructure.Data;

/// <summary>How many players marked one phrase in one game. Only the phrase id is stored: ids are never reused.</summary>
public sealed class PhraseMarkRow
{
    public Guid GameId { get; set; }
    public int PhraseId { get; set; }
    public int Marks { get; set; }
    public bool Confirmed { get; set; }
}

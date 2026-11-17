using MeetingBingo.Core.Rooms;

namespace MeetingBingo.Api.Hubs;

public sealed record BingoAnnouncement(string Alias, string Line);

/// <summary>What the server can call on the browsers. A typed client turns a typo in a method name into a compile error.</summary>
public interface IBingoClient
{
    Task PlayerJoined(string alias);
    Task PlayerLeft(string alias);
    Task PhraseMarked(PhraseUpdate update);
    Task Bingo(BingoAnnouncement announcement);
    Task RoomClosed();
}

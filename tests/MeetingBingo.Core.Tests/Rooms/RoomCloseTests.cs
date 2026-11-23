using MeetingBingo.Core.Games;
using MeetingBingo.Core.Rooms;

namespace MeetingBingo.Core.Tests.Rooms;

public class RoomCloseTests
{
    [Fact]
    public void Closing_summarises_players_lines_and_phrases()
    {
        var (room, time) = RoomFactory.Create();
        var ana = room.Join(null).Player!;
        var bea = room.Join(null).Player!;
        var (anaCell, beaCell) = RoomFactory.SharedPhrase(ana, bea);
        room.Toggle(ana.Token, anaCell);
        room.Toggle(bea.Token, beaCell);
        var row = anaCell < Card.Size ? 1 : 0;            // a row without the shared cell or the free centre
        for (var i = row * Card.Size; i < (row + 1) * Card.Size; i++) room.Toggle(ana.Token, i);
        time.Advance(TimeSpan.FromMinutes(20));

        var summary = room.Close()!;

        Assert.Equal(7, summary.GameId.Version);
        Assert.Equal(RoomFactory.Start, summary.StartedAt);
        Assert.Equal(RoomFactory.Start.AddMinutes(20), summary.EndedAt);
        Assert.Equal(2, summary.Players);
        Assert.Equal(1, summary.Bingos);
        Assert.Contains(new PhraseTally(ana.Cells[anaCell].PhraseId!.Value, 2, true), summary.Phrases);
    }

    [Fact]
    public void A_room_nobody_joined_has_no_summary()
    {
        var (room, _) = RoomFactory.Create();
        Assert.Null(room.Close());
    }

    [Fact]
    public void A_closed_room_rejects_everything()
    {
        var (room, _) = RoomFactory.Create();
        var me = room.Join(null).Player!;
        room.Close();

        Assert.Null(room.Close());
        Assert.Equal(JoinStatus.Closed, room.Join(me.Token).Status);
        Assert.Equal(ToggleStatus.Closed, room.Toggle(me.Token, 0).Status);
    }
}
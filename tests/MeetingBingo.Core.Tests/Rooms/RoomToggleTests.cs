using MeetingBingo.Core.Rooms;

namespace MeetingBingo.Core.Tests.Rooms;

public class RoomToggleTests
{
    [Fact]
    public void Marking_a_cell_reports_its_phrase_with_one_mark()
    {
        var (room, _) = RoomFactory.Create();
        var me = room.Join(null).Player!;

        var outcome = room.Toggle(me.Token, 0);

        Assert.Equal(ToggleStatus.Marked, outcome.Status);
        Assert.Equal(new PhraseUpdate(me.Cells[0].PhraseId!.Value, 1, false), outcome.Phrase);
        Assert.Equal(me.Alias, outcome.Alias);
    }

    [Fact]
    public void Toggling_a_marked_cell_unmarks_it()
    {
        var (room, _) = RoomFactory.Create();
        var me = room.Join(null).Player!;
        room.Toggle(me.Token, 0);

        var outcome = room.Toggle(me.Token, 0);

        Assert.Equal(ToggleStatus.Unmarked, outcome.Status);
        Assert.Equal(0, outcome.Phrase!.Marks);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(Card.FreeIndex)]
    [InlineData(Card.CellCount)]
    public void The_free_centre_and_cells_out_of_range_are_rejected(int index)
    {
        var (room, _) = RoomFactory.Create();
        var me = room.Join(null).Player!;
        Assert.Equal(ToggleStatus.InvalidCell, room.Toggle(me.Token, index).Status);
    }

    [Fact]
    public void Unknown_tokens_are_not_in_the_room()
    {
        var (room, _) = RoomFactory.Create();
        Assert.Equal(ToggleStatus.NotInRoom, room.Toggle(PlayerToken.New(), 0).Status);
    }

    [Fact]
    public void Two_players_marking_the_same_phrase_within_60_seconds_confirm_it()
    {
        var (room, time) = RoomFactory.Create();
        var ana = room.Join(null).Player!;
        var bea = room.Join(null).Player!;
        var (anaCell, beaCell) = RoomFactory.SharedPhrase(ana, bea);

        room.Toggle(ana.Token, anaCell);
        time.Advance(Room.ConfirmationWindow);
        var outcome = room.Toggle(bea.Token, beaCell);

        Assert.Equal(2, outcome.Phrase!.Marks);
        Assert.True(outcome.Phrase.Confirmed);
    }

    [Fact]
    public void Marks_more_than_60_seconds_apart_do_not_confirm()
    {
        var (room, time) = RoomFactory.Create();
        var ana = room.Join(null).Player!;
        var bea = room.Join(null).Player!;
        var (anaCell, beaCell) = RoomFactory.SharedPhrase(ana, bea);

        room.Toggle(ana.Token, anaCell);
        time.Advance(Room.ConfirmationWindow + TimeSpan.FromSeconds(1));
        var outcome = room.Toggle(bea.Token, beaCell);

        Assert.Equal(2, outcome.Phrase!.Marks);
        Assert.False(outcome.Phrase.Confirmed);
    }

    [Fact]
    public void One_player_cannot_confirm_alone_by_marking_twice()
    {
        var (room, _) = RoomFactory.Create();
        var me = room.Join(null).Player!;
        room.Toggle(me.Token, 0);
        room.Toggle(me.Token, 0);

        Assert.False(room.Toggle(me.Token, 0).Phrase!.Confirmed);
    }

    [Fact]
    public void A_confirmed_phrase_stays_confirmed_after_an_unmark()
    {
        var (room, _) = RoomFactory.Create();
        var ana = room.Join(null).Player!;
        var bea = room.Join(null).Player!;
        var (anaCell, beaCell) = RoomFactory.SharedPhrase(ana, bea);
        room.Toggle(ana.Token, anaCell);
        room.Toggle(bea.Token, beaCell);

        var outcome = room.Toggle(ana.Token, anaCell);

        Assert.Equal(1, outcome.Phrase!.Marks);
        Assert.True(outcome.Phrase.Confirmed);
    }

    [Fact]
    public void Completing_a_row_announces_the_line_only_once()
    {
        var (room, _) = RoomFactory.Create();
        var me = room.Join(null).Player!;
        for (var i = 0; i < 4; i++) Assert.Empty(room.Toggle(me.Token, i).NewLines);

        Assert.Equal(new[] { "row-1" }, room.Toggle(me.Token, 4).NewLines);
        room.Toggle(me.Token, 4);                         // unmark
        Assert.Empty(room.Toggle(me.Token, 4).NewLines);  // mark again: already announced
    }

    [Fact]
    public void One_mark_can_complete_two_lines()
    {
        var (room, _) = RoomFactory.Create();
        var me = room.Join(null).Player!;
        foreach (var i in new[] { 1, 2, 3, 4, 5, 10, 15, 20 }) room.Toggle(me.Token, i);

        Assert.Equal(new[] { "row-1", "col-1" }, room.Toggle(me.Token, 0).NewLines);
    }

    [Fact]
    public void Rejoining_with_the_token_keeps_the_marks_and_the_phrase_state()
    {
        var (room, _) = RoomFactory.Create();
        var me = room.Join(null).Player!;
        room.Toggle(me.Token, 0);
        room.Leave(me.Token);

        var again = room.Join(me.Token).Player!;

        Assert.True(again.Cells[0].Marked);
        Assert.Contains(new PhraseUpdate(me.Cells[0].PhraseId!.Value, 1, false), again.Phrases);
    }
}
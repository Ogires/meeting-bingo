using MeetingBingo.Core.Rooms;

namespace MeetingBingo.Core.Tests.Rooms;

public class RoomJoinTests
{
    [Fact]
    public void A_new_player_gets_a_token_an_alias_and_a_card()
    {
        var (room, _) = RoomFactory.Create();

        var outcome = room.Join(token: null);

        Assert.Equal(JoinStatus.Joined, outcome.Status);
        var me = outcome.Player!;
        Assert.True(PlayerToken.IsValid(me.Token));
        Assert.False(string.IsNullOrWhiteSpace(me.Alias));
        Assert.Equal(Card.CellCount, me.Cells.Count);
        Assert.Null(me.Cells[Card.FreeIndex].PhraseId);
        Assert.All(me.Cells.Where(c => c.PhraseId is not null), c => Assert.False(string.IsNullOrEmpty(c.Text)));
        Assert.Equal(new[] { me.Alias }, me.Players);
    }

    [Fact]
    public void Rejoining_with_the_token_returns_the_same_card()
    {
        var (room, _) = RoomFactory.Create();
        var me = room.Join(null).Player!;
        room.Leave(me.Token);

        var again = room.Join(me.Token);

        Assert.Equal(JoinStatus.Rejoined, again.Status);
        Assert.Equal(me.Cells.Select(c => c.PhraseId), again.Player!.Cells.Select(c => c.PhraseId));
    }

    [Fact]
    public void An_unknown_token_joins_as_a_new_player()
    {
        var (room, _) = RoomFactory.Create();
        Assert.Equal(JoinStatus.Joined, room.Join(PlayerToken.New()).Status);
    }

    [Fact]
    public void The_room_turns_away_the_51st_player_but_lets_existing_players_back_in()
    {
        var (room, _) = RoomFactory.Create();
        var first = room.Join(null).Player!;
        for (var i = 1; i < Room.MaxPlayers; i++) room.Join(null);

        Assert.Equal(JoinStatus.Full, room.Join(null).Status);
        Assert.Equal(JoinStatus.Rejoined, room.Join(first.Token).Status);
    }

    [Fact]
    public void Aliases_are_unique_within_a_room()
    {
        var (room, _) = RoomFactory.Create();
        var aliases = Enumerable.Range(0, Room.MaxPlayers).Select(_ => room.Join(null).Player!.Alias).ToList();
        Assert.Equal(aliases.Count, aliases.Distinct().Count());
    }

    [Fact]
    public void Leaving_tells_who_left_and_the_others_see_the_list_change()
    {
        var (room, _) = RoomFactory.Create();
        var ana = room.Join(null).Player!;
        var bea = room.Join(null).Player!;

        Assert.Equal(ana.Alias, room.Leave(ana.Token));
        Assert.Equal(new[] { bea.Alias }, room.Join(bea.Token).Player!.Players);
    }

    [Fact]
    public void A_room_expires_five_minutes_after_it_empties()
    {
        var (room, time) = RoomFactory.Create();
        var me = room.Join(null).Player!;
        time.Advance(TimeSpan.FromMinutes(30));
        Assert.False(room.IsExpired(time.GetUtcNow()));

        room.Leave(me.Token);
        time.Advance(Room.EmptyTimeout - TimeSpan.FromSeconds(1));
        Assert.False(room.IsExpired(time.GetUtcNow()));
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(room.IsExpired(time.GetUtcNow()));
    }

    [Fact]
    public void A_room_nobody_joins_expires_after_five_minutes()
    {
        var (room, time) = RoomFactory.Create();
        time.Advance(Room.EmptyTimeout);
        Assert.True(room.IsExpired(time.GetUtcNow()));
    }

    [Fact]
    public void A_room_expires_after_three_hours_without_activity_even_with_players_connected()
    {
        var (room, time) = RoomFactory.Create();
        room.Join(null);
        time.Advance(Room.IdleTimeout - TimeSpan.FromSeconds(1));
        Assert.False(room.IsExpired(time.GetUtcNow()));
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(room.IsExpired(time.GetUtcNow()));
    }

    [Fact]
    public void A_reconnection_that_arrives_before_the_old_connection_drops_keeps_the_player_in()
    {
        var (room, _) = RoomFactory.Create();
        var me = room.Join(null).Player!;
        room.Join(me.Token);                 // the new connection arrives first

        Assert.Null(room.Leave(me.Token));   // the old one drops: nobody actually left
        Assert.Null(room.EmptySince);
    }
}
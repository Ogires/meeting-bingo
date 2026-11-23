using MeetingBingo.Core.Rooms;

using Microsoft.Extensions.Time.Testing;

namespace MeetingBingo.Core.Tests.Rooms;

public class RoomRegistryTests
{
    private static (RoomRegistry Registry, FakeTimeProvider Time) Create(int maxRooms = 500)
    {
        var time = new FakeTimeProvider(RoomFactory.Start);
        return (new RoomRegistry(TestCatalog.Create(30), time, maxRooms), time);
    }

    [Fact]
    public void Created_rooms_can_be_found_by_code()
    {
        var (registry, _) = Create();
        var room = registry.Create()!;
        Assert.Same(room, registry.Find(room.Code));
    }

    [Fact]
    public void No_more_rooms_are_created_at_the_cap()
    {
        var (registry, _) = Create(maxRooms: 2);
        registry.Create();
        registry.Create();

        Assert.Null(registry.Create());
        Assert.Equal(2, registry.OpenRooms);
    }

    [Fact]
    public void Sweeping_removes_expired_rooms_and_returns_their_summaries()
    {
        var (registry, time) = Create();
        var unused = registry.Create()!;
        var played = registry.Create()!;
        var me = played.Join(null).Player!;
        played.Toggle(me.Token, 0);
        played.Leave(me.Token);
        var active = registry.Create()!;
        active.Join(null);
        time.Advance(Room.EmptyTimeout);

        var closed = registry.SweepExpired();

        Assert.Equal(new[] { unused.Code, played.Code }.Order(), closed.Select(c => c.Room.Code).Order());
        Assert.Null(closed.Single(c => c.Room == unused).Summary);
        Assert.NotNull(closed.Single(c => c.Room == played).Summary);
        Assert.Null(registry.Find(played.Code));
        Assert.Same(active, registry.Find(active.Code));
    }

    [Fact]
    public void CloseAll_closes_every_open_room()
    {
        var (registry, _) = Create();
        registry.Create()!.Join(null);
        registry.Create();

        var closed = registry.CloseAll();

        Assert.Equal(2, closed.Count);
        Assert.Equal(0, registry.OpenRooms);
        Assert.Single(closed, c => c.Summary is not null);
    }
}
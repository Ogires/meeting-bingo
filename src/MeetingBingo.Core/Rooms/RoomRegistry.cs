using System.Collections.Concurrent;
using MeetingBingo.Core.Games;
using MeetingBingo.Core.Phrases;

namespace MeetingBingo.Core.Rooms;

public sealed record ClosedRoom(Room Room, GameSummary? Summary);

/// <summary>All open rooms, in memory. Exact limits are possible because there is a single replica.</summary>
public sealed class RoomRegistry(PhraseCatalog catalog, TimeProvider time, int maxRooms = 500)
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.Ordinal);

    public int OpenRooms => _rooms.Count;

    // The cap check and the insert are not atomic: a burst can overshoot by a few rooms, which is acceptable
    public Room? Create()
    {
        if (_rooms.Count >= maxRooms) return null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var room = new Room(RoomCode.New(), catalog, Random.Shared, time);
            if (_rooms.TryAdd(room.Code, room)) return room;
        }
        throw new InvalidOperationException("Could not find a free room code");
    }

    public Room? Find(string code) => _rooms.GetValueOrDefault(code);

    public IReadOnlyList<ClosedRoom> SweepExpired()
    {
        var now = time.GetUtcNow();
        return CloseWhere(room => room.IsExpired(now));
    }

    /// <summary>On shutdown: closes every room so its summary reaches the Recorder's last flush.</summary>
    public IReadOnlyList<ClosedRoom> CloseAll() => CloseWhere(_ => true);

    private List<ClosedRoom> CloseWhere(Func<Room, bool> predicate)
    {
        var closed = new List<ClosedRoom>();
        foreach (var room in _rooms.Values)
            if (predicate(room) && _rooms.TryRemove(room.Code, out _))
                closed.Add(new ClosedRoom(room, room.Close()));
        return closed;
    }
}

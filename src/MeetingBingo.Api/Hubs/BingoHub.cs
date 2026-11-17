using MeetingBingo.Core.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace MeetingBingo.Api.Hubs;

// Marking cells arrives in Task 14
public sealed class BingoHub(RoomRegistry rooms) : Hub<IBingoClient>
{
    private const string RoomKey = "room";
    private const string TokenKey = "token";

    public async Task<PlayerSnapshot> JoinRoom(string code, string? token)
    {
        // One room per connection: the connection remembers which room and player it belongs to
        if (Context.Items.ContainsKey(RoomKey)) throw new HubException("already_joined");
        if (!RoomCode.TryNormalize(code, out var normalized) || rooms.Find(normalized) is not { } room)
            throw new HubException("room_not_found");

        // A malformed token is treated as no token: the player simply joins as someone new
        var outcome = room.Join(PlayerToken.IsValid(token) ? token : null);
        var player = outcome.Status switch
        {
            JoinStatus.Joined or JoinStatus.Rejoined => outcome.Player!,
            JoinStatus.Full => throw new HubException("room_full"),
            _ => throw new HubException("room_not_found"),
        };

        Context.Items[RoomKey] = room;
        Context.Items[TokenKey] = player.Token;
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Code);
        // Clients treat the player list as a set, so a rejoin announced twice does no harm
        await Clients.OthersInGroup(room.Code).PlayerJoined(player.Alias);
        return player;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(RoomKey, out var value) && value is Room room
            && Context.Items[TokenKey] is string token && room.Leave(token) is { } alias)
            await Clients.Group(room.Code).PlayerLeft(alias);
        await base.OnDisconnectedAsync(exception);
    }
}

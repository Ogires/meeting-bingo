using Microsoft.AspNetCore.SignalR;

namespace MeetingBingo.Api.Hubs;

// Joining arrives in Task 10 and marking in Task 14
public sealed class BingoHub : Hub<IBingoClient>
{
}

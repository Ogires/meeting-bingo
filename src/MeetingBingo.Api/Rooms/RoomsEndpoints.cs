using MeetingBingo.Core.Games;
using MeetingBingo.Core.Rooms;

namespace MeetingBingo.Api.Rooms;

public sealed record CreatedRoom(string Code);

public static class RoomsEndpoints
{
    public static IEndpointRouteBuilder MapRoomsApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/rooms", (RoomRegistry rooms) =>
            rooms.Create() is { } room
                ? Results.Created($"/s/{room.Code}", new CreatedRoom(room.Code))
                : Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Hay demasiadas salas abiertas",
                    detail: "Prueba otra vez dentro de unos minutos."))
           .WithName("CreateRoom");

        // Served from memory: reading the stats never wakes the database
        api.MapGet("/stats", (StatsSnapshot stats) => stats.Current).WithName("GetStats");

        return app;
    }
}

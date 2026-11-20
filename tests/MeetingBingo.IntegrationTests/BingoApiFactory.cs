using System.Net.Http.Json;
using MeetingBingo.Api.Rooms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace MeetingBingo.IntegrationTests;

/// <summary>
/// Starts the whole API in memory (the real Program.cs, no network, no ports) and shares it
/// between the tests of a class. Each test creates its own room, so they don't depend on each other.
/// </summary>
public sealed class BingoApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The app's clock: tests move it forward instead of waiting.</summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // A fake connection string: nothing opens a connection to SQL yet, so the API starts without it
        builder.UseSetting("ConnectionStrings:bingo", "Server=unused;Database=bingo;Encrypt=false");

        // Runs after the app's own registrations, so these replace them
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    /// <summary>Creates a room through the API and returns its code.</summary>
    public async Task<string> CreateRoomAsync(CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.PostAsync("/api/rooms", content: null, ct);
        response.EnsureSuccessStatusCode();
        var room = await response.Content.ReadFromJsonAsync<CreatedRoom>(ct);
        return room!.Code;
    }

    /// <summary>A SignalR connection to the hub, already started. Dispose it with <c>await using</c>.</summary>
    public async Task<HubConnection> ConnectAsync(CancellationToken ct)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "hubs/bingo"), options =>
            {
                // The requests go to the in-memory server, not to the network
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                // Long polling is plain HTTP requests: the simplest transport against the TestServer
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        await connection.StartAsync(ct);
        return connection;
    }
}

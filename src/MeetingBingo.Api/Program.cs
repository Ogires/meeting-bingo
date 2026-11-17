using MeetingBingo.Api.Errors;
using MeetingBingo.Api.Hubs;
using MeetingBingo.Api.Recording;
using MeetingBingo.Api.Rooms;
using MeetingBingo.Core.Games;
using MeetingBingo.Core.Phrases;
using MeetingBingo.Core.Rooms;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(_ => PhraseCatalog.LoadEmbedded());
builder.Services.AddSingleton(sp => new RoomRegistry(
    sp.GetRequiredService<PhraseCatalog>(),
    sp.GetRequiredService<TimeProvider>(),
    sp.GetRequiredService<IConfiguration>().GetValue("Limits:MaxRooms", 500)));
builder.Services.AddSingleton<StatsSnapshot>();

// Order matters on shutdown: the host stops hosted services in reverse order, so the Recorder (S3)
// must be registered before the Sweeper to receive the summaries of the rooms closed on shutdown.
builder.Services.AddSingleton<IGameRecorder, LoggingGameRecorder>();
builder.Services.AddHostedService<SweeperService>();

builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 4 * 1024);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

// /healthz (liveness, never touches SQL) in every environment; the full /health report only in development
app.MapDefaultEndpoints();
app.MapRoomsApi();
app.MapHub<BingoHub>("/hubs/bingo");

app.Run();

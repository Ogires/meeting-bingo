using MeetingBingo.Api.Data;
using MeetingBingo.Api.Errors;
using MeetingBingo.Api.Hubs;
using MeetingBingo.Api.Recording;
using MeetingBingo.Api.Rooms;
using MeetingBingo.Api.Security;
using MeetingBingo.Core.Games;
using MeetingBingo.Core.Phrases;
using MeetingBingo.Core.Rooms;
using MeetingBingo.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(_ => PhraseCatalog.LoadEmbedded());
builder.Services.AddSingleton(sp => new RoomRegistry(
    sp.GetRequiredService<PhraseCatalog>(),
    sp.GetRequiredService<TimeProvider>(),
    sp.GetRequiredService<IConfiguration>().GetValue("Limits:MaxRooms", 500)));
builder.Services.AddSingleton<StatsSnapshot>();

// Retries come on by default. No health check, so /healthz never touches SQL.
// The connection string is only read when a DbContext is first needed: the API starts without SQL.
builder.AddSqlServerDbContext<BingoDbContext>("bingo", s => s.DisableHealthChecks = true);
if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    builder.Services.AddHostedService<LocalMigrationService>(); // local only: production migrates from the pipeline

// Order matters on shutdown: the host stops hosted services in reverse order, so the Recorder (S3)
// must be registered before the Sweeper to receive the summaries of the rooms closed on shutdown.
builder.Services.AddSingleton<IGameRecorder, LoggingGameRecorder>();
builder.Services.AddHostedService<SweeperService>();

builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 4 * 1024);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();

var app = builder.Build();

app.UseSecurityHeaders();
app.UseExceptionHandler();
// HSTS is only sent over HTTPS. Behind Azure's ingress the request arrives over HTTP until
// ForwardedHeaders (S4) restores the original scheme, so in production it starts working with S4.
if (!app.Environment.IsDevelopment())
    app.UseHsts();

// /healthz (liveness, never touches SQL) in every environment; the full /health report only in development
app.MapDefaultEndpoints();
app.MapRoomsApi();
app.MapHub<BingoHub>("/hubs/bingo");

// The Angular app, copied into wwwroot by the Dockerfile: files (with their precompressed versions),
// and index.html for the app's own routes (/s/ABCDE, /imprimir). An unknown API route is a 404, not the app.
app.MapStaticAssets();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

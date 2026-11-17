using MeetingBingo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MeetingBingo.Api.Data;

/// <summary>
/// Local development only (the AppHost sets Database:MigrateOnStartup): applies pending migrations in the
/// background, so the API starts without waiting for SQL. Production never does this: the pipeline runs a
/// migration bundle before the new revision starts.
/// </summary>
public sealed class LocalMigrationService(IServiceScopeFactory scopes, ILogger<LocalMigrationService> log) : BackgroundService
{
    private const int Attempts = 12;
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BingoDbContext>();
                await db.Database.MigrateAsync(stoppingToken);
                log.LogInformation("Local database is up to date");
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The SQL container may still be starting (the first run also downloads the image)
                log.LogWarning(ex, "Local migration attempt {Attempt} of {Attempts} failed", attempt, Attempts);
                await Task.Delay(Delay, stoppingToken);
            }
        }
        log.LogError("Local database was not migrated after {Attempts} attempts", Attempts);
    }
}

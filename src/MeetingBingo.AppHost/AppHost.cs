var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
                 .WithImageTag("2025-latest") // same engine as the Testcontainers tests, closest to Azure SQL
                 .WithDataVolume()
                 .AddDatabase("bingo");

// No WaitFor(sql): the API must start without SQL, exactly as in production
builder.AddProject<Projects.MeetingBingo_Api>("api")
       .WithReference(sql)
       .WithEnvironment("Database__MigrateOnStartup", "true"); // local only; production migrates from the pipeline

builder.Build().Run();

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
                 .WithImageTag("2025-latest") // same engine as the Testcontainers tests, closest to Azure SQL
                 .WithDataVolume()
                 .AddDatabase("bingo");

// No WaitFor(sql): the API must start without SQL, exactly as in production
var api = builder.AddProject<Projects.MeetingBingo_Api>("api")
                 .WithReference(sql)
                 .WithEnvironment("Database__MigrateOnStartup", "true"); // local only; production migrates from the pipeline

// Angular dev server on http://localhost:4200. Aspire passes the real port in PORT (read by web/start.mjs),
// and WithReference(api) gives web/proxy.conf.mjs the API's URL, so the browser sees a single origin.
builder.AddJavaScriptApp("web", "../../web", runScriptName: "start")
       .WithHttpEndpoint(port: 4200, env: "PORT")
       .WithReference(api)
       .WaitFor(api);

builder.Build().Run();

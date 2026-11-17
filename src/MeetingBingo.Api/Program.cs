var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();

// /healthz (liveness, never touches SQL) in every environment; the full /health report only in development
app.MapDefaultEndpoints();

app.Run();

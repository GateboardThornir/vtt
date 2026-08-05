using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Vtt.Server.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Before Build(): a missing connection string should stop the process here, not surface as a
// confusing failure on the first request that needs the database.
var connectionString = DatabaseConnectionString.Resolve(builder.Configuration);

builder.Services.AddVttDatabase(connectionString);

// CanConnectAsync against the registered context, run per request to /api/health — no background
// timer and no connection held open between probes.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<VttDbContext>("database");

var app = builder.Build();

var redactedConnectionString = DatabaseConnectionString.Redact(connectionString);
StartupLog.DatabaseConfigured(app.Logger, redactedConnectionString);

// 200 here now means "this instance can serve a request that touches the database", which is what
// a proxy or a container probe actually wants to know. The trade is that a 503 no longer
// distinguishes a dead process from an unreachable database, so the body names the failing check.
// Under /api so the Vite dev proxy needs one prefix (task 004) and Caddy one rule (task 101): a
// root-level path would be swallowed by the SPA's index.html fallback unless special-cased.
app.MapHealthChecks("/api/health", new HealthCheckOptions { ResponseWriter = HealthCheckResponse.Write });

app.Run();

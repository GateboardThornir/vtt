using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Vtt.Server.Accounts;
using Vtt.Server.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Before Build(): a missing connection string should stop the process here, not surface as a
// confusing failure on the first request that needs the database.
var connectionString = DatabaseConnectionString.Resolve(builder.Configuration);

// Application-wide rather than an Accounts concern, even though Accounts is currently its only
// consumer. Injecting the clock is what lets an expiry test move time instead of sleeping.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddVttDatabase(connectionString);
builder.Services.AddAccounts();
builder.Services.AddSessionCookie(builder.Environment.IsDevelopment());

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
// Serving is not the only thing this binary does. The branch is deliberately narrow — `dotnet ef`
// and the integration tests both execute this file, and neither passes arguments.
if (CreateAccountCommand.Matches(args))
{
    return await CreateAccountCommand.RunAsync(app.Services, args, Console.Out, ConsoleSecret.Read);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAccounts();
app.MapAccountAdministration();

// Under /api so the Vite dev proxy needs one prefix (task 004) and Caddy one rule (task 101): a
// root-level path would be swallowed by the SPA's index.html fallback unless special-cased.
app.MapHealthChecks("/api/health", new HealthCheckOptions { ResponseWriter = HealthCheckResponse.Write });

app.Run();

return 0;

// Top-level statements compile to an internal entry-point class, which WebApplicationFactory<T>
// cannot name. This declaration exists solely so the integration tests can boot the real
// application (task 005). Preferred over [assembly: InternalsVisibleTo], which would open every
// internal of this assembly to the test project and invite tests across module boundaries.
public partial class Program;

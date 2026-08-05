using Vtt.Server.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Before Build(): a missing connection string should stop the process here, not surface as a
// confusing failure on the first request that needs the database.
var connectionString = DatabaseConnectionString.Resolve(builder.Configuration);

builder.Services.AddVttDatabase(connectionString);

builder.Services.AddHealthChecks();

var app = builder.Build();

var redactedConnectionString = DatabaseConnectionString.Redact(connectionString);
StartupLog.DatabaseConfigured(app.Logger, redactedConnectionString);

// Health checks rather than a plain MapGet: task 002 needs a container probe and task 003 a
// database readiness check, and both attach here without changing the endpoint contract.
app.MapHealthChecks("/health");

app.Run();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

// Health checks rather than a plain MapGet: task 002 needs a container probe and task 003 a
// database readiness check, and both attach here without changing the endpoint contract.
app.MapHealthChecks("/health");

app.Run();

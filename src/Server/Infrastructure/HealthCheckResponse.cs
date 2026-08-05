using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Vtt.Server.Infrastructure;

public static class HealthCheckResponse
{
    /// <summary>
    /// Writes the aggregate status and the status of each registered check as JSON.
    /// </summary>
    /// <remarks>
    /// The default writer emits the aggregate status as a bare string, which says the server is
    /// unhealthy without saying what failed. Naming the failing check is what makes a 503 useful.
    /// <para>
    /// Statuses only: never the exception or the description. <c>/health</c> is unauthenticated,
    /// and an Npgsql connection failure carries the database host, port and username in its
    /// message — that is an information disclosure, not a diagnostic.
    /// </para>
    /// </remarks>
    public static Task Write(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Status.ToString()),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}

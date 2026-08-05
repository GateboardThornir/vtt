namespace Vtt.Server.Infrastructure;

public static class DatabaseConnectionString
{
    public const string ConfigurationKey = "ConnectionStrings:Default";

    /// <summary>
    /// Reads the database connection string from configuration, or throws.
    /// </summary>
    /// <remarks>
    /// Called before the host is built so a misconfigured environment stops startup immediately,
    /// rather than surfacing later as a connection failure on whatever request happens to be first.
    /// </remarks>
    public static string Resolve(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"No database connection string configured ({ConfigurationKey}, or " +
                "ConnectionStrings__Default in the environment). Copy .env.example to .env and " +
                "start the server with scripts/dev-server.sh.");
        }

        return connectionString;
    }

    /// <summary>
    /// Returns the connection string with any password value replaced by <c>***</c>.
    /// </summary>
    /// <remarks>
    /// Logging which host and database were configured is what makes a misconfiguration visible at
    /// startup; the password must not travel with it into a log sink.
    /// </remarks>
    public static string Redact(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < parts.Length; i++)
        {
            var separator = parts[i].IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            var key = parts[i][..separator].Trim();
            if (key.Equals("Password", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Pwd", StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = $"{key}=***";
            }
        }

        return string.Join(';', parts);
    }
}

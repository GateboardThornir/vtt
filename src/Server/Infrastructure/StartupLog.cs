namespace Vtt.Server.Infrastructure;

/// <remarks>
/// Source-generated log messages rather than <c>ILogger.LogInformation</c>: the generator emits a
/// strongly typed, allocation-free call site, which is what the analysers enforced by this build
/// require. Every log message the server emits follows this shape.
/// </remarks>
internal static partial class StartupLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Database configured: {ConnectionString}")]
    public static partial void DatabaseConfigured(ILogger logger, string connectionString);
}

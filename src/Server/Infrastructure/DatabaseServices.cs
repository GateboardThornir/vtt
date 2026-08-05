using Microsoft.EntityFrameworkCore;

namespace Vtt.Server.Infrastructure;

public static class DatabaseServices
{
    /// <summary>
    /// Registers <see cref="VttDbContext"/> with the provider and conventions the application uses.
    /// </summary>
    /// <remarks>
    /// The context is registered scoped, so each request gets its own: a <c>DbContext</c> owns a
    /// change tracker and is not thread-safe, and one shared instance would leak entities between
    /// requests. This matters again at task 060, where the table actor is long-lived and must
    /// resolve a context per unit of work rather than hold one.
    /// </remarks>
    public static IServiceCollection AddVttDatabase(
        this IServiceCollection services,
        string connectionString) =>
        services.AddDbContext<VttDbContext>(options => Configure(options, connectionString));

    /// <summary>
    /// Applies the provider and model conventions shared by the application and its tests.
    /// </summary>
    /// <remarks>
    /// Public so the migration-parity test builds the same model the server builds. A test that
    /// configured its own options would assert against a model that does not ship, which is
    /// precisely the drift it exists to catch.
    /// </remarks>
    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder options,
        string connectionString) =>
        options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
}

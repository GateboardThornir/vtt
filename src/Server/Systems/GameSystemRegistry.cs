using Vtt.Server.Systems.Dnd5e;

namespace Vtt.Server.Systems;

internal sealed class GameSystemRegistry : IGameSystemRegistry
{
    private readonly Dictionary<(string SystemId, string Version), IGameSystem> _modules;

    public GameSystemRegistry(IEnumerable<IGameSystem> modules)
    {
        _modules = modules.ToDictionary(module => (module.SystemId, module.Version));
        All = [.. _modules.Values];
    }

    public IReadOnlyList<IGameSystem> All { get; }

    public IGameSystem? Find(string systemId, string version) =>
        _modules.GetValueOrDefault((systemId, version));

    public bool IsKnown(string systemId, string version) => Find(systemId, version) is not null;
}

/// <summary>A registered module, as a client needs to see it.</summary>
public sealed record GameSystemSummary(string SystemId, string Version);

public static class GameSystemEndpoints
{
    /// <summary>
    /// Lists what a campaign may pin.
    /// </summary>
    /// <remarks>
    /// Exists because the campaign form previously asked people to type a system identifier and a
    /// version by hand, with no way to know what was valid. A pin that does not resolve produces a
    /// campaign in which no character can ever be created — a failure that surfaces much later than
    /// the mistake. Offering the registered modules removes the guess.
    /// </remarks>
    public static IEndpointRouteBuilder MapGameSystems(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet(
                "/api/systems",
                (IGameSystemRegistry registry) => registry.All
                    .Select(module => new GameSystemSummary(module.SystemId, module.Version))
                    .OrderBy(module => module.SystemId)
                    .ThenBy(module => module.Version))
            .RequireAuthorization(Accounts.AccountPolicies.ActiveAccount);

        return endpoints;
    }
}

public static class GameSystemServices
{
    public static IServiceCollection AddGameSystems(this IServiceCollection services) =>
        // Singleton: modules are stateless descriptions of a game's rules, and there is exactly one
        // set of them for the life of the process.
        services
            // Modules are compiled in and authored by the maintainer, so registration is a list
            // rather than a discovery protocol — and no third-party code needs sandboxing.
            .AddSingleton<IGameSystem, Dnd5eSystem>()
            .AddSingleton<IGameSystemRegistry, GameSystemRegistry>()

            // Singleton so the compiled-schema cache is shared rather than rebuilt per request.
            .AddSingleton<IDocumentValidator, DocumentValidator>();
}

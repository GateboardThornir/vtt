namespace Vtt.Server.Systems;

/// <summary>
/// Resolves a campaign's pinned <c>(SystemId, Version)</c> to the module that implements it.
/// </summary>
/// <remarks>
/// A lookup, not a plugin host. Modules are compiled in and authored by the maintainer, so there is
/// no discovery protocol to design and no third-party code to sandbox.
/// </remarks>
public interface IGameSystemRegistry
{
    /// <summary>The module for this pin, or null if nothing implements it.</summary>
    IGameSystem? Find(string systemId, string version);

    bool IsKnown(string systemId, string version);

    IReadOnlyList<IGameSystem> All { get; }
}

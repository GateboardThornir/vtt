namespace Vtt.Server.Campaigns;

/// <summary>
/// The long-lived unit of play: one Master, a roster, a pinned game system, a persistent world.
/// </summary>
/// <remarks>
/// The pinned <see cref="SystemId"/> and <see cref="SystemVersion"/> are the point of this type
/// existing this early. A campaign accumulating years of play must survive the evolution of the
/// system modules, and that is only possible if every campaign records which version it was built
/// against — from the very first one. Upgrading is a deliberate, migrated operation (task 080),
/// never something that happens because a newer module shipped.
/// </remarks>
public class Campaign
{
    public const int NameMaxLength = 80;

    public const int SystemIdMaxLength = 40;

    public const int SystemVersionMaxLength = 20;

    private Campaign()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string SystemId { get; private set; } = null!;

    public string SystemVersion { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <remarks>
    /// The system identifier and version are stored exactly as supplied and are not checked against
    /// anything: no registry exists until task 030. A hardcoded list here would be a second source
    /// of truth that 030 would then have to remove.
    /// </remarks>
    /// <remarks>
    /// Who masters it is not stored here. Task 021 moved that to the roster, because a column plus
    /// a membership table means two sources of truth for one fact, and they eventually disagree.
    /// </remarks>
    public static Campaign Create(
        string name,
        string systemId,
        string systemVersion,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            SystemId = systemId,
            SystemVersion = systemVersion,
            CreatedAt = now,
        };
}

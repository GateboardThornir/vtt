namespace Vtt.Server.Characters;

/// <summary>
/// A character in a campaign, owned by one account.
/// </summary>
/// <remarks>
/// The sheet is a document whose shape the platform does not know: it belongs to whichever game
/// system the campaign pinned. Everything structural about it lives in that module's schema.
/// </remarks>
public class Character
{
    public const int NameMaxLength = 80;

    private Character()
    {
    }

    public Guid Id { get; private set; }

    public Guid CampaignId { get; private set; }

    /// <summary>The account whose character this is. The Master may also edit it.</summary>
    public Guid OwnerUserId { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>The sheet, as the pinned module defines it. Stored as JSONB.</summary>
    public string Sheet { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Character Create(
        Guid campaignId,
        Guid ownerUserId,
        string name,
        string sheet,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CampaignId = campaignId,
            OwnerUserId = ownerUserId,
            Name = name,
            Sheet = sheet,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public void ReplaceSheet(string sheet, string name, DateTimeOffset now)
    {
        Sheet = sheet;
        Name = name;
        UpdatedAt = now;
    }
}

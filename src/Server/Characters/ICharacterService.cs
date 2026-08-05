using Vtt.Server.Systems;

namespace Vtt.Server.Characters;

public sealed record CharacterSummary(Guid Id, string Name, Guid OwnerUserId, DateTimeOffset UpdatedAt);

public sealed record CharacterDetail(
    Guid Id,
    string Name,
    Guid OwnerUserId,
    string Sheet,
    DateTimeOffset UpdatedAt);

public sealed record SaveCharacterRequest(string? Name, string? Sheet);

public enum CharacterOutcome
{
    Done,

    /// <summary>No such campaign or character, or the caller may not see it.</summary>
    NotVisible,

    /// <summary>Visible, but not yours to edit.</summary>
    NotYours,

    /// <summary>The sheet does not match the pinned system's schema.</summary>
    SheetInvalid,
}

public interface ICharacterService
{
    Task<IReadOnlyList<CharacterSummary>?> ForCampaignAsync(
        Guid campaignId,
        Guid callerId,
        CancellationToken cancellationToken = default);

    Task<CharacterDetail?> GetAsync(
        Guid campaignId,
        Guid characterId,
        Guid callerId,
        CancellationToken cancellationToken = default);

    Task<(CharacterOutcome Outcome, CharacterDetail? Character, IReadOnlyList<DocumentError> Errors)> CreateAsync(
        Guid campaignId,
        Guid callerId,
        string name,
        string sheet,
        CancellationToken cancellationToken = default);

    Task<(CharacterOutcome Outcome, CharacterDetail? Character, IReadOnlyList<DocumentError> Errors)> UpdateAsync(
        Guid campaignId,
        Guid characterId,
        Guid callerId,
        string name,
        string sheet,
        CancellationToken cancellationToken = default);
}

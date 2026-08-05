using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vtt.Server.Campaigns;
using Vtt.Server.Infrastructure;
using Vtt.Server.Systems;

namespace Vtt.Server.Characters;

internal sealed class CharacterService(
    VttDbContext context,
    ICampaignRoles roles,
    IGameSystemRegistry systems,
    IDocumentValidator validator,
    TimeProvider clock) : ICharacterService
{
    public async Task<IReadOnlyList<CharacterSummary>?> ForCampaignAsync(
        Guid campaignId,
        Guid callerId,
        CancellationToken cancellationToken = default)
    {
        if (await roles.RoleOfAsync(campaignId, callerId, cancellationToken) is null)
        {
            return null;
        }

        return await context.Set<Character>()
            .AsNoTracking()
            .Where(character => character.CampaignId == campaignId)
            .OrderBy(character => character.Name)
            .Select(character => new CharacterSummary(
                character.Id,
                character.Name,
                character.OwnerUserId,
                character.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CharacterDetail?> GetAsync(
        Guid campaignId,
        Guid characterId,
        Guid callerId,
        CancellationToken cancellationToken = default)
    {
        if (await roles.RoleOfAsync(campaignId, callerId, cancellationToken) is null)
        {
            return null;
        }

        return await context.Set<Character>()
            .AsNoTracking()
            .Where(character => character.Id == characterId && character.CampaignId == campaignId)
            .Select(character => new CharacterDetail(
                character.Id,
                character.Name,
                character.OwnerUserId,
                character.Sheet,
                character.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<(CharacterOutcome, CharacterDetail?, IReadOnlyList<DocumentError>)> CreateAsync(
        Guid campaignId,
        Guid callerId,
        string name,
        string sheet,
        CancellationToken cancellationToken = default)
    {
        if (await roles.RoleOfAsync(campaignId, callerId, cancellationToken) is null)
        {
            return (CharacterOutcome.NotVisible, null, []);
        }

        var prepared = await PrepareAsync(campaignId, sheet, cancellationToken);

        if (prepared.Outcome != CharacterOutcome.Done)
        {
            return (prepared.Outcome, null, prepared.Errors);
        }

        var character = Character.Create(campaignId, callerId, name.Trim(), prepared.Sheet!, clock.GetUtcNow());

        context.Set<Character>().Add(character);
        await context.SaveChangesAsync(cancellationToken);

        return (CharacterOutcome.Done, Detail(character), []);
    }

    public async Task<(CharacterOutcome, CharacterDetail?, IReadOnlyList<DocumentError>)> UpdateAsync(
        Guid campaignId,
        Guid characterId,
        Guid callerId,
        string name,
        string sheet,
        CancellationToken cancellationToken = default)
    {
        var role = await roles.RoleOfAsync(campaignId, callerId, cancellationToken);

        if (role is null)
        {
            return (CharacterOutcome.NotVisible, null, []);
        }

        var character = await context.Set<Character>()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == characterId && candidate.CampaignId == campaignId,
                cancellationToken);

        if (character is null)
        {
            return (CharacterOutcome.NotVisible, null, []);
        }

        // Your own character, or anything in a campaign you master. Both answers come from the
        // roster resolver rather than from a column comparison scattered here.
        if (character.OwnerUserId != callerId && role != CampaignRole.Master)
        {
            return (CharacterOutcome.NotYours, null, []);
        }

        var prepared = await PrepareAsync(campaignId, sheet, cancellationToken);

        if (prepared.Outcome != CharacterOutcome.Done)
        {
            return (prepared.Outcome, null, prepared.Errors);
        }

        character.ReplaceSheet(prepared.Sheet!, name.Trim(), clock.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);

        return (CharacterOutcome.Done, Detail(character), []);
    }

    /// <summary>
    /// Validates a sheet against the campaign's pinned module, then recomputes its derived values.
    /// </summary>
    /// <remarks>
    /// Every write goes through here, which is the point: <c>.claude/rules/game-systems.md</c>
    /// requires <c>RecomputeDerived</c> after **every** sheet write, and putting it at each call
    /// site is how one path eventually forgets and the derived values quietly rot.
    /// <para>
    /// The module is the one the *campaign pinned*, never the newest available. Reaching for the
    /// latest here would undo version pinning silently, which is the failure the whole discipline
    /// exists to prevent.
    /// </para>
    /// <para>
    /// A client may send its own <c>derived</c> object; it is overwritten. What is stored is always
    /// the module's arithmetic, never the caller's claim about it.
    /// </para>
    /// </remarks>
    private async Task<(CharacterOutcome Outcome, string? Sheet, IReadOnlyList<DocumentError> Errors)> PrepareAsync(
        Guid campaignId,
        string sheet,
        CancellationToken cancellationToken)
    {
        var pin = await context.Set<Campaign>()
            .AsNoTracking()
            .Where(campaign => campaign.Id == campaignId)
            .Select(campaign => new { campaign.SystemId, campaign.SystemVersion })
            .SingleAsync(cancellationToken);

        var module = systems.Find(pin.SystemId, pin.SystemVersion);

        if (module is null)
        {
            return (
                CharacterOutcome.SheetInvalid,
                null,
                [new DocumentError("/", $"No module implements {pin.SystemId} {pin.SystemVersion}.")]);
        }

        SheetDocument document;

        try
        {
            document = SheetDocument.Parse(sheet);
        }
        catch (JsonException exception)
        {
            return (CharacterOutcome.SheetInvalid, null, [new DocumentError("/", exception.Message)]);
        }

        var validation = validator.ValidateSheet(module, document);

        if (!validation.IsValid)
        {
            return (CharacterOutcome.SheetInvalid, null, validation.Errors);
        }

        return (CharacterOutcome.Done, module.RecomputeDerived(document).ToString(), []);
    }

    private static CharacterDetail Detail(Character character) =>
        new(character.Id, character.Name, character.OwnerUserId, character.Sheet, character.UpdatedAt);
}

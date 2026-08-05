using System.Security.Claims;
using Vtt.Server.Accounts;

namespace Vtt.Server.Characters;

public static class CharacterEndpoints
{
    public static IEndpointRouteBuilder MapCharacters(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/campaigns/{campaignId:guid}/characters")
            .RequireAuthorization(AccountPolicies.ActiveAccount);

        group.MapGet("/", ListAsync);
        group.MapGet("/{characterId:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{characterId:guid}", UpdateAsync);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid campaignId,
        ClaimsPrincipal principal,
        ICharacterService characters,
        CancellationToken cancellationToken)
    {
        var found = await characters.ForCampaignAsync(campaignId, Caller(principal), cancellationToken);

        return found is null ? Results.NotFound() : Results.Ok(found);
    }

    private static async Task<IResult> GetAsync(
        Guid campaignId,
        Guid characterId,
        ClaimsPrincipal principal,
        ICharacterService characters,
        CancellationToken cancellationToken)
    {
        var found = await characters.GetAsync(campaignId, characterId, Caller(principal), cancellationToken);

        return found is null ? Results.NotFound() : Results.Ok(found);
    }

    private static async Task<IResult> CreateAsync(
        Guid campaignId,
        SaveCharacterRequest request,
        ClaimsPrincipal principal,
        ICharacterService characters,
        CancellationToken cancellationToken)
    {
        if (!IsWellFormed(request))
        {
            return Results.BadRequest(new { error = "name_or_sheet_missing" });
        }

        var (outcome, character, errors) = await characters.CreateAsync(
            campaignId,
            Caller(principal),
            request.Name!,
            request.Sheet!,
            cancellationToken);

        return outcome == CharacterOutcome.Done
            ? Results.Created($"/api/campaigns/{campaignId}/characters/{character!.Id}", character)
            : Translate(outcome, errors);
    }

    private static async Task<IResult> UpdateAsync(
        Guid campaignId,
        Guid characterId,
        SaveCharacterRequest request,
        ClaimsPrincipal principal,
        ICharacterService characters,
        CancellationToken cancellationToken)
    {
        if (!IsWellFormed(request))
        {
            return Results.BadRequest(new { error = "name_or_sheet_missing" });
        }

        var (outcome, character, errors) = await characters.UpdateAsync(
            campaignId,
            characterId,
            Caller(principal),
            request.Name!,
            request.Sheet!,
            cancellationToken);

        return outcome == CharacterOutcome.Done ? Results.Ok(character) : Translate(outcome, errors);
    }

    private static bool IsWellFormed(SaveCharacterRequest request) =>
        !string.IsNullOrWhiteSpace(request.Name) &&
        request.Name.Trim().Length <= Character.NameMaxLength &&
        !string.IsNullOrWhiteSpace(request.Sheet);

    private static Guid Caller(ClaimsPrincipal principal) => SessionCookie.UserIdOf(principal)!.Value;

    /// <remarks>
    /// The schema errors are returned to the caller because they are about the document they just
    /// sent, and a path like <c>/abilities/strength</c> is the difference between a fixable mistake
    /// and a shrug. They describe shape only and disclose nothing about anyone else's data.
    /// </remarks>
    private static IResult Translate(CharacterOutcome outcome, IReadOnlyList<Systems.DocumentError> errors) =>
        outcome switch
        {
            CharacterOutcome.NotVisible => Results.NotFound(),
            CharacterOutcome.NotYours => Results.Forbid(),
            _ => Results.BadRequest(new { error = "sheet_invalid", errors }),
        };
}

public static class CharacterServices
{
    public static IServiceCollection AddCharacters(this IServiceCollection services) =>
        services.AddScoped<ICharacterService, CharacterService>();
}

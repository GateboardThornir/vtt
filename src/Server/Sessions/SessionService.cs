using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vtt.Server.Campaigns;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Sessions;

internal sealed class SessionService(
    VttDbContext context,
    ICampaignRoles roles,
    TimeProvider clock) : ISessionService
{
    public async Task<IReadOnlyList<PlaySessionView>?> ForCampaignAsync(
        Guid campaignId,
        Guid callerId,
        CancellationToken cancellationToken = default)
    {
        // A session's visibility is its campaign's — no new rule, just the resolver from 021.
        if (await roles.RoleOfAsync(campaignId, callerId, cancellationToken) is null)
        {
            return null;
        }

        return await context.Set<PlaySession>()
            .AsNoTracking()
            .Where(session => session.CampaignId == campaignId)
            .OrderBy(session => session.CreatedAt)
            .Select(session => new PlaySessionView(
                session.Id,
                session.Title,
                session.State,
                session.CreatedAt,
                session.OpenedAt,
                session.ClosedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<(SessionOutcome Outcome, PlaySessionView? Session)> CreateAsync(
        Guid campaignId,
        Guid callerId,
        string title,
        CancellationToken cancellationToken = default)
    {
        var guard = await RequireMasterAsync(campaignId, callerId, cancellationToken);

        if (guard != SessionOutcome.Done)
        {
            return (guard, null);
        }

        var session = PlaySession.Plan(campaignId, title.Trim(), clock.GetUtcNow());

        context.Set<PlaySession>().Add(session);
        await context.SaveChangesAsync(cancellationToken);

        return (
            SessionOutcome.Done,
            new PlaySessionView(session.Id, session.Title, session.State, session.CreatedAt, null, null));
    }

    public async Task<SessionOutcome> SetStateAsync(
        Guid campaignId,
        Guid sessionId,
        Guid callerId,
        SessionState state,
        CancellationToken cancellationToken = default)
    {
        var guard = await RequireMasterAsync(campaignId, callerId, cancellationToken);

        if (guard != SessionOutcome.Done)
        {
            return guard;
        }

        var session = await context.Set<PlaySession>()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == sessionId && candidate.CampaignId == campaignId,
                cancellationToken);

        if (session is null)
        {
            return SessionOutcome.NotVisible;
        }

        if (!session.TransitionTo(state, clock.GetUtcNow()))
        {
            return SessionOutcome.NotAllowed;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A second session opened while this one was being opened. The partial unique index is
            // what actually decides; checking for an open session first would leave a window.
            return SessionOutcome.NotAllowed;
        }

        return SessionOutcome.Done;
    }

    private async Task<SessionOutcome> RequireMasterAsync(
        Guid campaignId,
        Guid callerId,
        CancellationToken cancellationToken) =>
        await roles.RoleOfAsync(campaignId, callerId, cancellationToken) switch
        {
            null => SessionOutcome.NotVisible,
            CampaignRole.Master => SessionOutcome.Done,
            _ => SessionOutcome.NotTheMaster,
        };
}

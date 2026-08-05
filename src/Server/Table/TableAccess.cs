using Microsoft.EntityFrameworkCore;
using Vtt.Server.Accounts;
using Vtt.Server.Campaigns;
using Vtt.Server.Infrastructure;
using Vtt.Server.Sessions;

namespace Vtt.Server.Table;

internal sealed class TableAccess(VttDbContext context, ICampaignRoles roles) : ITableAccess
{
    public async Task<Participant?> AdmitAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (await CampaignOfAsync(sessionId, userId, cancellationToken) is null)
        {
            return null;
        }

        var username = await context.Set<User>()
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.Username)
            .SingleOrDefaultAsync(cancellationToken);

        return username is null ? null : new Participant(userId, username);
    }

    public async Task<Guid?> CampaignOfAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Open only. A planned session has not started and a closed one is history; neither has a
        // live audience, and joining either would be joining a table that is not there.
        var session = await context.Set<PlaySession>()
            .AsNoTracking()
            .Where(candidate => candidate.Id == sessionId && candidate.State == SessionState.Open)
            .Select(candidate => new { candidate.CampaignId })
            .SingleOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return null;
        }

        // The same resolver the HTTP side uses. A stranger gets null here and learns nothing about
        // whether the session exists.
        return await roles.RoleOfAsync(session.CampaignId, userId, cancellationToken) is null
            ? null
            : session.CampaignId;
    }
}

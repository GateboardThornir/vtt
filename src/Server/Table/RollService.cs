using Microsoft.EntityFrameworkCore;
using Vtt.Server.Accounts;
using Vtt.Server.Campaigns;
using Vtt.Server.Dice;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Table;

internal sealed class RollService(
    VttDbContext context,
    ITableAccess access,
    ICampaignRoles roles,
    IDiceRoller dice,
    TimeProvider clock) : IRollService
{
    public async Task<RollBroadcast?> RollAsync(
        Guid sessionId,
        Guid rollerUserId,
        string expression,
        RollVisibility visibility,
        CancellationToken cancellationToken = default)
    {
        var campaignId = await access.CampaignOfAsync(sessionId, rollerUserId, cancellationToken);

        if (campaignId is null)
        {
            return null;
        }

        var role = await roles.RoleOfAsync(campaignId.Value, rollerUserId, cancellationToken);

        // A Master-only roll is the Master's secret check. A player asking for one would be hiding
        // a roll from the person running the game, which is not a thing the table means.
        if (visibility == RollVisibility.MasterOnly && role != CampaignRole.Master)
        {
            return null;
        }

        var result = dice.Roll(expression);

        if (result is null)
        {
            return null;
        }

        var roll = Roll.Record(sessionId, rollerUserId, result, visibility, clock.GetUtcNow());

        context.Set<Roll>().Add(roll);
        await context.SaveChangesAsync(cancellationToken);

        var username = await UsernameOf(rollerUserId, cancellationToken);
        var recipients = await RecipientsOf(campaignId.Value, rollerUserId, visibility, cancellationToken);

        return new RollBroadcast(Line(roll, rollerUserId, username, result.Kept, result.Dropped), recipients);
    }

    public async Task<IReadOnlyList<RollLine>?> HistoryAsync(
        Guid sessionId,
        Guid callerId,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var campaignId = await access.CampaignOfAsync(sessionId, callerId, cancellationToken);

        if (campaignId is null)
        {
            return null;
        }

        var isMaster = await roles.IsMasterAsync(campaignId.Value, callerId, cancellationToken);

        // Filtered in the query. A reconnecting player must not learn of a roll that was hidden
        // from them while they were away, and fetching everything to filter afterwards is how one
        // day it gets sent.
        var rolls = await (from roll in context.Set<Roll>().AsNoTracking()
                           join roller in context.Set<User>().AsNoTracking()
                               on roll.RollerUserId equals roller.Id
                           where roll.SessionId == sessionId &&
                                 (roll.Visibility == RollVisibility.Public ||
                                  isMaster ||
                                  (roll.Visibility == RollVisibility.Private && roll.RollerUserId == callerId))
                           orderby roll.CreatedAt descending
                           select new
                           {
                               roll.Id,
                               roll.RollerUserId,
                               roller.Username,
                               roll.Expression,
                               roll.Kept,
                               roll.Dropped,
                               roll.Modifier,
                               roll.Total,
                               roll.Visibility,
                               roll.CreatedAt,
                           })
            .Take(limit)
            .ToListAsync(cancellationToken);

        rolls.Reverse();

        return [.. rolls.Select(roll => new RollLine(
            roll.Id,
            roll.RollerUserId,
            roll.Username,
            roll.Expression,
            Faces(roll.Kept),
            Faces(roll.Dropped),
            roll.Modifier,
            roll.Total,
            roll.Visibility,
            roll.CreatedAt))];
    }

    /// <remarks>
    /// The whole point of the task. A hidden roll produces **no event** for the people excluded —
    /// not a redacted one and not a placeholder, because "somebody rolled something" still tells a
    /// player the Master is checking, which is exactly what a secret roll withholds.
    /// </remarks>
    private async Task<IReadOnlyList<Guid>> RecipientsOf(
        Guid campaignId,
        Guid rollerUserId,
        RollVisibility visibility,
        CancellationToken cancellationToken)
    {
        var masters = await context.Set<CampaignMember>()
            .AsNoTracking()
            .Where(member =>
                member.CampaignId == campaignId &&
                member.State == MembershipState.Active &&
                member.Role == CampaignRole.Master)
            .Select(member => member.UserId)
            .ToListAsync(cancellationToken);

        return visibility switch
        {
            RollVisibility.Public => await context.Set<CampaignMember>()
                .AsNoTracking()
                .Where(member => member.CampaignId == campaignId && member.State == MembershipState.Active)
                .Select(member => member.UserId)
                .ToListAsync(cancellationToken),

            // The roller and the Master, and nobody else at all.
            RollVisibility.Private => [.. masters.Append(rollerUserId).Distinct()],

            _ => masters,
        };
    }

    private Task<string> UsernameOf(Guid userId, CancellationToken cancellationToken) =>
        context.Set<User>()
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.Username)
            .SingleAsync(cancellationToken);

    private static RollLine Line(
        Roll roll,
        Guid rollerUserId,
        string username,
        IReadOnlyList<int> kept,
        IReadOnlyList<int> dropped) =>
        new(
            roll.Id,
            rollerUserId,
            username,
            roll.Expression,
            kept,
            dropped,
            roll.Modifier,
            roll.Total,
            roll.Visibility,
            roll.CreatedAt);

    private static IReadOnlyList<int> Faces(string packed) =>
        packed.Length == 0
            ? []
            : [.. packed.Split(',').Select(face => int.Parse(face, System.Globalization.CultureInfo.InvariantCulture))];
}

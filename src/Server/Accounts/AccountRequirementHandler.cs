using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Accounts;

/// <summary>
/// Decides <see cref="AccountRequirement"/> by reading the account as it is right now.
/// </summary>
/// <remarks>
/// A database read per authorised request, deliberately. The session cookie is signed once and then
/// frozen until it is reissued, so any role or state carried inside it would keep working after it
/// had been revoked — an administrator demoted this morning would still be one until they next
/// happened to sign in. At fewer than fifty users, one indexed lookup is the right price for
/// revocation that takes effect on the next request.
/// <para>
/// Fails closed: an unparseable claim, a missing account or a non-active one is a refusal.
/// </para>
/// </remarks>
internal sealed class AccountRequirementHandler(VttDbContext context)
    : AuthorizationHandler<AccountRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext authorization,
        AccountRequirement requirement)
    {
        var id = SessionCookie.UserIdOf(authorization.User);

        if (id is null)
        {
            return;
        }

        var account = await context.Set<User>()
            .AsNoTracking()
            .Where(user => user.Id == id.Value)
            .Select(user => new { user.State, user.Role })
            .SingleOrDefaultAsync();

        if (account is null || account.State != AccountState.Active)
        {
            // Closes the gap 013 left: disabling an account stopped it signing in, but the cookie
            // it already held kept working until it expired.
            return;
        }

        if (requirement.MustBeAdministrator && account.Role != PlatformRole.Admin)
        {
            return;
        }

        authorization.Succeed(requirement);
    }
}

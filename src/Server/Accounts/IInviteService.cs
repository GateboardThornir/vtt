namespace Vtt.Server.Accounts;

/// <summary>
/// Issues, checks and spends invite tokens.
/// </summary>
/// <remarks>
/// This interface says nothing about who is allowed to call it. The authorisation policy — that
/// only an administrator may issue an invite — is task 016's, applied at the endpoint. Here
/// <paramref name="createdByUserId"/> is simply recorded.
/// </remarks>
public interface IInviteService
{
    /// <summary>Creates an invite and returns its token, which is not recoverable afterwards.</summary>
    Task<IssuedInvite> IssueAsync(Guid createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>Reports whether a token could be spent right now, without spending it.</summary>
    Task<InviteStatus> ValidateAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spends a token on behalf of a newly created account.
    /// </summary>
    /// <returns>
    /// <see cref="InviteStatus.Ok"/> only if this call is the one that spent it. Two concurrent
    /// callers cannot both receive it.
    /// </returns>
    Task<InviteStatus> ConsumeAsync(
        string token,
        Guid consumedByUserId,
        CancellationToken cancellationToken = default);
}

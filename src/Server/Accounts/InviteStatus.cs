namespace Vtt.Server.Accounts;

/// <summary>
/// What happened when an invite token was checked or spent.
/// </summary>
/// <remarks>
/// One vocabulary for both operations: from a check, <see cref="Ok"/> means the token is usable;
/// from a redemption, it means this call is the one that spent it.
/// <para>
/// Not a boolean, because "no such token", "expired" and "already used" are different facts and
/// task 012 will want to respond to them differently. How much of that distinction is safe to tell
/// an unauthenticated stranger is 012's decision, and the safe default is very little — but the
/// service has to know before the endpoint can choose.
/// </para>
/// </remarks>
public enum InviteStatus
{
    Ok,
    NotFound,
    Expired,
    AlreadyConsumed,
}

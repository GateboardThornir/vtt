namespace Vtt.Server.Notifications;

/// <summary>
/// What happened. The client turns this into a sentence in the reader's language.
/// </summary>
/// <remarks>
/// A kind and its parameters, never prose. The interface is bilingual, so an English sentence
/// composed on the server arrives at the client untranslatable — the same reasoning that made task
/// 012 return error codes rather than messages.
/// </remarks>
public enum NotificationKind
{
    CampaignInvitation,
    AccountApproved,
    AccountRejected,
}

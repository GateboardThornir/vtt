namespace Vtt.Server.Campaigns;

/// <summary>
/// What an account is <em>within one campaign</em>.
/// </summary>
/// <remarks>
/// Unrelated to <c>PlatformRole</c>, and deliberately so: the same person is Master of one campaign
/// and Player in another, and a platform administrator is nothing at all to a campaign they are not
/// on. Conflating the two is how an authorisation model becomes unfixable.
/// </remarks>
public enum CampaignRole
{
    Master,
    Player,
}

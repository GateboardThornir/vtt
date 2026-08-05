namespace Vtt.Server.Campaigns;

/// <summary>Where a roster entry sits between being asked and being gone.</summary>
/// <remarks>
/// A row in the membership table is not membership. <see cref="Invited"/> confers no access to
/// anything the campaign contains — treating any row as access is the easiest mistake here.
/// </remarks>
public enum MembershipState
{
    Invited,
    Active,
    Declined,
    Left,
}

namespace Vtt.Server.Table;

/// <summary>
/// Whether an account may take part in a session's live table.
/// </summary>
/// <remarks>
/// A hub method is a public endpoint with no visible URL, which makes it easier to forget to
/// authorise than an HTTP route. Every entry point asks this, and it answers with the same rules
/// the HTTP side uses: 021's roster resolver, plus the session actually being open.
/// </remarks>
public interface ITableAccess
{
    /// <summary>The participant, or null if this account may not be at that table.</summary>
    Task<Participant?> AdmitAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The campaign a session belongs to, or null if the caller may not see it.</summary>
    Task<Guid?> CampaignOfAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
}

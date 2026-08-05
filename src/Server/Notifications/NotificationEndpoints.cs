using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Vtt.Server.Accounts;

namespace Vtt.Server.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotifications(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/notifications")
            .RequireAuthorization(AccountPolicies.ActiveAccount);

        group.MapGet("/", ListAsync);
        group.MapPost("/{id:guid}/read", MarkReadAsync);
        group.MapPost("/read", MarkAllReadAsync);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<NotificationView>>> ListAsync(
        ClaimsPrincipal principal,
        INotificationService notifications,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await notifications.ForAsync(Caller(principal), cancellationToken));

    /// <remarks>
    /// A notification belonging to somebody else is a 404: confirming it exists would say that
    /// something happened to another account.
    /// </remarks>
    private static async Task<Results<NoContent, NotFound>> MarkReadAsync(
        Guid id,
        ClaimsPrincipal principal,
        INotificationService notifications,
        CancellationToken cancellationToken) =>
        await notifications.MarkReadAsync(Caller(principal), id, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();

    private static async Task<NoContent> MarkAllReadAsync(
        ClaimsPrincipal principal,
        INotificationService notifications,
        CancellationToken cancellationToken)
    {
        await notifications.MarkAllReadAsync(Caller(principal), cancellationToken);

        return TypedResults.NoContent();
    }

    private static Guid Caller(ClaimsPrincipal principal) => SessionCookie.UserIdOf(principal)!.Value;
}

public static class NotificationServices
{
    public static IServiceCollection AddNotifications(this IServiceCollection services) =>
        services.AddScoped<INotificationService, NotificationService>();
}

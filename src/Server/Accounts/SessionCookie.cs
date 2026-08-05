using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Vtt.Server.Accounts;

/// <summary>
/// How a signed-in identity is carried between requests.
/// </summary>
/// <remarks>
/// The cookie is a signed and encrypted payload the browser stores and returns, rather than an
/// identifier pointing at a row somewhere. At fewer than fifty users there is nothing a session
/// table would buy that is worth the extra read on every request.
/// <para>
/// It carries identity and nothing else. No roles, no campaign membership, nothing an authorisation
/// decision depends on — a cookie is frozen until it is reissued, so a permission revoked today
/// would keep working until the holder happened to sign in again.
/// </para>
/// </remarks>
public static class SessionCookie
{
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;

    public const string Name = "vtt_session";

    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    public static void Configure(CookieAuthenticationOptions options, bool isDevelopment)
    {
        options.Cookie.Name = Name;

        // Unreadable from JavaScript, so a cross-site scripting bug cannot walk off with the
        // session — the difference between a defaced page and a stolen account.
        options.Cookie.HttpOnly = true;

        // Sent on top-level navigations to this site but not on cross-site form posts, which is
        // what stops another origin acting as the signed-in user.
        options.Cookie.SameSite = SameSiteMode.Lax;

        // Always in production, even though Caddy terminates TLS and forwards plain HTTP: the flag
        // is about what the *browser* will do, and the browser is speaking HTTPS. Development runs
        // on plain HTTP with no certificate, where Always would mean the cookie is never sent.
        options.Cookie.SecurePolicy = isDevelopment
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        options.ExpireTimeSpan = Lifetime;
        options.SlidingExpiration = true;

        // This is an API, not a site with login pages. Without these, an unauthenticated request is
        // answered with a 302 to a path that does not exist, which the frontend cannot act on.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }

    /// <summary>Builds the principal stored in the cookie.</summary>
    public static ClaimsPrincipal PrincipalFor(SignedInUser user) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
            ],
            Scheme));

    /// <summary>Reads the account id back out of a principal, or null if not signed in.</summary>
    public static Guid? UserIdOf(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

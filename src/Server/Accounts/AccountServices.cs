namespace Vtt.Server.Accounts;

public static class AccountServices
{
    /// <summary>
    /// Registers everything the Accounts module offers the rest of the application.
    /// </summary>
    /// <remarks>
    /// One entry point per module, mirroring <c>Infrastructure/DatabaseServices.cs</c>, so
    /// <c>Program.cs</c> grows a line per module rather than a line per service.
    /// </remarks>
    public static IServiceCollection AddAccounts(this IServiceCollection services) =>
        services
            .AddSingleton<IPasswordHasher, IdentityPasswordHasher>()

            // Scoped, not singleton: they hold a DbContext, which is scoped per request and is not
            // thread-safe.
            .AddScoped<IInviteService, InviteService>()
            .AddScoped<IRegistrationService, RegistrationService>()
            .AddScoped<ISignInService, SignInService>();

    /// <summary>
    /// Registers cookie authentication. Separate from <see cref="AddAccounts"/> because it needs to
    /// know whether this is a development host, and because it adds middleware rather than services.
    /// </summary>
    public static IServiceCollection AddSessionCookie(this IServiceCollection services, bool isDevelopment) =>
        services
            .AddAuthentication(SessionCookie.Scheme)
            .AddCookie(SessionCookie.Scheme, options => SessionCookie.Configure(options, isDevelopment))
            .Services
            .AddAuthorization();
}

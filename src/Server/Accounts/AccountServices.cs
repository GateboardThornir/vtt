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
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
}

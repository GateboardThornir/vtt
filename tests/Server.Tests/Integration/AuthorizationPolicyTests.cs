using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Accounts;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Integration;

/// <summary>
/// The policies themselves, rather than any one endpoint's use of them.
/// </summary>
[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class AuthorizationPolicyTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string Password = "a perfectly ordinary passphrase";

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnAccountDisabledMidSessionLosesAccessOnItsNextRequest()
    {
        // The gap 013 left and 016 closes: disabling an account stopped it signing in, but the
        // cookie it was already holding kept working until it expired. Revocation that takes
        // effect at some unknown future moment is not revocation.
        var id = await CreateAsync("Admin", PlatformRole.Admin);
        using var client = await SignedInAsync("Admin");

        Assert.Equal(HttpStatusCode.OK, (await GetAdminAsync(client)).StatusCode);

        await SetAsync(id, user => user.TransitionTo(AccountState.Disabled));

        Assert.Equal(HttpStatusCode.Forbidden, (await GetAdminAsync(client)).StatusCode);
    }

    [Fact]
    public async Task AnAdministratorDemotedMidSessionLosesAdminAccess()
    {
        var id = await CreateAsync("Admin", PlatformRole.Admin);
        using var client = await SignedInAsync("Admin");

        Assert.Equal(HttpStatusCode.OK, (await GetAdminAsync(client)).StatusCode);

        await SetRoleAsync(id, PlatformRole.Member);

        Assert.Equal(HttpStatusCode.Forbidden, (await GetAdminAsync(client)).StatusCode);
    }

    [Fact]
    public async Task APromotedMemberGainsAdminAccessWithoutSigningInAgain()
    {
        // The same property in the other direction, and the reason it is worth reading the database
        // rather than the cookie: the answer is always current.
        var id = await CreateAsync("Player", PlatformRole.Member);
        using var client = await SignedInAsync("Player");

        Assert.Equal(HttpStatusCode.Forbidden, (await GetAdminAsync(client)).StatusCode);

        await SetRoleAsync(id, PlatformRole.Admin);

        Assert.Equal(HttpStatusCode.OK, (await GetAdminAsync(client)).StatusCode);
    }

    [Fact]
    public async Task AnAnonymousCallerIsUnauthorisedRatherThanForbidden()
    {
        using var client = fixture.CreateClient();

        // 401 and 403 mean different things to a client: "you are nobody" versus "you are somebody
        // who may not". The frontend at 017 will branch on exactly this.
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAdminAsync(client)).StatusCode);
    }

    [Fact]
    public async Task ThePolicyFailsClosedForAnAccountThatNoLongerExists()
    {
        var id = await CreateAsync("Ghost", PlatformRole.Admin);
        using var client = await SignedInAsync("Ghost");

        await using (var scope = NewScope())
        {
            await scope.Context.Set<User>().Where(user => user.Id == id).ExecuteDeleteAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await GetAdminAsync(client)).StatusCode);
    }

    [Fact]
    public async Task EnumsCrossTheWireAsNamesRatherThanNumbers()
    {
        // A payload saying "state": 1 forces every client to keep its own copy of the numbering,
        // and breaks silently the day a value is inserted in the middle. The frontend translates
        // these names directly into message keys, so a number would render as a raw key on screen.
        await CreateAsync("Admin", PlatformRole.Admin);
        using var client = await SignedInAsync("Admin");

        var body = await (await GetAdminAsync(client)).Content.ReadAsStringAsync();

        Assert.Contains("\"state\":\"Active\"", body, StringComparison.Ordinal);
        Assert.Contains("\"role\":\"Admin\"", body, StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> GetAdminAsync(HttpClient client) =>
        client.GetAsync(new Uri("/api/admin/accounts", UriKind.Relative));

    private async Task<HttpClient> SignedInAsync(string username)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/session", new SignInRequest(username, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return client;
    }

    private async Task<Guid> CreateAsync(string username, PlatformRole role)
    {
        await using var scope = NewScope();
        var hash = scope.Provider.GetRequiredService<IPasswordHasher>().Hash(Password);
        var user = User.CreateActive(username, hash, fixture.Clock.GetUtcNow());

        if (role == PlatformRole.Member)
        {
            scope.Context.Entry(user).Property(nameof(User.Role)).CurrentValue = PlatformRole.Member;
        }

        scope.Context.Set<User>().Add(user);
        await scope.Context.SaveChangesAsync();

        return user.Id;
    }

    private async Task SetAsync(Guid id, Action<User> change)
    {
        await using var scope = NewScope();
        var user = await scope.Context.Set<User>().SingleAsync(candidate => candidate.Id == id);

        change(user);
        await scope.Context.SaveChangesAsync();
    }

    private async Task SetRoleAsync(Guid id, PlatformRole role)
    {
        await using var scope = NewScope();
        var user = await scope.Context.Set<User>().SingleAsync(candidate => candidate.Id == id);

        scope.Context.Entry(user).Property(nameof(User.Role)).CurrentValue = role;
        await scope.Context.SaveChangesAsync();
    }

    private Scope NewScope() => new(fixture.Factory.Services.CreateScope());

    private sealed class Scope(IServiceScope scope) : IAsyncDisposable
    {
        public IServiceProvider Provider { get; } = scope.ServiceProvider;

        public VttDbContext Context { get; } = scope.ServiceProvider.GetRequiredService<VttDbContext>();

        public ValueTask DisposeAsync()
        {
            scope.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

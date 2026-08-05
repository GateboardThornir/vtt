using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Accounts;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Integration;

[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class AccountAdministrationTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string Password = "a perfectly ordinary passphrase";

    /// <remarks>
    /// Mirrors the server's configuration: enums cross the wire as names, so a client reading them
    /// needs the same converter. A test using the defaults would be reading a different contract
    /// from the one that ships.
    /// </remarks>
    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnAdministratorSeesPendingAccounts()
    {
        await CreateAdminAsync("Admin");
        await CreatePendingAsync("Newcomer");

        using var client = await SignedInAsync("Admin");
        var pending = await client.GetFromJsonAsync<List<AccountSummary>>(
            "/api/admin/accounts/pending",
            _jsonOptions);

        Assert.Equal("Newcomer", Assert.Single(pending!).Username);
    }

    [Fact]
    public async Task ApprovingAnAccountLetsItSignIn()
    {
        await CreateAdminAsync("Admin");
        var newcomer = await CreatePendingAsync("Newcomer");

        using var admin = await SignedInAsync("Admin");
        var response = await SetStateAsync(admin, newcomer, AccountState.Active);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var client = fixture.CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/session", new SignInRequest("Newcomer", Password));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task RejectingAnAccountDisablesIt()
    {
        await CreateAdminAsync("Admin");
        var newcomer = await CreatePendingAsync("Newcomer");

        using var admin = await SignedInAsync("Admin");
        await SetStateAsync(admin, newcomer, AccountState.Disabled);

        using var client = fixture.CreateClient();
        var signIn = await client.PostAsJsonAsync("/api/session", new SignInRequest("Newcomer", Password));

        Assert.Equal(HttpStatusCode.Forbidden, signIn.StatusCode);
        Assert.Contains("account_disabled", await signIn.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADisabledAccountCanBeReEnabled()
    {
        await CreateAdminAsync("Admin");
        var newcomer = await CreatePendingAsync("Newcomer");

        using var admin = await SignedInAsync("Admin");
        await SetStateAsync(admin, newcomer, AccountState.Disabled);
        Assert.Equal(HttpStatusCode.NoContent, (await SetStateAsync(admin, newcomer, AccountState.Active)).StatusCode);
    }

    [Fact]
    public async Task AnIllegalTransitionIsRefused()
    {
        await CreateAdminAsync("Admin");
        var newcomer = await CreatePendingAsync("Newcomer");

        using var admin = await SignedInAsync("Admin");

        // Pending -> Pending is not a decision anybody made; approving twice from a stale screen
        // should not silently succeed.
        Assert.Equal(HttpStatusCode.Conflict, (await SetStateAsync(admin, newcomer, AccountState.Pending)).StatusCode);
    }

    [Fact]
    public async Task AnUnknownAccountIsNotFound()
    {
        await CreateAdminAsync("Admin");
        using var admin = await SignedInAsync("Admin");

        Assert.Equal(HttpStatusCode.NotFound, (await SetStateAsync(admin, Guid.NewGuid(), AccountState.Active)).StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/accounts")]
    [InlineData("/api/admin/accounts/pending")]
    public async Task AnOrdinaryMemberGetsNothing(string path)
    {
        await CreateAdminAsync("Admin");
        await CreateMemberAsync("Player");

        using var member = await SignedInAsync("Player");
        var response = await member.GetAsync(new Uri(path, UriKind.Relative));

        // Forbidden and, crucially, no body: the roster of an invitation-only platform is exactly
        // the thing a member should not be able to read.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("Admin", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMemberCannotChangeAnyoneSState()
    {
        await CreateAdminAsync("Admin");
        await CreateMemberAsync("Player");
        var newcomer = await CreatePendingAsync("Newcomer");

        using var member = await SignedInAsync("Player");
        Assert.Equal(HttpStatusCode.Forbidden, (await SetStateAsync(member, newcomer, AccountState.Active)).StatusCode);

        await using var scope = NewScope();
        var stored = await scope.Context.Set<User>().SingleAsync(user => user.Id == newcomer);
        Assert.Equal(AccountState.Pending, stored.State);
    }

    [Theory]
    [InlineData("/api/admin/accounts")]
    [InlineData("/api/admin/accounts/pending")]
    public async Task AnAnonymousCallerIsRefused(string path)
    {
        using var client = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri(path, UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task ADemotedAdministratorLosesAccessWithoutSigningOut()
    {
        // The reason 013 kept roles out of the cookie. If the role travelled in it, this session
        // would keep its powers until it happened to be reissued.
        await CreateAdminAsync("Admin");
        var second = await CreateAdminAsync("Second");

        using var client = await SignedInAsync("Second");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/api/admin/accounts", UriKind.Relative))).StatusCode);

        await using (var scope = NewScope())
        {
            var user = await scope.Context.Set<User>().SingleAsync(u => u.Id == second);
            scope.Context.Entry(user).Property(nameof(User.Role)).CurrentValue = PlatformRole.Member;
            await scope.Context.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(new Uri("/api/admin/accounts", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task NoListingEverCarriesAPasswordHash()
    {
        await CreateAdminAsync("Admin");
        using var client = await SignedInAsync("Admin");

        var body = await (await client.GetAsync(new Uri("/api/admin/accounts", UriKind.Relative))).Content.ReadAsStringAsync();

        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheBootstrapCommandProducesAnAdministrator()
    {
        await using var output = new StringWriter();
        var queue = new Queue<string>([Password, Password]);

        await CreateAccountCommand.RunAsync(
            fixture.Factory.Services,
            ["create-account", "Bootstrapped"],
            output,
            _ => queue.Dequeue());

        await using var scope = NewScope();
        var user = await scope.Context.Set<User>().SingleAsync();

        Assert.Equal(PlatformRole.Admin, user.Role);
        Assert.Equal(AccountState.Active, user.State);
    }

    [Fact]
    public async Task RegistrationProducesAnOrdinaryMember()
    {
        var admin = await CreateAdminAsync("Admin");

        IssuedInvite invite;
        await using (var scope = NewScope())
        {
            invite = await scope.Provider.GetRequiredService<IInviteService>().IssueAsync(admin);
        }

        using var client = fixture.CreateClient();
        await client.PostAsJsonAsync("/api/registration", new RegistrationRequest(invite.Token, "Newcomer", Password));

        await using var check = NewScope();
        var user = await check.Context.Set<User>().SingleAsync(u => u.UsernameNormalized == "newcomer");

        Assert.Equal(PlatformRole.Member, user.Role);
    }

    private Task<Guid> CreateAdminAsync(string username) => CreateAsync(username, admin: true, pending: false);

    private Task<Guid> CreateMemberAsync(string username) => CreateAsync(username, admin: false, pending: false);

    private Task<Guid> CreatePendingAsync(string username) => CreateAsync(username, admin: false, pending: true);

    private async Task<Guid> CreateAsync(string username, bool admin, bool pending)
    {
        await using var scope = NewScope();
        var hasher = scope.Provider.GetRequiredService<IPasswordHasher>();
        var hash = hasher.Hash(Password);

        var user = pending
            ? User.Register(username, hash, fixture.Clock.GetUtcNow())
            : User.CreateActive(username, hash, fixture.Clock.GetUtcNow());

        if (!pending && !admin)
        {
            scope.Context.Entry(user).Property(nameof(User.Role)).CurrentValue = PlatformRole.Member;
        }

        scope.Context.Set<User>().Add(user);
        await scope.Context.SaveChangesAsync();

        return user.Id;
    }

    private async Task<HttpClient> SignedInAsync(string username)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/session", new SignInRequest(username, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return client;
    }

    private static Task<HttpResponseMessage> SetStateAsync(HttpClient client, Guid id, AccountState state) =>
        client.PutAsJsonAsync($"/api/admin/accounts/{id}/state", new SetAccountStateRequest(state));

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

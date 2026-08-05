using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vtt.Server.Accounts;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Integration;

[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class SessionTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string Password = "a perfectly ordinary passphrase";

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnActiveAccountCanSignInAndIsThenRecognised()
    {
        await CreateAsync("Mattia", AccountState.Active);
        using var client = fixture.CreateClient();

        var signIn = await SignInAsync(client, "Mattia", Password);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var session = await client.GetAsync(new Uri("/api/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        var body = await session.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.Equal("Mattia", body?.Username);
    }

    [Fact]
    public async Task TheUsernameIsMatchedCaseInsensitively()
    {
        await CreateAsync("Mattia", AccountState.Active);
        using var client = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "MATTIA", Password)).StatusCode);
    }

    [Fact]
    public async Task WithoutACookieThereIsNoSession()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync(new Uri("/api/session", UriKind.Relative));

        // A 401, not a redirect to a login page that does not exist.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SigningOutEndsTheSession()
    {
        await CreateAsync("Mattia", AccountState.Active);
        using var client = fixture.CreateClient();
        await SignInAsync(client, "Mattia", Password);

        var signOut = await client.DeleteAsync(new Uri("/api/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);

        var after = await client.GetAsync(new Uri("/api/session", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task TheCookieIsHttpOnlyAndSameSiteLax()
    {
        await CreateAsync("Mattia", AccountState.Active);
        using var client = fixture.CreateClient();

        var response = await SignInAsync(client, "Mattia", Password);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.Contains(SessionCookie.Name, cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AWrongPasswordAndAnUnknownAccountAreIndistinguishable()
    {
        await CreateAsync("Mattia", AccountState.Active);
        using var client = fixture.CreateClient();

        var wrongPassword = await SignInAsync(client, "Mattia", "not the right passphrase");
        var noSuchAccount = await SignInAsync(client, "Nobody", Password);

        // If these differed, the login form would be a way to find out who has an account here —
        // which on an invitation-only platform is the membership list.
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, noSuchAccount.StatusCode);
        Assert.Equal(
            await wrongPassword.Content.ReadAsStringAsync(),
            await noSuchAccount.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task APendingAccountIsToldToWait()
    {
        await CreateAsync("Newcomer", AccountState.Pending);
        using var client = fixture.CreateClient();

        var response = await SignInAsync(client, "Newcomer", Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("awaiting_approval", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task ADisabledAccountIsToldSo()
    {
        await CreateAsync("Former", AccountState.Disabled);
        using var client = fixture.CreateClient();

        var response = await SignInAsync(client, "Former", Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("account_disabled", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWrongPasswordOnAPendingAccountRevealsNothingAboutItsState()
    {
        // The check order is the point: password first, state second. Reversed, this would answer
        // "awaiting approval" to anyone who guessed a username, which is an enumeration oracle.
        await CreateAsync("Newcomer", AccountState.Pending);
        using var client = fixture.CreateClient();

        var response = await SignInAsync(client, "Newcomer", "not the right passphrase");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("awaiting", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoResponseEverCarriesAPasswordOrAHash()
    {
        await CreateAsync("Mattia", AccountState.Active);
        using var client = fixture.CreateClient();

        var signIn = await SignInAsync(client, "Mattia", Password);
        var body = await signIn.Content.ReadAsStringAsync();

        Assert.DoesNotContain(Password, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnOutdatedHashIsUpgradedOnSuccessfulSignIn()
    {
        var legacy = LegacyHashOf(Password);
        await CreateAsync("Mattia", AccountState.Active, legacy);

        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "Mattia", Password)).StatusCode);

        await using var scope = NewScope();
        var user = await scope.Context.Set<User>().SingleAsync();

        // Signing in is the only moment the plaintext exists, so it is the only moment the stored
        // hash can be upgraded. This is how a work factor rises over years without asking anybody
        // to change their password.
        Assert.NotEqual(legacy, user.PasswordHash);

        // And the upgraded hash still works.
        using var second = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(second, "Mattia", Password)).StatusCode);
    }

    /// <summary>
    /// Hashes in the framework's version 2 format — genuinely produced, not a fabricated constant,
    /// so the verifier really does accept it while reporting that a rehash is due.
    /// </summary>
    private static string LegacyHashOf(string password) =>
        new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2,
        })).HashPassword(null!, password);

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string username, string password) =>
        client.PostAsJsonAsync("/api/session", new SignInRequest(username, password));

    private async Task CreateAsync(string username, AccountState state, string? passwordHash = null)
    {
        await using var scope = NewScope();
        var hasher = scope.Provider.GetRequiredService<IPasswordHasher>();

        var user = state == AccountState.Active
            ? User.CreateActive(username, passwordHash ?? hasher.Hash(Password), fixture.Clock.GetUtcNow())
            : User.Register(username, passwordHash ?? hasher.Hash(Password), fixture.Clock.GetUtcNow());

        if (state == AccountState.Disabled)
        {
            // No transition exists yet — 014 owns approving and disabling — so the state is set
            // through the same path the seed data uses.
            scope.Context.Entry(user).Property(nameof(User.State)).CurrentValue = AccountState.Disabled;
        }

        scope.Context.Set<User>().Add(user);
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

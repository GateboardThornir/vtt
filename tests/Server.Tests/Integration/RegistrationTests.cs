using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vtt.Server.Accounts;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Integration;

/// <summary>
/// Registration exercised through the real HTTP pipeline.
/// </summary>
[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class RegistrationTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string GoodPassword = "a perfectly ordinary passphrase";

    private Guid _issuer;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();

        var issuer = User.CreateActive("Issuer", "hash", fixture.Clock.GetUtcNow());
        _issuer = issuer.Id;

        await using var scope = NewScope();
        scope.Context.Set<User>().Add(issuer);
        await scope.Context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AValidInviteProducesAPendingAccount()
    {
        var invite = await IssueAsync();

        var response = await PostAsync(invite.Token, "Newcomer", GoodPassword);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var scope = NewScope();
        var created = await scope.Context.Set<User>()
            .SingleAsync(user => user.UsernameNormalized == "newcomer");

        Assert.Equal(AccountState.Pending, created.State);
    }

    [Fact]
    public async Task RegisteringMarksTheInviteSpentByTheNewAccount()
    {
        var invite = await IssueAsync();

        await PostAsync(invite.Token, "Newcomer", GoodPassword);

        await using var scope = NewScope();
        var created = await scope.Context.Set<User>()
            .SingleAsync(user => user.UsernameNormalized == "newcomer");
        var spent = await scope.Context.Set<Invite>().SingleAsync();

        Assert.Equal(created.Id, spent.ConsumedByUserId);
        Assert.NotNull(spent.ConsumedAt);
    }

    [Fact]
    public async Task TheResponseCarriesNoCredentialOfAnyKind()
    {
        var invite = await IssueAsync();

        var response = await PostAsync(invite.Token, "Newcomer", GoodPassword);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(GoodPassword, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(invite.Token, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);

        // Registering is not signing in. Task 013 owns sessions.
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task AnExpiredInviteIsRefusedAndCreatesNothing()
    {
        var invite = await IssueAsync();
        fixture.Clock.Advance(Invite.Lifetime + TimeSpan.FromSeconds(1));

        var response = await PostAsync(invite.Token, "Newcomer", GoodPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invite_expired", await ErrorOf(response));
        await AssertNoAccountNamed("newcomer");
    }

    [Fact]
    public async Task AnAlreadySpentInviteIsRefusedAndCreatesNothing()
    {
        var invite = await IssueAsync();
        await PostAsync(invite.Token, "First", GoodPassword);

        var response = await PostAsync(invite.Token, "Second", GoodPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invite_already_used", await ErrorOf(response));
        await AssertNoAccountNamed("second");
    }

    [Fact]
    public async Task AnUnrecognisedTokenGetsTheGenericRefusal()
    {
        // Distinguishing "expired" from "used" tells a real holder why their link failed. Saying
        // whether an arbitrary string is a token at all would tell an attacker something instead.
        var response = await PostAsync(InviteToken.Generate(), "Newcomer", GoodPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invite_invalid", await ErrorOf(response));
        await AssertNoAccountNamed("newcomer");
    }

    [Fact]
    public async Task ATakenUsernameIsRefusedCleanly()
    {
        var invite = await IssueAsync();

        var response = await PostAsync(invite.Token, "Issuer", GoodPassword);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("username_taken", await ErrorOf(response));
    }

    [Fact]
    public async Task AUsernameDifferingOnlyInCaseIsAlsoTaken()
    {
        var invite = await IssueAsync();

        var response = await PostAsync(invite.Token, "ISSUER", GoodPassword);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ARefusedRegistrationLeavesTheInviteSpendable()
    {
        var invite = await IssueAsync();

        await PostAsync(invite.Token, "Issuer", GoodPassword);
        var second = await PostAsync(invite.Token, "Newcomer", GoodPassword);

        // The username clash rolled back before the invite was touched, so it is still good.
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Theory]
    [InlineData("ab", GoodPassword, "username_invalid")]
    [InlineData("has space", GoodPassword, "username_invalid")]
    [InlineData("Newcomer", "short", "password_too_short")]
    public async Task MalformedInputIsRefusedAtTheBoundary(string username, string password, string expected)
    {
        var invite = await IssueAsync();

        var response = await PostAsync(invite.Token, username, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expected, await ErrorOf(response));

        // Rejected before anything was written, so the invite survives for a corrected attempt.
        await using var scope = NewScope();
        Assert.Null((await scope.Context.Set<Invite>().SingleAsync()).ConsumedAt);
    }

    [Fact]
    public async Task ParallelRegistrationsOnOneInviteCreateExactlyOneAccount()
    {
        // 011 proved one invite cannot be spent twice. This proves the other half: the losers must
        // leave nothing behind. Without the transaction, each loser's user row survives its failed
        // registration and holds a username forever against an account nobody can sign into.
        const int Racers = 8;

        var invite = await IssueAsync();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, Racers)
                .Select(index => Task.Run(() => PostAsync(invite.Token, $"Racer{index}", GoodPassword))));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));

        await using var scope = NewScope();
        var accounts = await scope.Context.Set<User>()
            .Where(user => user.UsernameNormalized != "issuer")
            .ToListAsync();

        Assert.Single(accounts);
    }

    [Fact]
    public async Task ParallelRegistrationsOfOneUsernameProduceOneAccountAndNoServerError()
    {
        var first = await IssueAsync();
        var second = await IssueAsync();

        var responses = await Task.WhenAll(
            Task.Run(() => PostAsync(first.Token, "Contested", GoodPassword)),
            Task.Run(() => PostAsync(second.Token, "Contested", GoodPassword)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);
    }

    private async Task<IssuedInvite> IssueAsync()
    {
        await using var scope = NewScope();

        return await scope.Invites.IssueAsync(_issuer);
    }

    private async Task<HttpResponseMessage> PostAsync(string token, string username, string password)
    {
        using var client = fixture.CreateClient();

        return await client.PostAsJsonAsync(
            "/api/registration",
            new RegistrationRequest(token, username, password));
    }

    private static async Task<string?> ErrorOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<RegistrationError>())?.Error;

    private async Task AssertNoAccountNamed(string normalised)
    {
        await using var scope = NewScope();

        Assert.False(await scope.Context.Set<User>().AnyAsync(user => user.UsernameNormalized == normalised));
    }

    private Scope NewScope() => new(fixture.Factory.Services.CreateScope());

    private sealed class Scope(IServiceScope scope) : IAsyncDisposable
    {
        public VttDbContext Context { get; } = scope.ServiceProvider.GetRequiredService<VttDbContext>();

        public IInviteService Invites { get; } = scope.ServiceProvider.GetRequiredService<IInviteService>();

        public ValueTask DisposeAsync()
        {
            scope.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

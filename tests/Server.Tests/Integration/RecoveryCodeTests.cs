using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vtt.Server.Accounts;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Integration;

[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class RecoveryCodeTests(PostgresFixture fixture) : IAsyncLifetime
{
    private const string OldPassword = "the original passphrase here";
    private const string NewPassword = "a different passphrase now";

    private Guid _admin;
    private Guid _member;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        _admin = await CreateAsync("Admin", admin: true);
        _member = await CreateAsync("Player", admin: false);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AnAdministratorCanIssueACodeAndTheHolderChoosesANewPassword()
    {
        var code = await IssueAsync(_member);

        using var client = fixture.CreateClient();
        var reset = await client.PostAsJsonAsync(
            "/api/password-reset",
            new PasswordResetRequest(code.Code, NewPassword));

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync("Player", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task TheOldPasswordStopsWorking()
    {
        var code = await IssueAsync(_member);
        await RedeemAsync(code.Code, NewPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync("Player", OldPassword)).StatusCode);
    }

    [Fact]
    public async Task ThePlaintextIsNowhereInTheStoredRow()
    {
        var code = await IssueAsync(_member);

        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id || code_hash || user_id || issued_by_user_id || created_at || expires_at "
            + "|| coalesce(used_at::text, '') FROM recovery_codes",
            connection);

        var row = await command.ExecuteScalarAsync() as string;

        Assert.NotNull(row);
        Assert.DoesNotContain(code.Code, row, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(SecureToken.Hash(code.Code), row, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACodeWorksOnlyOnce()
    {
        var code = await IssueAsync(_member);
        await RedeemAsync(code.Code, NewPassword);

        var second = await RedeemAsync(code.Code, "yet another passphrase");

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync("Player", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentRedemptionsProduceExactlyOneWinner()
    {
        var code = await IssueAsync(_member);

        var attempts = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(index =>
                Task.Run(() => RedeemAsync(code.Code, $"passphrase number {index} here"))));

        Assert.Equal(1, attempts.Count(response => response.StatusCode == HttpStatusCode.NoContent));
    }

    [Fact]
    public async Task AnExpiredCodeIsRefused()
    {
        var code = await IssueAsync(_member);
        fixture.Clock.Advance(RecoveryCode.Lifetime + TimeSpan.FromSeconds(1));

        Assert.Equal(HttpStatusCode.BadRequest, (await RedeemAsync(code.Code, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync("Player", OldPassword)).StatusCode);
    }

    [Fact]
    public async Task AnUnknownCodeIsRefusedWithoutSayingWhy()
    {
        var response = await RedeemAsync(SecureToken.Generate(), NewPassword);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("code_invalid", body, StringComparison.Ordinal);
        Assert.DoesNotContain("expired", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AShortNewPasswordIsRefusedAndTheCodeSurvives()
    {
        var code = await IssueAsync(_member);

        Assert.Equal(HttpStatusCode.BadRequest, (await RedeemAsync(code.Code, "short")).StatusCode);

        // Rejected before the code was claimed, so a mistyped password does not burn the code.
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemAsync(code.Code, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task RecoveryRestoresThePasswordAndNothingElse()
    {
        // A disabled account gets its password back and stays disabled: recovery is about
        // credentials, not about readmission to the platform.
        await using (var scope = NewScope())
        {
            var user = await scope.Context.Set<User>().SingleAsync(u => u.Id == _member);
            user.TransitionTo(AccountState.Disabled);
            await scope.Context.SaveChangesAsync();
        }

        var code = await IssueAsync(_member);
        await RedeemAsync(code.Code, NewPassword);

        var signIn = await SignInAsync("Player", NewPassword);
        Assert.Equal(HttpStatusCode.Forbidden, signIn.StatusCode);
        Assert.Contains("account_disabled", await signIn.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await using var check = NewScope();
        var after = await check.Context.Set<User>().SingleAsync(u => u.Id == _member);
        Assert.Equal(PlatformRole.Member, after.Role);
    }

    [Fact]
    public async Task AMemberCannotIssueRecoveryCodes()
    {
        using var member = await SignedInClientAsync("Player", OldPassword);

        var response = await member.PostAsJsonAsync($"/api/admin/accounts/{_admin}/recovery-code", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnAnonymousCallerCannotIssueRecoveryCodes()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/admin/accounts/{_member}/recovery-code", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IssuingForAnUnknownAccountIsNotFound()
    {
        using var admin = await SignedInClientAsync("Admin", OldPassword);

        var response = await admin.PostAsJsonAsync($"/api/admin/accounts/{Guid.NewGuid()}/recovery-code", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<IssuedRecoveryCode> IssueAsync(Guid userId)
    {
        using var admin = await SignedInClientAsync("Admin", OldPassword);
        var response = await admin.PostAsJsonAsync($"/api/admin/accounts/{userId}/recovery-code", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<IssuedRecoveryCode>())!;
    }

    private async Task<HttpResponseMessage> RedeemAsync(string code, string password)
    {
        using var client = fixture.CreateClient();

        return await client.PostAsJsonAsync("/api/password-reset", new PasswordResetRequest(code, password));
    }

    private async Task<HttpResponseMessage> SignInAsync(string username, string password)
    {
        using var client = fixture.CreateClient();

        return await client.PostAsJsonAsync("/api/session", new SignInRequest(username, password));
    }

    private async Task<HttpClient> SignedInClientAsync(string username, string password)
    {
        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/session", new SignInRequest(username, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return client;
    }

    private async Task<Guid> CreateAsync(string username, bool admin)
    {
        await using var scope = NewScope();
        var hash = scope.Provider.GetRequiredService<IPasswordHasher>().Hash(OldPassword);
        var user = User.CreateActive(username, hash, fixture.Clock.GetUtcNow());

        if (!admin)
        {
            scope.Context.Entry(user).Property(nameof(User.Role)).CurrentValue = PlatformRole.Member;
        }

        scope.Context.Set<User>().Add(user);
        await scope.Context.SaveChangesAsync();

        return user.Id;
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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Vtt.Server.Accounts;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Integration;

[Collection(IntegrationDatabase.Name)]
[Trait("Category", "Integration")]
public class InviteTests(PostgresFixture fixture) : IAsyncLifetime
{
    private Guid _admin;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();

        // Invites carry foreign keys to users at both ends, so an account has to exist first.
        // Whether that account is an administrator is task 016's question, not this service's.
        var admin = User.Register("Admin", "hash", fixture.Clock.GetUtcNow());
        _admin = admin.Id;

        await using var scope = NewScope();
        scope.Context.Set<User>().Add(admin);
        await scope.Context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TheTokenIsNowhereInTheStoredRow()
    {
        var issued = await IssueAsync();

        // Every column, concatenated, searched for the plaintext. If the token were ever stored
        // instead of its hash, a leaked dump would be a supply of working invitations.
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT id || token_hash || created_by_user_id || created_at || expires_at "
            + "|| coalesce(consumed_at::text, '') || coalesce(consumed_by_user_id::text, '') FROM invites",
            connection);

        var row = await command.ExecuteScalarAsync() as string;

        Assert.NotNull(row);
        Assert.DoesNotContain(issued.Token, row, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(SecureToken.Hash(issued.Token), row, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AFreshInviteIsUsable()
    {
        var issued = await IssueAsync();

        Assert.Equal(InviteStatus.Ok, await ValidateAsync(issued.Token));
    }

    [Fact]
    public async Task AnInviteLapsesOnceItsLifetimeHasPassed()
    {
        var issued = await IssueAsync();

        // Advancing a fake clock, not sleeping for a week.
        fixture.Clock.Advance(Invite.Lifetime + TimeSpan.FromSeconds(1));

        Assert.Equal(InviteStatus.Expired, await ValidateAsync(issued.Token));
    }

    [Fact]
    public async Task AnExpiredInviteCannotBeSpent()
    {
        var issued = await IssueAsync();
        fixture.Clock.Advance(Invite.Lifetime + TimeSpan.FromSeconds(1));

        Assert.Equal(InviteStatus.Expired, await ConsumeAsync(issued.Token));
    }

    [Fact]
    public async Task AnInviteIsStillUsableAMomentBeforeItLapses()
    {
        var issued = await IssueAsync();
        fixture.Clock.Advance(Invite.Lifetime - TimeSpan.FromSeconds(1));

        Assert.Equal(InviteStatus.Ok, await ValidateAsync(issued.Token));
    }

    [Fact]
    public async Task SpendingAnInviteRecordsWhoSpentItAndWhen()
    {
        var issued = await IssueAsync();
        fixture.Clock.Advance(TimeSpan.FromHours(3));
        var spentAt = fixture.Clock.GetUtcNow();

        Assert.Equal(InviteStatus.Ok, await ConsumeAsync(issued.Token));

        await using var scope = NewScope();
        var invite = await scope.Context.Set<Invite>().SingleAsync();

        Assert.Equal(spentAt, invite.ConsumedAt);
        Assert.Equal(_admin, invite.ConsumedByUserId);
    }

    [Fact]
    public async Task AnInviteCannotBeSpentTwice()
    {
        var issued = await IssueAsync();

        Assert.Equal(InviteStatus.Ok, await ConsumeAsync(issued.Token));
        Assert.Equal(InviteStatus.AlreadyConsumed, await ConsumeAsync(issued.Token));
    }

    [Fact]
    public async Task ASpentInviteNoLongerValidates()
    {
        var issued = await IssueAsync();
        await ConsumeAsync(issued.Token);

        Assert.Equal(InviteStatus.AlreadyConsumed, await ValidateAsync(issued.Token));
    }

    [Fact]
    public async Task AnUnknownTokenIsRejected()
    {
        Assert.Equal(InviteStatus.NotFound, await ValidateAsync(SecureToken.Generate()));
        Assert.Equal(InviteStatus.NotFound, await ConsumeAsync(SecureToken.Generate()));
    }

    [Fact]
    public async Task ConcurrentRedemptionsOfOneInviteProduceExactlyOneWinner()
    {
        // The test this card exists for. Sequential calls would prove nothing: the race is between
        // a read and a write, and only genuinely parallel callers can enter it. Replace the
        // conditional UPDATE in InviteService with a read-then-write and this must fail.
        const int Racers = 16;

        var issued = await IssueAsync();

        var attempts = Enumerable.Range(0, Racers)
            .Select(_ => Task.Run(() => ConsumeAsync(issued.Token)))
            .ToArray();

        var outcomes = await Task.WhenAll(attempts);

        Assert.Equal(1, outcomes.Count(status => status == InviteStatus.Ok));
        Assert.Equal(Racers - 1, outcomes.Count(status => status == InviteStatus.AlreadyConsumed));
    }

    private async Task<IssuedInvite> IssueAsync()
    {
        await using var scope = NewScope();

        return await scope.Invites.IssueAsync(_admin);
    }

    private async Task<InviteStatus> ValidateAsync(string token)
    {
        await using var scope = NewScope();

        return await scope.Invites.ValidateAsync(token);
    }

    private async Task<InviteStatus> ConsumeAsync(string token)
    {
        // A scope per call, because each concurrent redemption needs its own DbContext.
        await using var scope = NewScope();

        return await scope.Invites.ConsumeAsync(token, _admin);
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

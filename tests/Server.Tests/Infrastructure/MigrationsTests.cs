using Microsoft.EntityFrameworkCore;
using Vtt.Server.Infrastructure;

namespace Vtt.Server.Tests.Infrastructure;

public class MigrationsTests
{
    /// <remarks>
    /// Trivially true while the model is empty. It earns its place from task 010 onward, when
    /// "changed an entity, forgot the migration" becomes possible — and it covers the naming
    /// convention for free, since dropping <c>UseSnakeCaseNamingConvention</c> would rename every
    /// table and column away from the committed snapshot.
    /// <para>
    /// The context is built through <see cref="DatabaseServices.Configure"/>, the same method the
    /// server uses: a test configuring its own options would assert against a model that does not
    /// ship, which is the drift it exists to catch. No connection is opened —
    /// <c>HasPendingModelChanges</c> compares the runtime model to the committed snapshot — so this
    /// passes with the container down and stays out of task 005's integration harness.
    /// </para>
    /// </remarks>
    [Fact]
    public void CommittedMigrationsMatchTheModel()
    {
        var options = new DbContextOptionsBuilder<VttDbContext>();
        DatabaseServices.Configure(options, "Host=localhost;Port=55432;Database=vtt");

        using var context = new VttDbContext((DbContextOptions<VttDbContext>)options.Options);

        Assert.False(
            context.Database.HasPendingModelChanges(),
            "The model has changed without a matching migration. Run " +
            "`scripts/ef.sh migrations add <Name> --output-dir Infrastructure/Migrations`.");
    }
}

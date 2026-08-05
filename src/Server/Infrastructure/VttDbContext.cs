using Microsoft.EntityFrameworkCore;

namespace Vtt.Server.Infrastructure;

/// <summary>
/// The single EF Core context for the modular monolith.
/// </summary>
/// <remarks>
/// One context for every module, not one per module: the modules share a database and a
/// transaction, and splitting the context would only add ceremony without adding a boundary.
/// The boundary is the folder and the public interface, per <c>.claude/rules/backend.md</c>.
/// </remarks>
public class VttDbContext(DbContextOptions<VttDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Entities are configured by their own module — Accounts/, Campaigns/, Table/ — each
        // supplying an IEntityTypeConfiguration<T> that is discovered here. Adding a table is
        // therefore a change inside one module folder, never an edit to this file.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VttDbContext).Assembly);
    }
}

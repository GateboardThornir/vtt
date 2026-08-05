using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty, and not a mistake to be fixed. Task 003 sets up the migration
            // pipeline; it defines no schema, because the first entities belong to task 010
            // (users) and task 020 (campaigns). Applying this migration creates the
            // __EFMigrationsHistory table, which is the whole point of it.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

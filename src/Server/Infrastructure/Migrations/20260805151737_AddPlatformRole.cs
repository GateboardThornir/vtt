using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                // 'Member', not the generated "": an empty string is not a PlatformRole, so every
                // pre-existing row would fail to materialise the moment anything read it. Any
                // account created by `create-account` before this migration needs promoting by
                // hand — theoretical today, since the platform is not deployed anywhere.
                defaultValue: "Member");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "role",
                table: "users");
        }
    }
}

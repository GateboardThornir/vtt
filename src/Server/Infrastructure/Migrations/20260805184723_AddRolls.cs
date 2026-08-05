using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rolls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    roller_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expression = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    kept = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    dropped = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    modifier = table.Column<int>(type: "integer", nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rolls", x => x.id);
                    table.ForeignKey(
                        name: "fk_rolls_play_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "play_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rolls_users_roller_user_id",
                        column: x => x.roller_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rolls_roller_user_id",
                table: "rolls",
                column: "roller_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_rolls_session_id_created_at",
                table: "rolls",
                columns: new[] { "session_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rolls");
        }
    }
}

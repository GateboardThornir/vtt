using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vtt.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignRoster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reordered by hand, and this is exactly what reading a generated migration is for.
            // EF put the DropColumn first: every existing campaign would have lost its Master
            // before anything had recorded who it was, with no way to find out afterwards. The
            // roster is created and populated from the column, and only then is the column dropped.
            migrationBuilder.CreateTable(
                name: "campaign_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campaign_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_campaign_members_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalTable: "campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_campaign_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_campaign_members_campaign_id_user_id",
                table: "campaign_members",
                columns: new[] { "campaign_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_campaign_members_user_id",
                table: "campaign_members",
                column: "user_id");

            // Every campaign that already exists gets its Master as an active roster row, dated to
            // the campaign's own creation because that is genuinely when they became its Master.
            migrationBuilder.Sql(
                """
                INSERT INTO campaign_members (id, campaign_id, user_id, role, state, created_at, responded_at)
                SELECT gen_random_uuid(), id, master_user_id, 'Master', 'Active', created_at, created_at
                FROM campaigns;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_campaigns_users_master_user_id",
                table: "campaigns");

            migrationBuilder.DropIndex(
                name: "ix_campaigns_master_user_id",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "master_user_id",
                table: "campaigns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Honest about what it can and cannot restore. The Master comes back, read out of the
            // roster before it is dropped — but every Player, every pending invitation and every
            // record of who once left is gone, because the old schema had nowhere to put them.
            migrationBuilder.AddColumn<Guid>(
                name: "master_user_id",
                table: "campaigns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                """
                UPDATE campaigns
                SET master_user_id = member.user_id
                FROM campaign_members AS member
                WHERE member.campaign_id = campaigns.id AND member.role = 'Master';
                """);

            migrationBuilder.DropTable(
                name: "campaign_members");

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_master_user_id",
                table: "campaigns",
                column: "master_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_campaigns_users_master_user_id",
                table: "campaigns",
                column: "master_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

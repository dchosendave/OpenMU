using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations.AdminPanel
{
    /// <inheritdoc />
    public partial class AddCastleSiegePointMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CastleSiegeBattle",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HolderGuildName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    WinnerGuildName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ScoresJson = table.Column<string>(type: "text", nullable: false),
                    RewardsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CastleSiegeBattle", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CastleSiegeVaultReward",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BattleId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginName = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    GuildName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Placement = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ItemGroup = table.Column<byte>(type: "smallint", nullable: false),
                    ItemNumber = table.Column<short>(type: "smallint", nullable: false),
                    ItemLevel = table.Column<byte>(type: "smallint", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastProblem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CastleSiegeVaultReward", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CastleSiegeBattle_StartedAt",
                schema: "admin",
                table: "CastleSiegeBattle",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CastleSiegeVaultReward_BattleId",
                schema: "admin",
                table: "CastleSiegeVaultReward",
                column: "BattleId");

            migrationBuilder.CreateIndex(
                name: "IX_CastleSiegeVaultReward_DeliveredAt_CreatedAt",
                schema: "admin",
                table: "CastleSiegeVaultReward",
                columns: new[] { "DeliveredAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CastleSiegeBattle",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "CastleSiegeVaultReward",
                schema: "admin");
        }
    }
}

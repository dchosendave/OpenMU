using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations.AdminPanel
{
    /// <inheritdoc />
    public partial class AddWebMiniGamePlays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebMiniGamePlay",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginName = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Game = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Bet = table.Column<int>(type: "integer", nullable: false),
                    Payout = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Result = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PlayedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebMiniGamePlay", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebMiniGamePlay_LoginName_PlayedAt",
                schema: "admin",
                table: "WebMiniGamePlay",
                columns: new[] { "LoginName", "PlayedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebMiniGamePlay_PlayedAt",
                schema: "admin",
                table: "WebMiniGamePlay",
                column: "PlayedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebMiniGamePlay",
                schema: "admin");
        }
    }
}

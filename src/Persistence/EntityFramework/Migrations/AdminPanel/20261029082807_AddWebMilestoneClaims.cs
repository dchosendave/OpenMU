using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations.AdminPanel
{
    /// <inheritdoc />
    public partial class AddWebMilestoneClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebMilestoneClaim",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginName = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CharacterName = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    GrandResets = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimedItem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebMilestoneClaim", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebMilestoneClaim_CharacterName_GrandResets",
                schema: "admin",
                table: "WebMilestoneClaim",
                columns: new[] { "CharacterName", "GrandResets" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebMilestoneClaim_LoginName",
                schema: "admin",
                table: "WebMilestoneClaim",
                column: "LoginName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebMilestoneClaim",
                schema: "admin");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations.AdminPanel
{
    /// <inheritdoc />
    public partial class AddPlayerSiteFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {



            migrationBuilder.CreateTable(
                name: "WebCheckIn",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginName = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    StreakDay = table.Column<int>(type: "integer", nullable: false),
                    Reward = table.Column<int>(type: "integer", nullable: false),
                    CoinType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebCheckIn", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebMarketListing",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerLogin = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EscrowLogin = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    CoinType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BuyerLogin = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebMarketListing", x => x.Id);
                });


            migrationBuilder.CreateTable(
                name: "WebNewsPost",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Tag = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Author = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebNewsPost", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "admin",
                table: "WebNewsPost",
                columns: new[] { "Id", "Author", "Body", "IsPublished", "PublishedAt", "Slug", "Summary", "Tag", "Title" },
                values: new object[,]
                {
                    { new Guid("6c1b9e7a-0001-4d2e-9f3a-5b8c7d6e4f01"), "team", "Reach **level 400** and pay **1,000,000 zen** to reset. You go back to level 10 and keep your stats, and every reset gives more points per level.\n\nStats go up to **32,767**, so there's room for every build. Use `/reset` in the game, or the character tools on the website.", true, new DateTime(2026, 10, 25, 12, 0, 0, 0, DateTimeKind.Utc), "reset-rules", "Reset at level 400 for 1,000,000 zen. Stats go up to 32,767, so every build has room to grow.", "Notice", "Reset rules" },
                    { new Guid("6c1b9e7a-0002-4d2e-9f3a-5b8c7d6e4f02"), "team", "Blade Knight skill hits can now chain into extra strikes: a second hit, sometimes a third, and with luck a fourth. Each strike is weaker than the one before.\n\nThe chances and damage are listed on the server info page.", true, new DateTime(2026, 10, 26, 12, 0, 0, 0, DateTimeKind.Utc), "wind-combo", "Skill hits can now chain into up to four combo strikes, each weaker than the last. The fourth is rare, and lucky.", "Update", "Blade Knights: the wind combo" },
                    { new Guid("6c1b9e7a-0003-4d2e-9f3a-5b8c7d6e4f03"), "team", "Create your account here, then log in through the game client or play right in your browser.\n\nOn the website you can also see your characters, move items between them, check the rankings and shop for coins.", true, new DateTime(2026, 10, 28, 12, 0, 0, 0, DateTimeKind.Utc), "website-open", "Create your account here, then log in through the game client or play right in your browser.", "Notice", "The website is open" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebCheckIn_LoginName_Day",
                schema: "admin",
                table: "WebCheckIn",
                columns: new[] { "LoginName", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebMarketListing_SellerLogin",
                schema: "admin",
                table: "WebMarketListing",
                column: "SellerLogin");

            migrationBuilder.CreateIndex(
                name: "IX_WebMarketListing_State_CreatedAt",
                schema: "admin",
                table: "WebMarketListing",
                columns: new[] { "State", "CreatedAt" });



            migrationBuilder.CreateIndex(
                name: "IX_WebNewsPost_PublishedAt",
                schema: "admin",
                table: "WebNewsPost",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WebNewsPost_Slug",
                schema: "admin",
                table: "WebNewsPost",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebCheckIn",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "WebMarketListing",
                schema: "admin");


            migrationBuilder.DropTable(
                name: "WebNewsPost",
                schema: "admin");



        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations.AdminPanel
{
    /// <inheritdoc />
    public partial class AddAccountManagerItemPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemOptionPreset",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Level = table.Column<byte>(type: "smallint", nullable: true),
                    Skill = table.Column<bool>(type: "boolean", nullable: false),
                    Luck = table.Column<bool>(type: "boolean", nullable: false),
                    OptionLevel = table.Column<int>(type: "integer", nullable: false),
                    Excellent = table.Column<bool>(type: "boolean", nullable: false),
                    Guardian = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemOptionPreset", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemPackage",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Zen = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemPackage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemPackageItem",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Group = table.Column<byte>(type: "smallint", nullable: false),
                    Number = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Level = table.Column<byte>(type: "smallint", nullable: false),
                    Durability = table.Column<double>(type: "double precision", nullable: false),
                    HasSkill = table.Column<bool>(type: "boolean", nullable: false),
                    SocketCount = table.Column<int>(type: "integer", nullable: false),
                    SetItemIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemPackageItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemPackageItem_ItemPackage_ItemPackageId",
                        column: x => x.ItemPackageId,
                        principalSchema: "admin",
                        principalTable: "ItemPackage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemPackageItemOption",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemPackageItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemPackageItemOption", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemPackageItemOption_ItemPackageItem_ItemPackageItemId",
                        column: x => x.ItemPackageItemId,
                        principalSchema: "admin",
                        principalTable: "ItemPackageItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "admin",
                table: "ItemOptionPreset",
                columns: new[] { "Id", "CreatedAt", "Excellent", "Guardian", "Level", "Luck", "Name", "OptionLevel", "Skill" },
                values: new object[,]
                {
                    { new Guid("7c6f4f5e-1d0b-4a51-9b1e-000000000001"), new DateTime(2026, 10, 20, 0, 0, 0, 0, DateTimeKind.Utc), false, false, null, true, "Luck, skill, max option", 7, true },
                    { new Guid("7c6f4f5e-1d0b-4a51-9b1e-000000000002"), new DateTime(2026, 10, 20, 0, 0, 1, 0, DateTimeKind.Utc), true, true, null, true, "Full option", 7, true },
                    { new Guid("7c6f4f5e-1d0b-4a51-9b1e-000000000003"), new DateTime(2026, 10, 20, 0, 0, 2, 0, DateTimeKind.Utc), false, false, (byte)13, true, "Freebie +13", 3, true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemPackage_Name",
                schema: "admin",
                table: "ItemPackage",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemPackageItem_ItemPackageId",
                schema: "admin",
                table: "ItemPackageItem",
                column: "ItemPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPackageItemOption_ItemPackageItemId",
                schema: "admin",
                table: "ItemPackageItemOption",
                column: "ItemPackageItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemOptionPreset",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "ItemPackageItemOption",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "ItemPackageItem",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "ItemPackage",
                schema: "admin");
        }
    }
}

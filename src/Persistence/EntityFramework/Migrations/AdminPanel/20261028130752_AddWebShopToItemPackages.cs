using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MUnique.OpenMU.Persistence.EntityFramework.Migrations.AdminPanel
{
    /// <inheritdoc />
    public partial class AddWebShopToItemPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInWebShop",
                schema: "admin",
                table: "ItemPackage",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WebShopCurrency",
                schema: "admin",
                table: "ItemPackage",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WebShopPrice",
                schema: "admin",
                table: "ItemPackage",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInWebShop",
                schema: "admin",
                table: "ItemPackage");

            migrationBuilder.DropColumn(
                name: "WebShopCurrency",
                schema: "admin",
                table: "ItemPackage");

            migrationBuilder.DropColumn(
                name: "WebShopPrice",
                schema: "admin",
                table: "ItemPackage");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ThoughtGarden.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "subscription_plan_id",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "subscription_plan",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    max_journal_entries_per_day = table.Column<int>(type: "integer", nullable: false),
                    max_garden_customizations_per_day = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric", nullable: false),
                    billing_period = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_plan", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "subscription_plan",
                columns: new[] { "id", "billing_period", "max_garden_customizations_per_day", "max_journal_entries_per_day", "name", "price" },
                values: new object[,]
                {
                    { 1, "Monthly", 2, 3, "Free", 0.00m },
                    { 2, "Monthly", 2147483647, 2147483647, "Pro", 9.99m }
                });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: 1,
                column: "subscription_plan_id",
                value: 2);

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: 2,
                column: "subscription_plan_id",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "ix_users_subscription_plan_id",
                table: "users",
                column: "subscription_plan_id");

            migrationBuilder.AddForeignKey(
                name: "fk_users_subscription_plan_subscription_plan_id",
                table: "users",
                column: "subscription_plan_id",
                principalTable: "subscription_plan",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_subscription_plan_subscription_plan_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "subscription_plan");

            migrationBuilder.DropIndex(
                name: "ix_users_subscription_plan_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "subscription_plan_id",
                table: "users");
        }
    }
}

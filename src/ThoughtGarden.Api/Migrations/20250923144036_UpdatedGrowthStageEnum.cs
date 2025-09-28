using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThoughtGarden.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedGrowthStageEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "garden_plants",
                keyColumn: "id",
                keyValue: 1,
                column: "stage",
                value: 3);

            migrationBuilder.UpdateData(
                table: "garden_plants",
                keyColumn: "id",
                keyValue: 4,
                column: "stage",
                value: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "garden_plants",
                keyColumn: "id",
                keyValue: 1,
                column: "stage",
                value: 2);

            migrationBuilder.UpdateData(
                table: "garden_plants",
                keyColumn: "id",
                keyValue: 4,
                column: "stage",
                value: 3);
        }
    }
}

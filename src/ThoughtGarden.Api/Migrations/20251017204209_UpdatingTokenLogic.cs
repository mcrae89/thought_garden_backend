using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThoughtGarden.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdatingTokenLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "token",
                table: "refresh_tokens");

            migrationBuilder.AddColumn<string>(
                name: "token_hash",
                table: "refresh_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at_revoked_at",
                table: "refresh_tokens",
                columns: new[] { "expires_at", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_revoked_at",
                table: "refresh_tokens",
                columns: new[] { "user_id", "revoked_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_expires_at_revoked_at",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_user_id_revoked_at",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "token_hash",
                table: "refresh_tokens");

            migrationBuilder.AddColumn<string>(
                name: "token",
                table: "refresh_tokens",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");
        }
    }
}

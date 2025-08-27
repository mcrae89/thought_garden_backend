using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ThoughtGarden.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "emotion_tags",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    color = table.Column<string>(type: "text", nullable: false),
                    icon = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_emotion_tags", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "plant_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    emotion_tag_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plant_types", x => x.id);
                    table.ForeignKey(
                        name: "fk_plant_types_emotion_tags_emotion_tag_id",
                        column: x => x.emotion_tag_id,
                        principalTable: "emotion_tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    subscription_plan_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_subscription_plan_subscription_plan_id",
                        column: x => x.subscription_plan_id,
                        principalTable: "subscription_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "garden_states",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    size = table.Column<int>(type: "integer", nullable: false),
                    snapshot_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_garden_states", x => x.id);
                    table.ForeignKey(
                        name: "fk_garden_states_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    mood_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journal_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_journal_entries_emotion_tags_mood_id",
                        column: x => x.mood_id,
                        principalTable: "emotion_tags",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_journal_entries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    theme = table.Column<string>(type: "text", nullable: false),
                    encryption_level = table.Column<string>(type: "text", nullable: false),
                    reminders = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "garden_plants",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    garden_state_id = table.Column<int>(type: "integer", nullable: false),
                    plant_type_id = table.Column<int>(type: "integer", nullable: false),
                    growth_progress = table.Column<double>(type: "double precision", nullable: false),
                    stage = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: true),
                    is_stored = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_garden_plants", x => x.id);
                    table.ForeignKey(
                        name: "fk_garden_plants_garden_states_garden_state_id",
                        column: x => x.garden_state_id,
                        principalTable: "garden_states",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_garden_plants_plant_types_plant_type_id",
                        column: x => x.plant_type_id,
                        principalTable: "plant_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "entry_emotions",
                columns: table => new
                {
                    entry_id = table.Column<int>(type: "integer", nullable: false),
                    emotion_id = table.Column<int>(type: "integer", nullable: false),
                    intensity = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entry_emotions", x => new { x.entry_id, x.emotion_id });
                    table.ForeignKey(
                        name: "fk_entry_emotions_emotion_tags_emotion_id",
                        column: x => x.emotion_id,
                        principalTable: "emotion_tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_entry_emotions_journal_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "emotion_tags",
                columns: new[] { "id", "color", "icon", "name" },
                values: new object[,]
                {
                    { 1, "#FFD700", "😊", "Happy" },
                    { 2, "#1E90FF", "😢", "Sad" },
                    { 3, "#FF4500", "😡", "Angry" },
                    { 4, "#32CD32", "😌", "Calm" }
                });

            migrationBuilder.InsertData(
                table: "subscription_plan",
                columns: new[] { "id", "billing_period", "max_garden_customizations_per_day", "max_journal_entries_per_day", "name", "price" },
                values: new object[,]
                {
                    { 1, "Monthly", 2, 3, "Free", 0.00m },
                    { 2, "Monthly", 2147483647, 2147483647, "Pro", 9.99m }
                });

            migrationBuilder.InsertData(
                table: "plant_types",
                columns: new[] { "id", "emotion_tag_id", "name" },
                values: new object[,]
                {
                    { 1, 1, "Sunflower" },
                    { 2, 2, "Willow" },
                    { 3, 3, "Cactus" },
                    { 4, 4, "Lotus" }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "email", "password_hash", "role", "subscription_plan_id", "user_name" },
                values: new object[,]
                {
                    { 1, "admin@example.com", "hashedpassword1", 1, 2, "admin" },
                    { 2, "user@example.com", "hashedpassword2", 0, 1, "regular" }
                });

            migrationBuilder.InsertData(
                table: "garden_states",
                columns: new[] { "id", "size", "snapshot_at", "user_id" },
                values: new object[,]
                {
                    { 1, 5, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 2, 5, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2 }
                });

            migrationBuilder.InsertData(
                table: "journal_entries",
                columns: new[] { "id", "created_at", "is_deleted", "mood_id", "text", "updated_at", "user_id" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, 1, "Feeling happy and accomplished today.", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, 3, "Got frustrated with a bug, but resolved it.", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, 2, "Sad about the weather, it's been gloomy.", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2 },
                    { 4, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, 4, "Went for a walk and felt calm afterward.", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2 }
                });

            migrationBuilder.InsertData(
                table: "entry_emotions",
                columns: new[] { "emotion_id", "entry_id", "intensity" },
                values: new object[,]
                {
                    { 4, 1, 5 },
                    { 2, 2, 3 },
                    { 3, 3, 2 },
                    { 1, 4, 4 }
                });

            migrationBuilder.InsertData(
                table: "garden_plants",
                columns: new[] { "id", "created_at", "garden_state_id", "growth_progress", "is_stored", "order", "plant_type_id", "stage", "updated_at" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, 0.80000000000000004, true, null, 1, 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, 0.20000000000000001, false, 1, 3, 0, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, 0.5, false, 2, 2, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, 1.0, true, null, 4, 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_entry_emotions_emotion_id",
                table: "entry_emotions",
                column: "emotion_id");

            migrationBuilder.CreateIndex(
                name: "ix_garden_plants_garden_state_id",
                table: "garden_plants",
                column: "garden_state_id");

            migrationBuilder.CreateIndex(
                name: "ix_garden_plants_plant_type_id",
                table: "garden_plants",
                column: "plant_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_garden_states_user_id",
                table: "garden_states",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_mood_id",
                table: "journal_entries",
                column: "mood_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_user_id",
                table: "journal_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_plant_types_emotion_tag_id",
                table: "plant_types",
                column: "emotion_tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_settings_user_id",
                table: "user_settings",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_subscription_plan_id",
                table: "users",
                column: "subscription_plan_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entry_emotions");

            migrationBuilder.DropTable(
                name: "garden_plants");

            migrationBuilder.DropTable(
                name: "user_settings");

            migrationBuilder.DropTable(
                name: "journal_entries");

            migrationBuilder.DropTable(
                name: "garden_states");

            migrationBuilder.DropTable(
                name: "plant_types");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "emotion_tags");

            migrationBuilder.DropTable(
                name: "subscription_plan");
        }
    }
}

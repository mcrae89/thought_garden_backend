using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.Data
{
    public class ThoughtGardenDbContext : DbContext
    {
        public ThoughtGardenDbContext(DbContextOptions<ThoughtGardenDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<EmotionTag> EmotionTags { get; set; }
        public DbSet<EntryEmotion> EntryEmotions { get; set; }
        public DbSet<UserSettings> UserSettings { get; set; }
        public DbSet<GardenState> GardenStates { get; set; }
        public DbSet<Plant> Plants { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---- Relationships ----
            modelBuilder.Entity<EntryEmotion>()
                .HasKey(ee => new { ee.EntryId, ee.EmotionId });

            modelBuilder.Entity<User>()
                .HasOne(u => u.Settings)
                .WithOne(s => s.User)
                .HasForeignKey<UserSettings>(s => s.UserId);

            // ---- Seed Data ----
            var seedDate = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc);

            // EmotionTags
            modelBuilder.Entity<EmotionTag>().HasData(
                new EmotionTag { Id = 1, Name = "Happy", Color = "#FFD700", Icon = "😊" },
                new EmotionTag { Id = 2, Name = "Sad", Color = "#1E90FF", Icon = "😢" },
                new EmotionTag { Id = 3, Name = "Angry", Color = "#FF4500", Icon = "😡" },
                new EmotionTag { Id = 4, Name = "Calm", Color = "#32CD32", Icon = "😌" }
            );

            // Users
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    UserName = "admin",
                    Email = "admin@example.com",
                    PasswordHash = "hashedpassword1", // TODO: replace with real hash
                    Role = UserRole.Admin
                },
                new User
                {
                    Id = 2,
                    UserName = "regular",
                    Email = "user@example.com",
                    PasswordHash = "hashedpassword2", // TODO: replace with real hash
                    Role = UserRole.User
                }
            );

            // GardenStates
            modelBuilder.Entity<GardenState>().HasData(
                new GardenState { Id = 1, UserId = 1, SnapshotAt = seedDate },
                new GardenState { Id = 2, UserId = 2, SnapshotAt = seedDate }
            );

            // Plants
            modelBuilder.Entity<Plant>().HasData(
                new Plant { Id = 1, GardenStateId = 1, EmotionTagId = 1, Name = "Sunflower", GrowthStage = GrowthStage.Bloom, GrowthProgress = 0.8, CreatedAt = seedDate, UpdatedAt = seedDate },
                new Plant { Id = 2, GardenStateId = 1, EmotionTagId = 3, Name = "Cactus", GrowthStage = GrowthStage.Seed, GrowthProgress = 0.2, CreatedAt = seedDate, UpdatedAt = seedDate },
                new Plant { Id = 3, GardenStateId = 2, EmotionTagId = 2, Name = "Willow", GrowthStage = GrowthStage.Sprout, GrowthProgress = 0.5, CreatedAt = seedDate, UpdatedAt = seedDate },
                new Plant { Id = 4, GardenStateId = 2, EmotionTagId = 4, Name = "Lotus", GrowthStage = GrowthStage.Mature, GrowthProgress = 1.0, CreatedAt = seedDate, UpdatedAt = seedDate }
            );

            // JournalEntries
            modelBuilder.Entity<JournalEntry>().HasData(
                new JournalEntry { Id = 1, UserId = 1, Text = "Feeling happy and accomplished today.", Mood = "Happy", CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new JournalEntry { Id = 2, UserId = 1, Text = "Got frustrated with a bug, but resolved it.", Mood = "Angry", CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new JournalEntry { Id = 3, UserId = 2, Text = "Sad about the weather, it's been gloomy.", Mood = "Sad", CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new JournalEntry { Id = 4, UserId = 2, Text = "Went for a walk and felt calm afterward.", Mood = "Calm", CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false }
            );

            // EntryEmotions (links between JournalEntries and EmotionTags)
            modelBuilder.Entity<EntryEmotion>().HasData(
                new EntryEmotion { EntryId = 1, EmotionId = 1, Intensity = 8 }, // Happy
                new EntryEmotion { EntryId = 2, EmotionId = 3, Intensity = 6 }, // Angry
                new EntryEmotion { EntryId = 3, EmotionId = 2, Intensity = 7 }, // Sad
                new EntryEmotion { EntryId = 4, EmotionId = 4, Intensity = 9 }  // Calm
            );
        }

    }
}

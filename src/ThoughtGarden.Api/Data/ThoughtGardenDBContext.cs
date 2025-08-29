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
        public DbSet<PlantType> PlantTypes { get; set; }
        public DbSet<GardenPlant> GardenPlants { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---- Relationships ----
            modelBuilder.Entity<EntryEmotion>()
                .HasKey(ee => new { ee.EntryId, ee.EmotionId });

            modelBuilder.Entity<EntryEmotion>()
                .HasOne(ee => ee.Entry)
                .WithMany(e => e.SecondaryEmotions)
                .HasForeignKey(ee => ee.EntryId);

            modelBuilder.Entity<EntryEmotion>()
                .HasOne(ee => ee.Emotion)
                .WithMany(et => et.EntryLinks)
                .HasForeignKey(ee => ee.EmotionId);


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
                    PasswordHash = "$2a$11$bMbVvslv1w8ctmZB9XJJl.EZHIHgshLMc8zGmryaeKOH2nx/iDFZy",
                    Role = UserRole.Admin,
                    SubscriptionPlanId = 2  // give admin Pro
                },
                new User
                {
                    Id = 2,
                    UserName = "regular",
                    Email = "user@example.com",
                    PasswordHash = "$2a$11$FVx.eRjAlmsDfYXTGklNEuXbP3o4Gb45QVkTop/yK0xo5PkUNHLH6",
                    Role = UserRole.User,
                    SubscriptionPlanId = 1  // free tier
                }
            );

            // GardenStates
            modelBuilder.Entity<GardenState>().HasData(
                new GardenState { Id = 1, UserId = 1, SnapshotAt = seedDate },
                new GardenState { Id = 2, UserId = 2, SnapshotAt = seedDate }
            );

            // ---- JournalEntries ----
            modelBuilder.Entity<JournalEntry>().HasData(
                new JournalEntry { Id = 1, UserId = 1, Text = "Feeling happy and accomplished today.", MoodId = 1, CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new JournalEntry { Id = 2, UserId = 1, Text = "Got frustrated with a bug, but resolved it.", MoodId = 3, CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new JournalEntry { Id = 3, UserId = 2, Text = "Sad about the weather, it's been gloomy.", MoodId = 2, CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new JournalEntry { Id = 4, UserId = 2, Text = "Went for a walk and felt calm afterward.", MoodId = 4, CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false }
            );

            // ---- EntryEmotions (secondary emotions only) ----
            modelBuilder.Entity<EntryEmotion>().HasData(
                // Entry 1 (primary = Happy, add nuance)
                new EntryEmotion { EntryId = 1, EmotionId = 4, Intensity = 5 }, // Calm undertone

                // Entry 2 (primary = Angry, add nuance)
                new EntryEmotion { EntryId = 2, EmotionId = 2, Intensity = 3 }, // Sad undertone

                // Entry 3 (primary = Sad, add nuance)
                new EntryEmotion { EntryId = 3, EmotionId = 3, Intensity = 2 }, // Angry undertone

                // Entry 4 (primary = Calm, add nuance)
                new EntryEmotion { EntryId = 4, EmotionId = 1, Intensity = 4 }  // Happy undertone
            );

            // Plant Types
            modelBuilder.Entity<PlantType>().HasData(
                new PlantType { Id = 1, Name = "Sunflower", EmotionTagId = 1 },
                new PlantType { Id = 2, Name = "Willow", EmotionTagId = 2 },
                new PlantType { Id = 3, Name = "Cactus", EmotionTagId = 3 },
                new PlantType { Id = 4, Name = "Lotus", EmotionTagId = 4 }
            );

            // Garden Plants
            modelBuilder.Entity<GardenPlant>().HasData(
                // User 1, Entry 1: Mood = Happy → PlantTypeId = 1 (Sunflower)
                new GardenPlant
                {
                    Id = 1,
                    GardenStateId = 1,
                    PlantTypeId = 1,
                    Stage = GardenPlant.GrowthStage.Bloom,
                    GrowthProgress = 0.8,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                    Order = null,        // not yet placed
                    IsStored = true      // in storage
                },

                // User 1, Entry 2: Mood = Angry → PlantTypeId = 3 (Cactus)
                new GardenPlant
                {
                    Id = 2,
                    GardenStateId = 1,
                    PlantTypeId = 3,
                    Stage = GardenPlant.GrowthStage.Seed,
                    GrowthProgress = 0.2,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                    Order = 1,           // placed first in garden
                    IsStored = false     // active in layout
                },

                // User 2, Entry 3: Mood = Sad → PlantTypeId = 2 (Willow)
                new GardenPlant
                {
                    Id = 3,
                    GardenStateId = 2,
                    PlantTypeId = 2,
                    Stage = GardenPlant.GrowthStage.Sprout,
                    GrowthProgress = 0.5,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                    Order = 2,           // placed second
                    IsStored = false
                },

                // User 2, Entry 4: Mood = Calm → PlantTypeId = 4 (Lotus)
                new GardenPlant
                {
                    Id = 4,
                    GardenStateId = 2,
                    PlantTypeId = 4,
                    Stage = GardenPlant.GrowthStage.Mature,
                    GrowthProgress = 1.0,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                    Order = null,
                    IsStored = true
                }
            );

            modelBuilder.Entity<SubscriptionPlan>().HasData(
                new SubscriptionPlan
                {
                    Id = 1,
                    Name = "Free",
                    MaxJournalEntriesPerDay = 3,
                    MaxGardenCustomizationsPerDay = 2,
                    Price = 0.00m
                },
                new SubscriptionPlan
                {
                    Id = 2,
                    Name = "Pro",
                    MaxJournalEntriesPerDay = int.MaxValue,
                    MaxGardenCustomizationsPerDay = int.MaxValue,
                    Price = 9.99m
                }
            );


        }

    }
}

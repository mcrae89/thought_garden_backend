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
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---- EntryEmotion ----
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

            // ---- User → UserSettings ----
            modelBuilder.Entity<User>()
                .HasOne(u => u.Settings)
                .WithOne(s => s.User)
                .HasForeignKey<UserSettings>(s => s.UserId);

            // ---- PlantType → EmotionTag ----
            modelBuilder.Entity<PlantType>()
                .HasOne(pt => pt.EmotionTag)
                .WithMany(et => et.PlantTypes)
                .HasForeignKey(pt => pt.EmotionTagId)
                .IsRequired();

            // ---- JournalEntry → User ----
            modelBuilder.Entity<JournalEntry>()
                .HasOne(j => j.User)
                .WithMany(u => u.JournalEntries)
                .HasForeignKey(j => j.UserId)
                .IsRequired();

            // ---- JournalEntry → EmotionTag (Mood) ----
            modelBuilder.Entity<JournalEntry>()
                .HasOne(j => j.Mood)
                .WithMany()
                .HasForeignKey(j => j.MoodId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---- GardenState → User ----
            modelBuilder.Entity<GardenState>()
                .HasOne(gs => gs.User)
                .WithMany(u => u.GardenStates)
                .HasForeignKey(gs => gs.UserId)
                .IsRequired();

            // ---- GardenPlant → GardenState ----
            modelBuilder.Entity<GardenPlant>()
                .HasOne(gp => gp.GardenState)
                .WithMany(gs => gs.GardenPlants)
                .HasForeignKey(gp => gp.GardenStateId)
                .IsRequired();

            // ---- GardenPlant → PlantType ----
            modelBuilder.Entity<GardenPlant>()
                .HasOne(gp => gp.PlantType)
                .WithMany(pt => pt.GardenPlants)
                .HasForeignKey(gp => gp.PlantTypeId)
                .IsRequired();

            // ---- RefreshToken → User ----
            modelBuilder.Entity<RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .IsRequired();

            // ---- User → SubscriptionPlan ----
            modelBuilder.Entity<User>()
                .HasOne(u => u.SubscriptionPlan)
                .WithMany(sp => sp.Users)
                .HasForeignKey(u => u.SubscriptionPlanId)
                .IsRequired();

            // ---- Seed Data (static, non-crypto) ----
            var seedDate = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc);

            // 1. Subscription Plans
            modelBuilder.Entity<SubscriptionPlan>().HasData(
                new SubscriptionPlan { Id = 1, Name = "Free", MaxJournalEntriesPerDay = 3, MaxGardenCustomizationsPerDay = 2, Price = 0.00m },
                new SubscriptionPlan { Id = 2, Name = "Pro", MaxJournalEntriesPerDay = int.MaxValue, MaxGardenCustomizationsPerDay = int.MaxValue, Price = 9.99m }
            );

            // 2. Users
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, UserName = "admin", Email = "admin@example.com", PasswordHash = "$2a$11$bMbVvslv1w8ctmZB9XJJl.EZHIHgshLMc8zGmryaeKOH2nx/iDFZy", Role = UserRole.Admin, SubscriptionPlanId = 2 },
                new User { Id = 2, UserName = "regular", Email = "user@example.com", PasswordHash = "$2a$11$FVx.eRjAlmsDfYXTGklNEuXbP3o4Gb45QVkTop/yK0xo5PkUNHLH6", Role = UserRole.User, SubscriptionPlanId = 1 }
            );

            // 3. EmotionTags
            modelBuilder.Entity<EmotionTag>().HasData(
                new EmotionTag { Id = 1, Name = "Happy", Color = "#FFD700", Icon = "😊" },
                new EmotionTag { Id = 2, Name = "Sad", Color = "#1E90FF", Icon = "😢" },
                new EmotionTag { Id = 3, Name = "Angry", Color = "#FF4500", Icon = "😡" },
                new EmotionTag { Id = 4, Name = "Calm", Color = "#32CD32", Icon = "😌" }
            );

            // 4. Plant Types
            modelBuilder.Entity<PlantType>().HasData(
                new PlantType { Id = 1, Name = "Sunflower", EmotionTagId = 1 },
                new PlantType { Id = 2, Name = "Willow", EmotionTagId = 2 },
                new PlantType { Id = 3, Name = "Cactus", EmotionTagId = 3 },
                new PlantType { Id = 4, Name = "Lotus", EmotionTagId = 4 }
            );

            // 5. GardenStates
            modelBuilder.Entity<GardenState>().HasData(
                new GardenState { Id = 1, UserId = 1, SnapshotAt = seedDate },
                new GardenState { Id = 2, UserId = 2, SnapshotAt = seedDate }
            );

            // 6. JournalEntries — removed from static seeding.
            //    Rationale: envelope-encrypted rows must be generated with the runtime KEKs.
            //    Create demo entries via API after startup so they’re encrypted correctly.

            // 7. EntryEmotions — removed (they referenced seeded JournalEntries).
            //    Add emotions to entries via the API after entries are created.

            // 8. Garden Plants
            modelBuilder.Entity<GardenPlant>().HasData(
                new GardenPlant
                {
                    Id = 1,
                    GardenStateId = 1,
                    PlantTypeId = 1,
                    Stage = GardenPlant.GrowthStage.Bloom,
                    GrowthProgress = 8,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                    Order = null,
                    IsStored = true
                },
                new GardenPlant
                {
                    Id = 2,
                    GardenStateId = 1,
                    PlantTypeId = 3,
                    Stage = GardenPlant.GrowthStage.Seed,
                    GrowthProgress = 2,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                    Order = 1,
                    IsStored = false
                },
                new GardenPlant
                {
                    Id = 3,
                    GardenStateId = 2,
                    PlantTypeId = 2,
                    Stage = GardenPlant.GrowthStage.Sprout,
                    GrowthProgress = 5,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                    Order = 2,
                    IsStored = false
                },
                new GardenPlant
                {
                    Id = 4,
                    GardenStateId = 2,
                    PlantTypeId = 4,
                    Stage = GardenPlant.GrowthStage.Mature,
                    GrowthProgress = 1,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                    Order = null,
                    IsStored = true
                }
            );
        }
    }
}

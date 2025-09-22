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
                .WithMany(et => et.PlantTypes)       // add ICollection<PlantType> to EmotionTag
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
                .WithMany(u => u.RefreshTokens)     // add ICollection<RefreshToken> to User
                .HasForeignKey(rt => rt.UserId)
                .IsRequired();

            // ---- User → SubscriptionPlan ----
            modelBuilder.Entity<User>()
                .HasOne(u => u.SubscriptionPlan)
                .WithMany(sp => sp.Users)
                .HasForeignKey(u => u.SubscriptionPlanId)
                .IsRequired();

            // ---- Seed Data ----
            var seedDate = new DateTime(2025, 01, 01, 0, 0, 0, DateTimeKind.Utc);

            // 1. Subscription Plans
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

            // 2. Users
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

            // 3. EmotionTags
            modelBuilder.Entity<EmotionTag>().HasData(
                new EmotionTag { Id = 1, Name = "Happy", Color = "#FFD700", Icon = "😊" },
                new EmotionTag { Id = 2, Name = "Sad", Color = "#1E90FF", Icon = "😢" },
                new EmotionTag { Id = 3, Name = "Angry", Color = "#FF4500", Icon = "😡" },
                new EmotionTag { Id = 4, Name = "Calm", Color = "#32CD32", Icon = "😌" }
            );

            // 4. Plant Types (depend on EmotionTags)
            modelBuilder.Entity<PlantType>().HasData(
                new PlantType { Id = 1, Name = "Sunflower", EmotionTagId = 1 },
                new PlantType { Id = 2, Name = "Willow", EmotionTagId = 2 },
                new PlantType { Id = 3, Name = "Cactus", EmotionTagId = 3 },
                new PlantType { Id = 4, Name = "Lotus", EmotionTagId = 4 }
            );

            // 5. GardenStates (depend on Users)
            modelBuilder.Entity<GardenState>().HasData(
                new GardenState { Id = 1, UserId = 1, SnapshotAt = seedDate },
                new GardenState { Id = 2, UserId = 2, SnapshotAt = seedDate }
            );

            // 6. JournalEntries (depend on Users + EmotionTags)
            modelBuilder.Entity<JournalEntry>().HasData(
                new JournalEntry { Id = 1, UserId = 1, Text = "j2mTHKGDmOn4hlryKv3eyL6P4ShFRRYdOQLuh1RDZeElQcHsZqKDdVzEWai9iAN/", IV = "cBHSM6AUxhuJsCIyMAvklg==", MoodId = 1, CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new JournalEntry { Id = 2, UserId = 1, Text = "taR9XokdNP9nxTZwNRwJbZHmLwZdWhmf4UOa5QPfVGIUx7whOYwf06Sd6G+D0Ebl", IV = "qwA2K9DHJfAGik2wzQrEug==", MoodId = 3, CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new JournalEntry { Id = 3, UserId = 2, Text = "msYvFrmn0F4ZBLqmhmlRNAmIREbkViB8Pan6nrjkLl17bDzUWLDO7hp1zYLjI49o", IV = "z37o+Qx3yP7xJLBmeELwjw==", MoodId = 2, CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false },
                new JournalEntry { Id = 4, UserId = 2, Text = "PR4aADhdP/4lmVSQbFkdQSpLmYFqOE1ue9MbvxfZ8DDeah/cIlYWmcmIuWBpsb6o", IV = "5dBrHWrNm2LMqfFfTIN6ww==", MoodId = 4, CreatedAt = seedDate, UpdatedAt = seedDate, IsDeleted = false }
            );

            // 7. EntryEmotions (depend on JournalEntries + EmotionTags)
            modelBuilder.Entity<EntryEmotion>().HasData(
                new EntryEmotion { EntryId = 1, EmotionId = 4, Intensity = 5 },
                new EntryEmotion { EntryId = 2, EmotionId = 2, Intensity = 3 },
                new EntryEmotion { EntryId = 3, EmotionId = 3, Intensity = 2 },
                new EntryEmotion { EntryId = 4, EmotionId = 1, Intensity = 4 }
            );

            // 8. Garden Plants (depend on GardenStates + PlantTypes)
            modelBuilder.Entity<GardenPlant>().HasData(
                new GardenPlant
                {
                    Id = 1,
                    GardenStateId = 1,
                    PlantTypeId = 1,
                    Stage = GardenPlant.GrowthStage.Bloom,
                    GrowthProgress = 0.8,
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
                    GrowthProgress = 0.2,
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
                    GrowthProgress = 0.5,
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
                    GrowthProgress = 1.0,
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate,
                    Order = null,
                    IsStored = true
                }
            );
        }

    }
}

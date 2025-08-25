using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.Data;

public class ThoughtGardenDbContext : DbContext
{
    public ThoughtGardenDbContext(DbContextOptions<ThoughtGardenDbContext> options) : base(options) {}
    public DbSet<User> Users { get; set; }
    public DbSet<JournalEntry> JournalEntries { get; set; }
    public DbSet<EmotionTag> EmotionTags { get; set; }
    public DbSet<EntryEmotion> EntryEmotions { get; set; }
    public DbSet<UserSettings> UserSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Composite key for EntryEmotion
        modelBuilder.Entity<EntryEmotion>()
            .HasKey(ee => new { ee.EntryId, ee.EmotionId });

        // One-to-one: User <-> UserSettings
        modelBuilder.Entity<User>()
            .HasOne(u => u.Settings)
            .WithOne(s => s.User)
            .HasForeignKey<UserSettings>(s => s.UserId);
    }
}

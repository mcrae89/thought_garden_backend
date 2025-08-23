using Microsoft.EntityFrameworkCore;

namespace ThoughtGarden.Api.Data;

public class ThoughtGardenDbContext : DbContext
{
    public ThoughtGardenDbContext(DbContextOptions<ThoughtGardenDbContext> options)
        : base(options)
    {
    }

    // TODO: add DbSets (e.g., JournalEntries, Users, GardenState)
    // public DbSet<JournalEntry> JournalEntries { get; set; } = null!;
}

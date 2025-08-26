using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class JournalEntryQueries
    {
        private readonly ThoughtGardenDbContext _db;

        public JournalEntryQueries(ThoughtGardenDbContext db) => _db = db;

        public async Task<List<JournalEntry>> GetJournalEntries(int userId) =>
            await _db.JournalEntries
                .Where(e => e.UserId == userId && !e.IsDeleted)
                .Include(e => e.Mood)
                .Include(e => e.SecondaryEmotions)
                    .ThenInclude(se => se.Emotion)
                .ToListAsync();

        public async Task<JournalEntry?> GetJournalEntryById(int id) =>
            await _db.JournalEntries
                .Include(e => e.Mood)
                .Include(e => e.SecondaryEmotions)
                    .ThenInclude(se => se.Emotion)
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
    }
}

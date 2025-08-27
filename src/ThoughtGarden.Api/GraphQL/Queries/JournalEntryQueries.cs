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

        [UseProjection]
        public IQueryable<JournalEntry> GetJournalEntries() => _db.JournalEntries;

        [UseProjection]
        public IQueryable<JournalEntry> GetJournalEntryById(int id) =>
            _db.JournalEntries.Where(jl => jl.Id == id);
    }
}

using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class JournalEntryQueries
    {
        [UseProjection]
        public IQueryable<JournalEntry> GetJournalEntries([Service] ThoughtGardenDbContext db) => db.JournalEntries;

        [UseProjection]
        public IQueryable<JournalEntry> GetJournalEntryById(int id, [Service] ThoughtGardenDbContext db) =>
            db.JournalEntries.Where(jl => jl.Id == id);
    }
}
